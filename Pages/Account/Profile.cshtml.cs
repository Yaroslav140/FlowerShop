using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Web.Pages.Account
{
    public class ProfileModel(FlowerDbContext context) : PageModel
    {
        private readonly FlowerDbContext _context = context;

        public string Username {  get; set; } = string.Empty;
        public string Login {  get; set; } = string.Empty;
        public string Phone {  get; set; } = string.Empty;
        public DateTime DateRegister {  get; set; }
        public int CountOrderCompleted { get; set; } = 0;

        public List<GetOrderDto> Orders { get; set; } = [];
        public List<GetFeedbackDto> Feedbacks { get; set; } = new()
        {
            new GetFeedbackDto(Guid.NewGuid(), Guid.Parse("d4f8c4ce-9498-4bf4-a0ed-3f03efc4ce6b"), DateTime.UtcNow, "Все гуд", 5, new()
            {

            }),
            new GetFeedbackDto(Guid.NewGuid(), Guid.Parse("d4f8c4ce-9498-4bf4-a0ed-3f03efc4ce6b"), DateTime.UtcNow, "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book. It has survived not only five centuries, but also the leap into electronic typesetting, remaining essentially unchanged. It was popularised in the 1960s with the release of Letraset sheets containing Lorem Ipsum passages, and more recently with desktop publishing software like Aldus PageMaker including versions of Lorem Ipsum", 5, new()
            {

            })
        };


        public async Task OnGetAsync()
        {
            if (User.Identity?.IsAuthenticated ?? false)
            {
                var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty);
                var user = _context.UserDomains.Find(userId);
                if (user != null)
                {
                    Username = string.IsNullOrWhiteSpace(user.Name) ? user.Login : user.Name;
                    Login = user.Login;
                    Phone = user.Phone;
                    DateRegister = user.DateRegistration;
                }
                Orders = await _context.Orders.Where(o => o.UserId == userId).Select(o => new GetOrderDto(
                    o.Id,
                    o.User.Name,
                    o.User.Login,
                    o.PickupDate,
                    o.DeliveryAddress,
                    o.TotalAmount,
                    o.Status,
                    o.Items.Select(oi => new GetOrderItemDto(oi.Id, oi.BouquetId, oi.Quantity, oi.Price,
                        new GetBouquetDto(
                            oi.Bouquet.Id,  
                            oi.Bouquet.Name,
                            oi.Bouquet.Description,
                            oi.Bouquet.Price,
                            oi.Bouquet.Quantity,
                            oi.Bouquet.ImageUrl))).ToList().ToList())).ToListAsync();
                CountOrderCompleted = Orders
                    .Where(c => c.Status == OrderStatus.Completed)
                    .Count();
            }
        }

        public async Task<ActionResult> OnPostDeleteAsync()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var user = await _context.UserDomains.FindAsync(userId);
            if (user == null)
                return NotFound();

            await using var tx = await _context.Database.BeginTransactionAsync();

            var returns = await _context.OrderItems
                .Where(i => i.Order.UserId == userId)
                .GroupBy(i => i.BouquetId)
                .Select(g => new { BouquetId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToListAsync();

            if (returns.Count > 0)
            {
                var bouquetIds = returns.Select(r => r.BouquetId).ToList();
                var bouquets = await _context.Set<BouquetEntity>()
                    .Where(b => bouquetIds.Contains(b.Id))
                    .ToListAsync();

                var map = returns.ToDictionary(r => r.BouquetId, r => r.Qty);
                foreach (var b in bouquets)
                {
                    if (map.TryGetValue(b.Id, out var qty))
                        b.Quantity += qty;
                }
            }

            _context.UserDomains.Remove(user);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            foreach (var cookie in Request.Cookies.Keys)
                Response.Cookies.Delete(cookie);

            return RedirectToPage("/Home");
        }

        public async Task<ActionResult> OnPostExitAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Home");
        }
    }
}