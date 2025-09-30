using FlowerShop.Data;
using FlowerShop.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Web.Pages.Account
{
    public class PurchasesModel : PageModel
    {
        private readonly FlowerDbContext _context;
        public List<CartItemEntity> CartEntities { get; set; }
        public PurchasesModel(FlowerDbContext context) => _context = context;
        public async Task OnGetAsync()
        {
            CartEntities = await _context.CartItems
                .AsNoTracking()
                .Include(ci => ci.Bouquet)
                .ToListAsync();
        }
    }
}
