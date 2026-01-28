using FlowerShop.Data;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Web.Pages.PageHeader
{
    public class SoftToysModel(FlowerDbContext context) : PageModel
    {
        public List<GetSoftToyDto> GetSoftToys { get; set; }

        private readonly FlowerDbContext _context = context;

        public async Task OnGetAsync()
        {
            GetSoftToys = await _context.SoftToys
                .Select(s => new GetSoftToyDto(
                    s.Id,
                    s.Name,
                    s.Description,
                    s.Quantity,
                    s.Price,
                    s.ImagePath,
                    s.Rating))
                .ToListAsync();
        }
    }
}
