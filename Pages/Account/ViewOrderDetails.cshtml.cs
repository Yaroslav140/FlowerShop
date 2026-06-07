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

        public bool IsEditingAddress { get; set; }
        public bool IsAddProduct { get; set; }

        public List<GetBouquetDto> Bouquets { get; private set; } = new();
        public List<GetSoftToyDto> SoftToys { get; private set; } = new();

        [BindProperty]
        public string? NewDeliveryAddress { get; set; }

        [BindProperty]
        public Guid? SelectedProductId { get; set; }

        [BindProperty]
        public string? ProductType { get; set; }

        public async Task<ActionResult> OnGetAsync(bool editAddress = false)
        {
            if (Id is null || Id == Guid.Empty)
                return BadRequest("Не передан id заказа.");

            IsEditingAddress = editAddress;

            Order = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == Id)
                .Select(o => new GetOrderDto(
                    o.Id,
                    o.User.Name,
                    o.User.Email,
                    o.PickupDate,
                    o.DeliveryAddress,
                    o.TotalAmount,
                    o.Status,
                    o.CanReview,
                    o.Items.Select(oi => new GetOrderItemDto(
                        oi.Id,
                        oi.BouquetId,
                        oi.SoftToyId,
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

            if (Order is null)
                return NotFound($"Заказ {Id} не найден.");

            if (IsEditingAddress)
                NewDeliveryAddress = Order.DeliveryAddress;

            if (Order.Status != OrderStatus.Cancelled && Order.Status != OrderStatus.Completed)
            {
                Bouquets = await _context.Bouquets
                    .Where(b => b.Quantity > 0)
                    .OrderBy(b => b.Name)
                    .Select(b => new GetBouquetDto(b.Id, b.Name, b.Description, b.Price, b.Quantity, b.ImagePath, b.Rating))
                    .ToListAsync();

                SoftToys = await _context.SoftToys
                    .Where(s => s.Quantity > 0)
                    .OrderBy(s => s.Name)
                    .Select(s => new GetSoftToyDto(s.Id, s.Name, s.Description, s.Quantity, s.Price, s.ImagePath, s.Rating))
                    .ToListAsync();
            }

            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty);
            UserInfo = await _context.UserDomains
                .Where(u => u.Id == userId)
                .Select(u => new GetUserDto(u.Id, u.Name, u.Email, u.Phone, u.CodeOrder, new List<GetOrderDto>()))
                .FirstOrDefaultAsync()!;

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAddressAsync()
        {
            if (Id is null || string.IsNullOrWhiteSpace(NewDeliveryAddress))
                return RedirectToPage(new { id = Id, editAddress = true });

            var orderEntity = await _context.Orders.FirstOrDefaultAsync(o => o.Id == Id);
            if (orderEntity == null) return NotFound();

            if (orderEntity.Status == OrderStatus.Completed || orderEntity.Status == OrderStatus.Cancelled)
                return Forbid();

            orderEntity.DeliveryAddress = NewDeliveryAddress;
            await _context.SaveChangesAsync();

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddProductAsync()
        {
            if (Id is null || SelectedProductId is null || string.IsNullOrWhiteSpace(ProductType))
                return RedirectToPage(new { id = Id });

            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == Id);

            if (order == null) return NotFound();

            if (order.Status is OrderStatus.Completed or OrderStatus.Cancelled)
                return Forbid();

            decimal price = 0;
            Guid? bouquetId = null;
            Guid? softToyId = null;

            if (ProductType == "bouquet")
            {
                var bouquet = await _context.Bouquets.FindAsync(SelectedProductId);
                if (bouquet == null || bouquet.Quantity <= 0)
                    return RedirectToPage(new { id = Id });

                price = bouquet.Price;
                bouquetId = bouquet.Id;
                bouquet.Quantity--;
                _context.Bouquets.Update(bouquet);
            }
            else if (ProductType == "softtoy")
            {
                var toy = await _context.SoftToys.FindAsync(SelectedProductId);
                if (toy == null || toy.Quantity <= 0)
                    return RedirectToPage(new { id = Id });

                price = toy.Price;
                softToyId = toy.Id;
                toy.Quantity--;
                _context.SoftToys.Update(toy);
            }
            else
            {
                return RedirectToPage(new { id = Id });
            }

            var existingItem = order.Items.FirstOrDefault(i =>
                (bouquetId.HasValue && i.BouquetId == bouquetId) ||
                (softToyId.HasValue && i.SoftToyId == softToyId));

            if (existingItem != null)
            {
                existingItem.Quantity++;
                _context.OrderItems.Update(existingItem);
            }
            else
            {
                var item = new OrderItemEntity
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    BouquetId = bouquetId,
                    SoftToyId = softToyId,
                    Quantity = 1,
                    Price = price
                };
                _context.OrderItems.Add(item);
            }

            order.TotalAmount += price;
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id = Id });
        }

        public async Task<IActionResult> OnPostCancelOrderAsync(Guid id)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            var order = await _context.Orders
                .Include(o => o.Items).ThenInclude(i => i.Bouquet)
                .Include(o => o.Items).ThenInclude(s => s.SoftToy)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            if (order.Status is OrderStatus.Completed or OrderStatus.Cancelled)
            {
                ModelState.AddModelError(string.Empty, "Заказ нельзя отменить.");
                return RedirectToPage("/Account/ViewOrderDetails", new { id });
            }

            foreach (var item in order.Items)
            {
                if (item.Bouquet != null)
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
