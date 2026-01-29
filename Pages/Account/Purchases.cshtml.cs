using FlowerShop.Data;
using FlowerShop.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Web.Pages.Account
{
    public class PurchasesModel(FlowerDbContext context) : PageModel
    {
        private readonly FlowerDbContext _context = context;
        public List<CartItemEntity> CartEntities { get; set; } = [];

        public async Task OnGetAsync()
        {
            CartEntities = await _context.CartItems
                .AsNoTracking()
                .Include(c => c.Bouquet)
                .Include(c => c.SoftToy)
                .ToListAsync();
        }

        public async Task<ActionResult> OnPostClearCartsAsync()
        {
            var allCarts = await _context.Carts
                .Include(c => c.Items)
                .ToListAsync();
            _context.Carts.RemoveRange(allCarts);
            await _context.SaveChangesAsync();
            return RedirectToPage();
        }

        public async Task<ActionResult> OnPostUpdateQuantityAsync(Guid cartId, string direction)
        {
            var cartItem = await _context.CartItems
                .Include(ci => ci.Bouquet)
                .Include(ci => ci.SoftToy)
                .FirstOrDefaultAsync(ci => ci.Id == cartId);

            if (cartItem is null) return NotFound();

            var stock = cartItem.Bouquet != null ? cartItem.Bouquet.Quantity : cartItem.SoftToy.Quantity ;

            var totalInCart = await _context.CartItems
                .Where(ci => (ci.BouquetId == cartItem.BouquetId || ci.SoftToyId == cartItem.SoftToyId) && ci.CartId == cartItem.CartId)
                .SumAsync(ci => (int?)ci.Quantity) ?? 0;

            if (direction == "increase")
            {
                if (stock <= 0)
                {
                    ModelState.AddModelError(string.Empty, "Нет на складе");
                    return RedirectToPage();
                }

                if (totalInCart + 1 > stock)
                {
                    ModelState.AddModelError(string.Empty, "На складе недостаточно данного букета");
                    await OnGetAsync();
                    return Page();
                }

                cartItem.Quantity += 1;
            }
            else if (direction == "decrease" && cartItem.Quantity > 1)
            {
                cartItem.Quantity -= 1;
            }

            await _context.SaveChangesAsync();
            return RedirectToPage();
        }

        public async Task<ActionResult> OnPostRemoveFromCartAsync(Guid cartId)
        {
            var cart = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Id == cartId);
            if (cart is null) return NotFound();

            _context.CartItems.Remove(cart);
            await _context.SaveChangesAsync();
            return RedirectToPage();
        }

    }
}
