using FlowerShop.Data;
using FlowerShop.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;

namespace FlowerShop.Web.Pages.Account
{
    public class PlaceOrderModel(FlowerDbContext context) : PageModel
    {
        private readonly FlowerDbContext _context = context;

        [BindProperty, Required(ErrorMessage = "Введите номер телефона"), Phone]
        public string Phone { get; set; } = string.Empty;

        [BindProperty, DataType(DataType.DateTime), Required(ErrorMessage = "Время долно быть на 2 час больше")]
        public DateTime DeliveryDate { get; set; } = DateTime.Now;

        [BindProperty, Required(ErrorMessage = "Введите адрес доставки"), StringLength(500, MinimumLength = 10, ErrorMessage = "Адрес должен содержать от 10 до 500 символов")]
        public string DeliveryAddress { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; } = 0;


        public async Task<ActionResult> OnGetAsync()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart is null)
            {
                return Unauthorized();
            }
            TotalAmount = cart.Items.Sum(i => i.Quantity * i.PriceSnapshot);
            return Page();
        }

        public async Task<ActionResult> OnPostSubmitOrderAsync()
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError(string.Empty, "Данные некорректы.");
                return Page();
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            var user = await _context.UserDomains.FindAsync(userId);

            if (cart is null || cart.Items is null || cart.Items.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Корзина пуста.");
                return Page();
            }

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Пользователь не найден.");
                return Page();
            }

            if (string.IsNullOrWhiteSpace(user.CodeOrder))
            {
                string code;
                do
                {
                    code = GeneratedCode.Generated.GenerateRandomCode();
                } while (await _context.UserDomains
                    .AnyAsync(u => u.CodeOrder == code));

                user.CodeOrder = code;
            }

            var minDateTime = DateTime.Now.AddHours(2);
            if (DeliveryDate < minDateTime)
            {
                ModelState.AddModelError(string.Empty, $"Дата доставки не может быть раньше {minDateTime:dd.MM.yyyy HH:mm}");
                return Page();
            }

            var byBouquet = cart.Items
                .Where(i => i.BouquetId.HasValue)
                .GroupBy(i => i.BouquetId!.Value)
                .Select(g => new
                {
                    Id = g.Key,
                    RequiredQty = g.Sum(x => x.Quantity)
                })
                .ToList();

            var bySoftToy = cart.Items
                .Where(i => i.SoftToyId.HasValue)
                .GroupBy(i => i.SoftToyId!.Value)
                .Select(g => new
                {
                    Id = g.Key,
                    RequiredQty = g.Sum(x => x.Quantity)
                })
                .ToList();

            if (byBouquet.Count == 0 && bySoftToy.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "В корзине нет ни букетов, ни мягких игрушек.");
                return Page();
            }

            var bouquetIds = byBouquet.Select(x => x.Id).ToHashSet();
            var softToyIds = bySoftToy.Select(x => x.Id).ToHashSet();

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

            var bouquets = await _context.Bouquets
                .Where(b => bouquetIds.Contains(b.Id))
                .AsTracking()
                .ToListAsync();

            var softToys = await _context.SoftToys
                .Where(s => softToyIds.Contains(s.Id))
                .AsTracking()
                .ToListAsync();

            var missingBouquets = bouquetIds.Except(bouquets.Select(b => b.Id)).ToList();
            var missingSoftToys = softToyIds.Except(softToys.Select(s => s.Id)).ToList();

            if (missingBouquets.Count > 0 || missingSoftToys.Count > 0)
            {
                ModelState.AddModelError(string.Empty, "Некоторые позиции недоступны.");
                return Page();
            }

            foreach (var grp in byBouquet)
            {
                var b = bouquets.First(x => x.Id == grp.Id);
                if (b.Quantity < grp.RequiredQty)
                {
                    ModelState.AddModelError(string.Empty, $"Недостаточно на складе: «{b.Name}». Доступно {b.Quantity}, требуется {grp.RequiredQty}.");
                    return Page();
                }
            }

            foreach (var grp in bySoftToy)
            {
                var s = softToys.First(x => x.Id == grp.Id);
                if (s.Quantity < grp.RequiredQty)
                {
                    ModelState.AddModelError(string.Empty, $"Недостаточно на складе: «{s.Name}». Доступно {s.Quantity}, требуется {grp.RequiredQty}.");
                    return Page();
                }
            }

            foreach (var grp in byBouquet)
            {
                var b = bouquets.First(x => x.Id == grp.Id);
                b.Quantity -= grp.RequiredQty;
                if (b.Quantity < 0) b.Quantity = 0;
            }

            foreach (var grp in bySoftToy)
            {
                var s = softToys.First(x => x.Id == grp.Id);
                s.Quantity -= grp.RequiredQty;
                if (s.Quantity < 0) s.Quantity = 0;
            }

            var total = cart.Items.Sum(i => i.Quantity * i.PriceSnapshot);

            user.Phone = Phone;
            var deliveryUtc = DateTime.SpecifyKind(DeliveryDate, DateTimeKind.Utc);

            var order = new OrderEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PickupDate = deliveryUtc,
                DeliveryAddress = DeliveryAddress,
                TotalAmount = total,
                Items = [.. cart.Items.Select(i => new OrderItemEntity
                {
                    Id = Guid.NewGuid(),
                    BouquetId = i.BouquetId,
                    SoftToyId = i.SoftToyId,
                    Quantity = i.Quantity,
                    Price = i.PriceSnapshot
                })]
            };

            _context.Orders.Add(order);
            _context.RemoveRange(cart.Items);
            _context.Carts.Remove(cart);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return RedirectToPage("/Home");
        }

    }
}