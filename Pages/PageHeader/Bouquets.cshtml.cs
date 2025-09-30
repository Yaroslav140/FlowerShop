using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FlowerShop.Web.Pages.PageHeader
{
    public class BouquetsModel : PageModel
    {
        public List<GetBouquetDto> GetBouquets { get; set; } = new();

        private readonly FlowerDbContext _context;
        public BouquetsModel(FlowerDbContext context) => _context = context;

        public async Task OnGetAsync() => await LoadBouquetsAsync();

        public async Task<IActionResult> OnPostAddToCartAsync(Guid bouquetId, int qty)
        {
            if (!(User?.Identity?.IsAuthenticated ?? false))
            {
                return RedirectToPage("/Account/Register");
            }

            // 2) Логика добавления в корзину (эскиз)
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            // ...найти/создать корзину пользователя, проверить наличие букета, добавить позицию и т.д.

            // 3) PRG: после POST делаем Redirect, чтобы не терять данные модели
            return RedirectToPage(); // вернёт на OnGet и заново загрузит букетов
        }

        private async Task LoadBouquetsAsync()
        {
            GetBouquets = await _context.Bouquets
                .AsNoTracking()
                .Select(b => new GetBouquetDto(
                    b.Id, b.Name, b.Description, b.Price, b.Stock, b.ImageUrl,
                    b.FlowerLinks.Select(fl => new GetBouquetFlowerDto(
                        fl.BouquetId, fl.FlowerId, fl.Quantity)).ToList()))
                .ToListAsync();
        }
    }
}
