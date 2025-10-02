using FlowerShop.Data;
using FlowerShop.Dto.DTOGet;
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
}
