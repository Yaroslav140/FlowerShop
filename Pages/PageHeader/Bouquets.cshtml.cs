using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

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
                return RedirectToPage("/Account/Login");

            if (qty <= 0)
                return BadRequest("Количество должно быть положительным числом.");

            var userName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName))
                return NotFound("Не удалось определить пользователя.");

            var userId = await _context.UserDomains
                .Where(u => u.Name == userName)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync();

            if (userId is null)
                return NotFound("Пользователь не найден.");

            var cartId = await _context.Carts
                .Where(c => c.UserId == userId.Value)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync();

            if (cartId is null)
            {
                var newCart = new CartEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId.Value
                };
                _context.Carts.Add(newCart);
                await _context.SaveChangesAsync();
                cartId = newCart.Id;
            }

            var bouquet = await _context.Bouquets
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == bouquetId);

            if (bouquet is null)
                return NotFound("Такого букета нет на сайте.");

            if (bouquet.Quantity <= 0)
                return BadRequest("Букет закончился.");

            var item = await _context.CartItems
                .FirstOrDefaultAsync(i => i.CartId == cartId.Value && i.BouquetId == bouquetId);

            var newQty = (item?.Quantity ?? 0) + qty;
            if (newQty > bouquet.Quantity)
            {
                return RedirectToPage("/Account/Purchases");
            }

            if (item is null)
            {
                var newItem = new CartItemEntity
                {
                    Id = Guid.NewGuid(),
                    CartId = cartId.Value,
                    BouquetId = bouquetId,
                    Quantity = qty,
                    PriceSnapshot = bouquet.Price
                };
                _context.CartItems.Add(newItem);
            }
            else
            {
                item.Quantity = newQty;
                item.PriceSnapshot = bouquet.Price;
                _context.CartItems.Update(item);
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
                item = await _context.CartItems.FirstOrDefaultAsync(i => i.CartId == cartId.Value && i.BouquetId == bouquetId);
                var qtyAfterReload = (item?.Quantity ?? 0) + qty;
                if (qtyAfterReload > bouquet.Quantity)
                    return BadRequest("Кто-то уже забрал часть товара. Попробуйте меньшее количество.");

                if (item is null)
                {
                    _context.CartItems.Add(new CartItemEntity
                    {
                        Id = Guid.NewGuid(),
                        CartId = cartId.Value,
                        BouquetId = bouquetId,
                        Quantity = qty,
                        PriceSnapshot = bouquet.Price
                    });
                }
                else
                {
                    item.Quantity = qtyAfterReload;
                    item.PriceSnapshot = bouquet.Price;
                    _context.CartItems.Update(item);
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToPage("/PageHeader/Bouquets");
        }


        private async Task LoadBouquetsAsync()
        {
            GetBouquets = await _context.Bouquets
                .AsNoTracking()
                .Select(b => new GetBouquetDto(
                    b.Id, b.Name, b.Description, b.Price, b.Quantity, b.ImageUrl,
                    b.FlowerLinks.Select(fl => new GetBouquetFlowerDto(
                        fl.BouquetId, fl.FlowerId, fl.Quantity)).ToList()))
                .ToListAsync();
        }
    }
}
