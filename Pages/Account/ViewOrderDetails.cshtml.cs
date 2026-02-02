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

        [BindProperty]
        public string? NewDeliveryAddress { get; set; }

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
                    o.User.Login,
                    o.PickupDate,
                    o.DeliveryAddress, // Это текущий адрес из БД
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

            if (Order is null)
                return NotFound($"Заказ {Id} не найден.");

            // Если мы включили режим редактирования, нужно предзаполнить поле ввода текущим адресом
            if (IsEditingAddress)
            {
                NewDeliveryAddress = Order.DeliveryAddress;
            }

            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty);
            UserInfo = await _context.UserDomains
                .Where(u => u.Id == userId)
                .Select(u => new GetUserDto(u.Id, u.Name, u.Login, u.Phone, u.CodeOrder, new List<GetOrderDto>()))
                .FirstOrDefaultAsync()!;

            return Page();
        }

        // --- НОВЫЙ МЕТОД: Обновление адреса ---
        public async Task<IActionResult> OnPostUpdateAddressAsync()
        {
            if (Id is null || string.IsNullOrWhiteSpace(NewDeliveryAddress))
            {
                // Если адрес пустой, возвращаемся в режим редактирования, чтобы показать ошибку (можно добавить ModelState)
                return RedirectToPage(new { id = Id, editAddress = true });
            }

            // Нам нужно получить саму сущность (Entity) из БД, чтобы обновить её.
            // Используем FindAsync или FirstOrDefaultAsync, но БЕЗ проекции в DTO.
            var orderEntity = await _context.Orders.FirstOrDefaultAsync(o => o.Id == Id);

            if (orderEntity == null) return NotFound();

            // Проверка: можно ли менять адрес у завершенного заказа?
            // (Это пример бизнес-логики, можно убрать, если не нужно)
            if (orderEntity.Status == OrderStatus.Completed)
            {
                return Forbid(); // Или просто Redirect с сообщением об ошибке
            }

            // Обновляем поле
            orderEntity.DeliveryAddress = NewDeliveryAddress;

            // Сохраняем в БД
            await _context.SaveChangesAsync();

            // Перенаправляем обратно на страницу просмотра (сбрасываем editAddress в false)
            // fragment: "deliveryInfo" нужен, чтобы страница прокрутилась к карточке доставки
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCancelOrderAsync(Guid id)
        {
            // Твой старый код без изменений
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
