using FlowerShop.Data;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
namespace FlowerShop.Web.Pages
{
    public class BouquetsModel : PageModel
    {
        public List<GetBouquetDto> GetBouquets { get; set; } = new();
        private readonly FlowerDbContext _context;
        public BouquetsModel(FlowerDbContext context) => _context = context;
        public async Task OnGetAsync()
        {
            GetBouquets = await _context.Bouquets
                .AsNoTracking()
                .Select(b => new GetBouquetDto(
                    b.Name,
                    b.Description,
                    b.Price,
                    b.Stock,
                    b.FlowerLinks
                    .Select(fl => new GetBouquetFlowerDto(
                        fl.BouquetId,
                        fl.FlowerId,
                        fl.Quantity))
                    .ToList()))
                .ToListAsync();
        }
    }
}
