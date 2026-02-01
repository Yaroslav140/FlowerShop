using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Web.Pages.Account
{
    public class ViewOrderDetailsModel(FlowerDbContext context) : PageModel
    {
        private readonly FlowerDbContext _context = context;

        [BindProperty(SupportsGet = true)]
        public Guid? Id { get; set; }

        public GetOrderDto? Order { get; private set; }
        public GetUserDto UserInfo { get; private set; } = null!;

        public async Task<ActionResult> OnGetAsync()
        {
            if (Id is null || Id == Guid.Empty)
                return BadRequest("Не передан id заказа.");

            Order = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == Id)
                .Select(o => new GetOrderDto(
                    o.Id,
                    o.User.Name,
                    o.User.Login,
                    o.PickupDate,
                    o.DeliveryAddress,
                    o.TotalAmount,
                    o.Status,
                    o.CanReview,
                    o.Items.Select(oi => new GetOrderItemDto(
                        oi.Id,
                        oi.BouquetId!.Value,
                        oi.SoftToyId!.Value,
                        oi.Quantity,
                        oi.Price,
                        oi.Bouquet != null ? new GetBouquetDto(
                            oi.Bouquet.Id,
                            oi.Bouquet.Name,
                            oi.Bouquet.Description,
                            oi.Bouquet.Price,
                            oi.Bouquet.Quantity,
                            oi.Bouquet.ImagePath,
                            oi.Bouquet.Rating
                        ) : null,
                        oi.SoftToy != null ? new GetSoftToyDto(
                            oi.SoftToy.Id,
                            oi.SoftToy.Name,
                            oi.SoftToy.Description,
                            oi.SoftToy.Quantity,
                            oi.SoftToy.Price,
                            oi.SoftToy.ImagePath,
                            oi.SoftToy.Rating
                        ) : null
                    )).ToList()
                ))
                .SingleOrDefaultAsync();
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty);
            UserInfo = await _context.UserDomains.Where(u => u.Id == userId).Select(u => new GetUserDto(u.Id, u.Name, u.Login, u.Phone, u.CodeOrder, new List<GetOrderDto>())).FirstOrDefaultAsync();
            if (Order is null)
                return NotFound($"Заказ {Id} не найден.");

            return Page();
        }

        public async Task<IActionResult> OnPostCancelOrderAsync(Guid id)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Bouquet)
                .Include(o => o.Items)
                    .ThenInclude(s => s.SoftToy)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            if (order.Status is OrderStatus.Completed or OrderStatus.Cancelled)
            {
                ModelState.AddModelError(string.Empty, "Заказ нельзя отменить.");
                return RedirectToPage("/Account/ViewOrderDetails", new { id });
            }

            foreach (var item in order.Items)
            {
                if(item.Bouquet != null)
                {
                    item.Bouquet.Quantity += item.Quantity;
                    _context.Bouquets.Update(item.Bouquet);
                }
                else
                {
                    item.SoftToy.Quantity += item.Quantity;
                    _context.SoftToys.Update(item.SoftToy);
                }
            }

            order.Status = OrderStatus.Cancelled;

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return RedirectToPage("/Account/ViewOrderDetails", new { id });
        }
    }
}