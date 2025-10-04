using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOGet;
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

        [BindProperty, Required, Phone]
        public string Phone { get; set; } = string.Empty;

        [BindProperty, DataType(DataType.Date)]
        public DateTime DeliveryDate { get; set; } = DateTime.Today.AddDays(1);

        public async Task<ActionResult> OnPostSubmitOrderAsync()
        {
            if (!ModelState.IsValid)
                return Page();

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
                ViewData["ErrorMessage"] = "Нету пользваоетля";
                return Page();
            }
            var minDate = DateTime.Today.AddDays(1);
            if (DeliveryDate.Date < minDate)
            {
                ModelState.AddModelError(nameof(DeliveryDate),
                    $"Дата доставки не может быть раньше {minDate:dd.MM.yyyy}");
                return Page();
            }

            var byBouquet = cart.Items
                .GroupBy(i => i.BouquetId)
                .Select(g => new
                {
                    BouquetId = g.Key,
                    RequiredQty = g.Sum(x => x.Quantity)
                })
                .ToList();

            var bouquetIds = byBouquet.Select(x => x.BouquetId).ToHashSet();

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

            var bouquets = await _context.Bouquets
                .Where(b => bouquetIds.Contains(b.Id))
                .AsTracking()
                .ToListAsync();

            var missing = bouquetIds.Except(bouquets.Select(b => b.Id)).ToList();
            if (missing.Count > 0)
            {
                ModelState.AddModelError(string.Empty, "Некоторые букеты недоступны.");
                return Page();
            }

            foreach (var grp in byBouquet)
            {
                var b = bouquets.First(x => x.Id == grp.BouquetId);
                if (b.Quantity < grp.RequiredQty) 
                {
                    ModelState.AddModelError(string.Empty,
                        $"Недостаточно на складе: «{b.Name}». Доступно {b.Quantity}, требуется {grp.RequiredQty}.");
                    return Page();
                }
            }

            foreach (var grp in byBouquet)
            {
                var b = bouquets.First(x => x.Id == grp.BouquetId);
                b.Quantity -= grp.RequiredQty;
                if (b.Quantity < 0) b.Quantity = 0;
            }

            var total = cart.Items.Sum(i => i.Quantity * i.PriceSnapshot);

            var deliveryUtc = DateTime.SpecifyKind(DeliveryDate.Date, DateTimeKind.Utc);
            user.Phone = Phone;
            var order = new OrderEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PickupDate = deliveryUtc,      
                TotalAmount = total,
                Items = [.. cart.Items.Select(i => new OrderItemEntity
                {
                    Id = Guid.NewGuid(),
                    BouquetId = i.BouquetId,
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