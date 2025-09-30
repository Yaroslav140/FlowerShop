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
                .FirstOrDefaultAsync(ci => ci.Id == cartId);

            if (cartItem is null) return NotFound();

            var stock = cartItem.Bouquet.Quantity;

            var totalInCart = await _context.CartItems
                .Where(ci => ci.BouquetId == cartItem.BouquetId && ci.CartId == cartItem.CartId)
                .SumAsync(ci => (int?)ci.Quantity) ?? 0;

            if (direction == "increase")
            {
                if (stock <= 0)
                    return BadRequest("Нет на складе.");

                if (totalInCart + 1 > stock)
                    return BadRequest("Недостаточно на складе для увеличения количества.");

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
