using FlowerShop.Data;
using FlowerShop.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FlowerShop.Web.Pages.Account
{
    public class PlaceOrderModel(FlowerDbContext context) : PageModel
    {
        private readonly FlowerDbContext _context = context;
        [Required, Phone]
        public string Phone { get; set; } = string.Empty;
        [BindProperty, DataType(DataType.Date)]
        public DateTime DeliveryDate { get; set; } = DateTime.Now;


        public async Task<IActionResult> OnPostSubmitOrderAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim))
                return Unauthorized();

            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            var minDate = DateTime.Today.AddDays(1);
            if (DeliveryDate.Date < minDate)
            {
                ModelState.AddModelError(nameof(DeliveryDate),
                    $"Дата доставки не может быть раньше {minDate:dd.MM.yyyy}");
                return Page(); 
            }

            if (cart is null || cart.Items is null || cart.Items.Count == 0)
                return NotFound();
            var pickupUtc = DateTime.SpecifyKind(DeliveryDate, DateTimeKind.Utc);
            var order = new OrderEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PickupDate = pickupUtc,
                TotalAmount = cart.Items.Sum(i => i.Quantity * i.PriceSnapshot),
                Items = []
            };

            _context.Orders.Add(order);
            _context.Carts.Remove(cart);
            await _context.SaveChangesAsync();

            return RedirectToPage("/Home");
        }
    }
}
