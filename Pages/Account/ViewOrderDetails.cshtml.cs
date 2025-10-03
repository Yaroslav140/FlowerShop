using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FlowerShop.Web.Pages.Account
{
    public class ViewOrderDetailsModel(FlowerDbContext context) : PageModel
    {
        private readonly FlowerDbContext _context = context;

        [BindProperty(SupportsGet = true)]
        public Guid? Id { get; set; }

        public GetOrderDto? Order { get; private set; }
        public GetUserDto UserInfo { get; private set; }

        public async Task<ActionResult> OnGetAsync()
        {
            if (Id is null || Id == Guid.Empty)
                return BadRequest("Не передан id заказа.");

            Order = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == Id)
                .Select(o => new GetOrderDto(
                    o.Id,
                    o.UserId,
                    o.PickupDate,
                    o.TotalAmount,
                    o.Status,
                    o.Items.Select(oi => new GetOrderItemDto(
                        oi.Id,
                        oi.BouquetId,
                        oi.Quantity,
                        oi.Price,
                        new GetBouquetDto(
                            oi.Bouquet.Id,
                            oi.Bouquet.Name,
                            oi.Bouquet.Description,
                            oi.Bouquet.Price,
                            oi.Bouquet.Quantity,
                            oi.Bouquet.ImageUrl
                        )
                    )).ToList()
                ))
                .SingleOrDefaultAsync();
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty);
            UserInfo = await _context.UserDomains.Where(u => u.Id == userId).Select(u => new GetUserDto(u.Id, u.Name, u.Login, new List<GetOrderDto>())).FirstOrDefaultAsync();
            if (Order is null)
                return NotFound($"Заказ {Id} не найден.");

            return Page();
        }

        public async Task<IActionResult> OnPostCancelOrderAsync(Guid id)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            var order = await _context.Orders
                .Include(o => o.Items).ThenInclude(i => i.Bouquet)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            if (order.Status is OrderStatus.Completed or OrderStatus.Cancelled)
            {
                TempData["ErrorMessage"] = "Заказ нельзя отменить.";
                return RedirectToPage("/Account/ViewOrderDetails", new { id });
            }

            // вернуть остатки
            foreach (var item in order.Items)
            {
                item.Bouquet.Quantity += item.Quantity;
                _context.Bouquets.Update(item.Bouquet);
            }

            order.Status = OrderStatus.Cancelled;

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["SuccessMessage"] = "Заказ отменён. Товары вернулись на склад.";
            return RedirectToPage("/Account/ViewOrderDetails", new { id });
        }


    }
}