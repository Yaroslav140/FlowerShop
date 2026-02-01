using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;

namespace FlowerShop.Web.Pages.Account
{
    public class ProfileModel(FlowerDbContext context) : PageModel
    {
        private readonly FlowerDbContext _context = context;

        public bool IsEditing { get; set; } = false;
        public bool IsCanReviews { get; set; } = false;

        [BindProperty]
        public UpdateProfileInputModel EditInput { get; set; } = new();
        [BindProperty]
        public ReviewInputModel ReviewInput { get; set; } = new();

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
                    o.Items.Select(i => new GetOrderItemDto(
                        i.Id,
                        i.BouquetId,
                        i.SoftToyId,
                        i.Quantity,
                        i.Price,
                        i.Bouquet != null
                            ? new GetBouquetDto(
                                i.Bouquet.Id,
                                i.Bouquet.Name,
                                i.Bouquet.Description,
                                i.Bouquet.Price,
                                i.Bouquet.Quantity,
                                i.Bouquet.ImagePath,
                                i.Bouquet.Rating)
                            : null,
                        i.SoftToy != null
                            ? new GetSoftToyDto(
                                i.SoftToy.Id,
                                i.SoftToy.Name,
                                i.SoftToy.Description,
                                i.SoftToy.Quantity,
                                i.SoftToy.Price,
                                i.SoftToy.ImagePath,
                                i.SoftToy.Rating)
                            : null)).ToList())).ToListAsync();

            CountOrderCompleted = Orders.Count(c => c.Status == OrderStatus.Completed);
            Feedbacks = await _context.Feedbacks
                .Where(i => i.UserId == user.Id)
                .Select(f => new GetFeedbackDto(
                    f.Id,
                    f.UserId,
                    f.User.Name,
                    f.DateCreation,
                    f.Description,
                    f.StoreRating,
                    f.FeedbackItems.Select(i => new GetFeedbackItemDto(
                        i.Id,
                        i.ProductRating,
                        i.Bouquet.Name ?? i.SoftToy.Name,
                        i.Bouquet.ImagePath ?? i.SoftToy.ImagePath)).ToList())).ToListAsync();
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

        public async Task<IActionResult> OnPostStartReviewsAsync(Guid orderId)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

            await LoadUserDataAsync(userId);

            var order = Orders.FirstOrDefault(o => o.Id == orderId);
            if (order == null) return NotFound();

            if (!order.CanReview)
            {
                ModelState.AddModelError(string.Empty, "Для этого заказа нельзя оставить отзыв");
                return Page();
            }

            ReviewInput = new ReviewInputModel { OrderId = orderId };
            IsCanReviews = true;
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateProfileAsync()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

            ModelState.Remove("ReviewInput.Rating");
            ModelState.Remove("ReviewInput.Comment");

            ModelState.Remove("Rating");
            ModelState.Remove("Comment");

            if (!ModelState.IsValid)
            {
                await LoadUserDataAsync(userId);
                IsEditing = true;
                return Page();
            }

            var user = await _context.UserDomains.FindAsync(userId);
            if (user == null) return NotFound();

            var loginExits = await _context.UserDomains
                .Where(l => l.Login == EditInput.NewLogin)
                .FirstOrDefaultAsync();

            if (loginExits != null)
            {
                ModelState.AddModelError(string.Empty, "Такой логин уже есть.");
                return RedirectToPage("/Account/Profile");
            }

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

        public async Task<IActionResult> OnPostRepeatOrderAsync(Guid orderId)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

            var oldOrder = await _context.Orders
                .Include(o => o.Items).ThenInclude(i => i.Bouquet)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (oldOrder == null)
                return NotFound();

            foreach (var item in oldOrder.Items)
            {
                if (item.Bouquet.Quantity < item.Quantity)
                {
                    ModelState.AddModelError(string.Empty, $"Недостаточно на складе для «{item.Bouquet.Name}». Осталось: {item.Bouquet.Quantity}, требуется: {item.Quantity}");

                    await LoadUserDataAsync(userId);
                    return Page();
                }
            }

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

            return RedirectToPage("/Account/Profile");
        }


        public async Task<IActionResult> OnPostSubmitReviewAsync()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

            ModelState.Remove("EditInput.NewLogin");
            ModelState.Remove("EditInput.NewUsername");

            ModelState.Remove("NewLogin");
            ModelState.Remove("NewUsername");

            if (!ModelState.IsValid)
            {
                await LoadUserDataAsync(userId);
                IsCanReviews = true;
                return Page();
            }

            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == ReviewInput.OrderId && o.UserId == userId);

            if (order == null) return NotFound();

            if (!order.CanReview)
            {
                ModelState.AddModelError(string.Empty, "Для этого заказа уже оставлен отзыв");
                await LoadUserDataAsync(userId);
                IsCanReviews = true;
                return Page();
            }

            var feedback = new FeedbackEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DateCreation = DateTime.UtcNow,
                Description = ReviewInput.Comment,
                StoreRating = ReviewInput.Rating,
                FeedbackItems = [.. order.Items.Select(orderItem => new FeedbackItemEntity
                {
                    Id = Guid.NewGuid(), 
                    ProductRating = ReviewInput.Rating, 
                    BouquetId = orderItem.BouquetId,
                    SoftToyId = orderItem.SoftToyId,

                })]
            };

            _context.Feedbacks.Add(feedback);

            order.CanReview = false;

            await _context.SaveChangesAsync();

            return RedirectToPage("/Account/Profile");
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

    public class ReviewInputModel
    {
        public Guid OrderId { get; set; }

        [Required(ErrorMessage = "Пожалуйста, укажите оценку")]
        [Range(1, 5, ErrorMessage = "Оценка должна быть от 1 до 5")]
        public int Rating { get; set; }

        [Required(ErrorMessage = "Пожалуйста, напишите комментарий")]
        [StringLength(500, ErrorMessage = "Комментарий не может превышать 500 символов")]
        public string Comment { get; set; }
    }
}
