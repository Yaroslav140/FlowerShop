using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FlowerShop.Web.Pages.Account
{
    public class ProfileModel(FlowerDbContext context) : PageModel
    {
        private readonly FlowerDbContext _context = context;

        public bool IsEditing { get; set; } = false;

        [BindProperty]
        public UpdateProfileInputModel EditInput { get; set; } = new();

        public string Username { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime DateRegister { get; set; }
        public int CountOrderCompleted { get; set; } = 0;

        public List<GetOrderDto> Orders { get; set; } = [];
        public List<GetFeedbackDto> Feedbacks { get; set; } = [];

        private async Task LoadUserDataAsync(Guid userId)
        {
            var user = await _context.UserDomains.FindAsync(userId);
            if (user != null)
            {
                Username = string.IsNullOrWhiteSpace(user.Name) ? user.Login : user.Name;
                Login = user.Login;
                Phone = user.Phone;
                DateRegister = user.DateRegistration;
            }

            Orders = await _context.Orders
                .Where(o => o.UserId == userId)
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
                )).ToListAsync();

            CountOrderCompleted = Orders.Count(c => c.Status == OrderStatus.Completed);

            Feedbacks = new List<GetFeedbackDto>
            {
                new(Guid.NewGuid(), Guid.Parse("d4f8c4ce-9498-4bf4-a0ed-3f03efc4ce6b"), DateTime.UtcNow, "Все отлично!", 5, new()),
                new(Guid.NewGuid(), Guid.Parse("d4f8c4ce-9498-4bf4-a0ed-3f03efc4ce6b"), DateTime.UtcNow, "Пример отзыва", 4, new())
            };
        }

        public async Task OnGetAsync()
        {
            IsEditing = false;

            if (User.Identity?.IsAuthenticated ?? false)
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdStr, out var userId))
                {
                    await LoadUserDataAsync(userId);
                }
            }
        }

        public async Task<IActionResult> OnPostStartEditAsync()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

            await LoadUserDataAsync(userId);

            EditInput = new UpdateProfileInputModel
            {
                NewUsername = Username, 
                NewLogin = Login
            };

            IsEditing = true;
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateProfileAsync()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

            if (!ModelState.IsValid)
            {
                await LoadUserDataAsync(userId);
                IsEditing = true;
                return Page();
            }

            var user = await _context.UserDomains.FindAsync(userId);
            if (user == null) return NotFound();

            user.Name = EditInput.NewUsername;
            user.Login = EditInput.NewLogin;

            await _context.SaveChangesAsync();

            return RedirectToPage();
        }

        public async Task<ActionResult> OnPostDeleteAsync()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

            var user = await _context.UserDomains.FindAsync(userId);
            if (user == null) return NotFound();

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

    public class UpdateProfileInputModel
    {
        [Required(ErrorMessage = "Имя обязательно")]
        [Display(Name = "Имя пользователя")]
        public string NewUsername { get; set; } = string.Empty;

        [Required(ErrorMessage = "Логин обязателен")]
        [Display(Name = "Логин")]
        public string NewLogin { get; set; } = string.Empty;
    }
}
