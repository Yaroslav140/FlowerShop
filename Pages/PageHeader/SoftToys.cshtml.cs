using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FlowerShop.Web.Pages.PageHeader
{
    public class SoftToysModel(FlowerDbContext context) : PageModel
    {
        public List<GetSoftToyDto> GetSoftToys { get; set; }

        private readonly FlowerDbContext _context = context;

        public async Task OnGetAsync()
        {
            GetSoftToys = await _context.SoftToys
                .AsNoTracking()
                .Where(q => q.Quantity > 0)
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

        public async Task<ActionResult> OnPostAddToCartAsync(Guid softToyId, int qty)
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

            var softToy = await _context.SoftToys.AsNoTracking().FirstOrDefaultAsync(b => b.Id == softToyId);
            if (softToy is null) return NotFound("Такой игрушки нет на сайте.");
            if (softToy.Quantity <= 0) return BadRequest("Игршки нет на складе.");

            var item = await _context.CartItems
                .FirstOrDefaultAsync(i => i.CartId == cart.Id && i.SoftToyId == softToyId);

            var newQty = (item?.Quantity ?? 0) + qty;
            if (newQty > softToy.Quantity)
                return RedirectToPage("/Account/Purchases");

            if (item is null)
            {
                _context.CartItems.Add(new CartItemEntity
                {
                    Id = Guid.NewGuid(),
                    CartId = cart.Id,
                    SoftToyId = softToyId,
                    Quantity = qty,
                    PriceSnapshot = softToy.Price
                });
            }
            else
            {
                item.Quantity = newQty;
                item.PriceSnapshot = softToy.Price;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.ChangeTracker.Clear();

                softToy = await _context.SoftToys.AsNoTracking().FirstOrDefaultAsync(b => b.Id == softToyId);
                if (softToy is null || softToy.Quantity <= 0)
                    return BadRequest("Игрушки нет на складе.");

                item = await _context.CartItems.FirstOrDefaultAsync(i => i.CartId == cart.Id && i.SoftToyId == softToyId);
                var qtyAfterReload = (item?.Quantity ?? 0) + qty;
                if (qtyAfterReload > softToy.Quantity)
                    return BadRequest("Кто-то уже забрал часть товара. Попробуйте меньшее количество.");

                if (item is null)
                {
                    _context.CartItems.Add(new CartItemEntity
                    {
                        Id = Guid.NewGuid(),
                        CartId = cart.Id,
                        SoftToyId = softToyId,
                        Quantity = qty,
                        PriceSnapshot = softToy.Price
                    });
                }
                else
                {
                    item.Quantity = qtyAfterReload;
                    item.PriceSnapshot = softToy.Price;
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToPage("/PageHeader/SoftToys");
        }
    }
}
