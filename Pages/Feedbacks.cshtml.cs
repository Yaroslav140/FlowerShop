using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace FlowerShop.Web.Pages
{
    public class FeedbacksModel(FlowerDbContext context) : PageModel
    {
        private readonly FlowerDbContext _context = context;

        [BindProperty]
        [Display(Name = "Имя пользователя")]
        [Required(ErrorMessage = "Введите имя пользователя")]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        [Display(Name = "Общий комментарий (опционально)")]
        public string TextFeedback { get; set; } = string.Empty;

        [BindProperty]
        [Display(Name = "Выберите заказ")]
        public Guid? SelectedOrderId { get; set; }

        [BindProperty]
        public int StoreRating { get; set; } = 5;

        [BindProperty]
        public string ReviewItemsJson { get; set; } = "[]";

        public Guid? UserId { get; set; }

        public List<SelectListItem> AvailableOrders { get; set; } = [];

        public List<GetFeedbackDto> Feedbacks { get; set; } = [];

        public string? FormError { get; set; }

        public async Task OnGetAsync()
        {
            await LoadOrdersAsync();
        }

        public async Task<IActionResult> OnGetOrderItemsAsync(Guid orderId)
        {
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
                return new JsonResult(new List<object>());

            var order = await _context.Orders
                .Where(o => o.Id == orderId && o.UserId == userId)
                .Include(o => o.Items).ThenInclude(i => i.Bouquet)
                .Include(o => o.Items).ThenInclude(i => i.SoftToy)
                .FirstOrDefaultAsync();

            if (order == null)
                return new JsonResult(new List<object>());

            var items = order.Items.Select(i => new
            {
                bouquetId = i.BouquetId,
                softToyId = i.SoftToyId,
                name = i.Bouquet != null ? i.Bouquet.Name : (i.SoftToy != null ? i.SoftToy.Name : "Неизвестный товар"),
                price = i.Price,
                image = i.ImageUrlSnapshot ?? (i.Bouquet != null ? i.Bouquet.ImagePath : (i.SoftToy != null ? i.SoftToy.ImagePath : null)),
                type = i.BouquetId.HasValue ? "Букет" : "Игрушка"
            });

            return new JsonResult(items);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                FormError = "Пожалуйста, заполните все обязательные поля.";
                await LoadOrdersAsync();
                return Page();
            }

            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                await LoadOrdersAsync();
                return Page();
            }

            // Проверка дублирующегося отзыва для заказа
            if (SelectedOrderId.HasValue)
            {
                var alreadyReviewed = await _context.Feedbacks
                    .AnyAsync(f => f.OrderId == SelectedOrderId && f.UserId == userId);

                if (alreadyReviewed)
                {
                    FormError = "Вы уже оставляли отзыв на этот заказ.";
                    await LoadOrdersAsync();
                    return Page();
                }
            }

            var feedback = new FeedbackEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrderId = SelectedOrderId,
                Description = TextFeedback ?? string.Empty,
                StoreRating = StoreRating,
                DateCreation = DateTime.UtcNow,
                FeedbackItems = []
            };

            if (!string.IsNullOrWhiteSpace(ReviewItemsJson) && ReviewItemsJson != "[]")
            {
                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var reviewItems = JsonSerializer.Deserialize<List<ReviewItemSubmitDto>>(ReviewItemsJson, options);
                    if (reviewItems != null)
                    {
                        foreach (var item in reviewItems)
                        {
                            feedback.FeedbackItems.Add(new FeedbackItemEntity
                            {
                                Id = Guid.NewGuid(),
                                FeedbackId = feedback.Id,
                                ProductRating = Math.Clamp(item.Rating, 1, 5),
                                Comment = item.Comment ?? string.Empty,
                                BouquetId = item.BouquetId,
                                SoftToyId = item.SoftToyId
                            });
                        }
                    }
                }
                catch { }
            }

            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            // Обновляем рейтинги товаров на основе всех отзывов
            await UpdateProductRatingsAsync(feedback.FeedbackItems);

            return RedirectToPage("/Feedbacks");
        }

        private async Task UpdateProductRatingsAsync(List<FeedbackItemEntity> newItems)
        {
            var bouquetIds = newItems
                .Where(fi => fi.BouquetId.HasValue)
                .Select(fi => fi.BouquetId!.Value)
                .Distinct()
                .ToList();

            foreach (var bouquetId in bouquetIds)
            {
                var avgRating = await _context.FeedbackItems
                    .Where(fi => fi.BouquetId == bouquetId)
                    .AverageAsync(fi => (double)fi.ProductRating);

                var bouquet = await _context.Bouquets.FindAsync(bouquetId);
                if (bouquet != null)
                {
                    bouquet.Rating = (float)Math.Round(avgRating, 1);
                    _context.Bouquets.Update(bouquet);
                }
            }

            var softToyIds = newItems
                .Where(fi => fi.SoftToyId.HasValue)
                .Select(fi => fi.SoftToyId!.Value)
                .Distinct()
                .ToList();

            foreach (var softToyId in softToyIds)
            {
                var avgRating = await _context.FeedbackItems
                    .Where(fi => fi.SoftToyId == softToyId)
                    .AverageAsync(fi => (double)fi.ProductRating);

                var toy = await _context.SoftToys.FindAsync(softToyId);
                if (toy != null)
                {
                    toy.Rating = (float)Math.Round(avgRating, 1);
                    _context.SoftToys.Update(toy);
                }
            }

            if (bouquetIds.Count > 0 || softToyIds.Count > 0)
                await _context.SaveChangesAsync();
        }

        private async Task LoadOrdersAsync()
        {
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            Feedbacks = await _context.Feedbacks
                .Select(f => new GetFeedbackDto(
                    f.Id,
                    f.UserId,
                    f.User.Name,
                    f.DateCreation,
                    f.Description,
                    f.StoreRating,
                    f.FeedbackItems.Select(fi => new GetFeedbackItemDto(
                        fi.Id,
                        fi.ProductRating,
                        fi.Bouquet != null ? fi.Bouquet.Name : (fi.SoftToy != null ? fi.SoftToy.Name : "Неизвестный товар"),
                        fi.Bouquet != null ? fi.Bouquet.ImagePath : (fi.SoftToy != null ? fi.SoftToy.ImagePath : null),
                        fi.Comment
                    )).ToList()
                ))
                .ToListAsync();

            if (Guid.TryParse(userIdString, out Guid parsedId))
            {
                UserId = parsedId;

                // Заказы, на которые уже оставлен отзыв
                var reviewedOrderIds = await _context.Feedbacks
                    .Where(f => f.UserId == parsedId && f.OrderId.HasValue)
                    .Select(f => f.OrderId!.Value)
                    .ToListAsync();

                var ordersData = await _context.Orders
                    .Where(o => o.UserId == parsedId
                             && o.Status == OrderStatus.Completed
                             && !reviewedOrderIds.Contains(o.Id))
                    .Select(o => new
                    {
                        o.Id,
                        o.PickupDate,
                        TotalQuantity = o.Items.Sum(i => i.Quantity)
                    })
                    .OrderByDescending(o => o.PickupDate)
                    .ToListAsync();

                AvailableOrders = [.. ordersData.Select(o => new SelectListItem
                {
                    Value = o.Id.ToString(),
                    Text = $"Заказ #{o.Id.ToString()[^4..].ToUpper()} от {o.PickupDate:dd.MM.yyyy} ({o.TotalQuantity} товаров)"
                })];
            }
        }

        private record ReviewItemSubmitDto(Guid? BouquetId, Guid? SoftToyId, int Rating, string? Comment);
    }
}
