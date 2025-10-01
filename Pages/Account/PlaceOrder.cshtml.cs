using FlowerShop.Data;
using FlowerShop.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
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

        public async Task<IActionResult> OnPostSubmitOrderAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart is null || cart.Items is null || cart.Items.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Корзина пуста.");
                return Page();
            }

            var minDate = DateTime.Today.AddDays(1);
            if (DeliveryDate.Date < minDate)
            {
                ModelState.AddModelError(nameof(DeliveryDate),
                    $"Дата доставки не может быть раньше {minDate:dd.MM.yyyy}");
                return Page();
            }

            var deliveryUtc = DateTime.SpecifyKind(DeliveryDate.Date, DateTimeKind.Utc);

            var total = cart.Items.Sum(i => i.Quantity * i.PriceSnapshot);

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
                    Price = i.Quantity * i.PriceSnapshot
                })]
            };

            await using var tx = await _context.Database.BeginTransactionAsync();

            _context.Orders.Add(order);

            _context.RemoveRange(cart.Items);
            _context.Carts.Remove(cart);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return RedirectToPage("/Home");
        }
    }
}