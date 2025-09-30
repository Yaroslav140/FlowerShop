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
                .Include(ci => ci.Bouquet)
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
    }
}
