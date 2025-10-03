using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FlowerShop.Web.Pages.Account;

public class ViewOrdersModel(FlowerDbContext context) : PageModel
{
    private readonly FlowerDbContext _context = context;

    public List<GetOrderDto> Orders { get; set; } = [];

    public async Task OnGetAsync()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
        {
            Orders = [];
            return;
        }

        Orders = await _context.Orders
            .Where(o => o.UserId == userId)
            .Select(o => new GetOrderDto(
                o.Id,                
                o.UserId,
                o.PickupDate,
                o.TotalAmount,
                o.Status,
                o.Items.Select(i => new GetOrderItemDto(
                    i.Id,
                    i.BouquetId,
                    i.Quantity,
                    i.Price,
                    new GetBouquetDto(
                        i.Bouquet.Id,
                        i.Bouquet.Name,
                        i.Bouquet.Description,
                        i.Bouquet.Price,
                        i.Bouquet.Quantity,
                        i.Bouquet.ImageUrl)
                )).ToList()
            )).ToListAsync();
    }

    public async Task<IActionResult> OnPostRepeatOrderAsync(Guid orderId)
    {
        var oldOrder = await _context.Orders
            .Include(o => o.Items).ThenInclude(i => i.Bouquet)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (oldOrder == null)
            return NotFound();

        foreach (var item in oldOrder.Items)
        {
            if (item.Bouquet.Quantity < item.Quantity)
            {
                TempData["ErrorMessage"] =
                    $"Недостаточно на складе для «{item.Bouquet.Name}». Остаток: {item.Bouquet.Quantity}, требуется: {item.Quantity}";
                return RedirectToPage("/Account/ViewOrderDetails", new { id = orderId });
            }
        }

        // создать новый заказ + списать резерв
        var newOrder = new OrderEntity
        {
            Id = Guid.NewGuid(),
            UserId = oldOrder.UserId,
            PickupDate = DateTime.UtcNow.AddDays(1),
            Status = OrderStatus.New,
            TotalAmount = oldOrder.TotalAmount,
            Items = []
        };

        foreach (var item in oldOrder.Items)
        {
            newOrder.Items.Add(new OrderItemEntity
            {
                Id = Guid.NewGuid(),
                BouquetId = item.BouquetId,
                Quantity = item.Quantity,
                Price = item.Price
            });
            item.Bouquet.Quantity -= item.Quantity;
        }

        _context.Orders.Add(newOrder);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Заказ повторён.";
        return RedirectToPage("/Account/ViewOrderDetails", new { id = newOrder.Id });
    }


}
