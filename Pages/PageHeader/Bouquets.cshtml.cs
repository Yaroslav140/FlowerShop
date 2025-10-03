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
        public bool IsQuntity = false;

        private readonly FlowerDbContext _context;
        public BouquetsModel(FlowerDbContext context) => _context = context;

        public async Task OnGetAsync() => await LoadBouquetsAsync();


        public async Task<ActionResult> OnPostAddToCartAsync(Guid bouquetId, int qty)
        {
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return RedirectToPage("/Account/Login");

            if (qty <= 0)
                return BadRequest("Количество должно быть положительным числом.");

            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(idClaim) || !Guid.TryParse(idClaim, out var userId))
                return NotFound("Не удалось определить пользователя.");

            var cart = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == userId);
            if (cart is null)
            {
                cart = new CartEntity { Id = Guid.NewGuid(), UserId = userId };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var bouquet = await _context.Bouquets.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bouquetId);
            if (bouquet is null) return NotFound("Такого букета нет на сайте.");
            if (bouquet.Quantity <= 0) return BadRequest("Букет закончился.");

            var item = await _context.CartItems
                .FirstOrDefaultAsync(i => i.CartId == cart.Id && i.BouquetId == bouquetId);

            var newQty = (item?.Quantity ?? 0) + qty;
            if (newQty > bouquet.Quantity)
                return RedirectToPage("/Account/Purchases");

            if (item is null)
            {
                _context.CartItems.Add(new CartItemEntity
                {
                    Id = Guid.NewGuid(),
                    CartId = cart.Id,
                    BouquetId = bouquetId,
                    Quantity = qty,
                    PriceSnapshot = bouquet.Price
                });
            }
            else
            {
                item.Quantity = newQty;
                item.PriceSnapshot = bouquet.Price;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.ChangeTracker.Clear();

                bouquet = await _context.Bouquets.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bouquetId);
                if (bouquet is null || bouquet.Quantity <= 0)
                    return BadRequest("Букет закончился.");

                item = await _context.CartItems.FirstOrDefaultAsync(i => i.CartId == cart.Id && i.BouquetId == bouquetId);
                var qtyAfterReload = (item?.Quantity ?? 0) + qty;
                if (qtyAfterReload > bouquet.Quantity)
                    return BadRequest("Кто-то уже забрал часть товара. Попробуйте меньшее количество.");

                if (item is null)
                {
                    _context.CartItems.Add(new CartItemEntity
                    {
                        Id = Guid.NewGuid(),
                        CartId = cart.Id,
                        BouquetId = bouquetId,
                        Quantity = qty,
                        PriceSnapshot = bouquet.Price
                    });
                }
                else
                {
                    item.Quantity = qtyAfterReload;
                    item.PriceSnapshot = bouquet.Price;
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToPage("/PageHeader/Bouquets");
        }



        private async Task LoadBouquetsAsync()
        {
            GetBouquets = await _context.Bouquets
                .AsNoTracking()
                .Where(c => c.Quantity > 0)
                .Select(b => new GetBouquetDto(
                    b.Id, b.Name, b.Description, b.Price, b.Quantity, b.ImageUrl)).ToListAsync();
        }
    }
}
