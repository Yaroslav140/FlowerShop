using FlowerShop.Data;
using FlowerShop.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

namespace FlowerShop.Web.Pages.Account
{
    public class PlaceOrderModel(FlowerDbContext context, IConfiguration configuration, IHttpClientFactory httpClientFactory) : PageModel
    {
        private readonly FlowerDbContext _context = context;
        private readonly IHttpClientFactory _http = httpClientFactory;

        public string YandexApiKey { get; private set; } = configuration["YandexMaps:ApiKey"] ?? string.Empty;

        [BindProperty, Required(ErrorMessage = "Введите номер телефона"), Phone(ErrorMessage = "Некорректный формат телефона")]
        public string Phone { get; set; } = string.Empty;

        [BindProperty, DataType(DataType.DateTime), Required(ErrorMessage = "Время должно быть на 2 часа больше текущего")]
        public DateTime DeliveryDate { get; set; } = DateTime.Now;

        [BindProperty, Required(ErrorMessage = "Укажите адрес доставки"),
         StringLength(500, MinimumLength = 10, ErrorMessage = "Адрес слишком короткий — выберите точку на карте или введите полный адрес")]
        public string DeliveryAddress { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; } = 0;

        public async Task<ActionResult> OnGetAsync()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var user = await _context.UserDomains.FindAsync(userId);
            if (!string.IsNullOrWhiteSpace(user?.Phone))
                Phone = user.Phone;

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart is null)
                return Unauthorized();

            TotalAmount = cart.Items.Sum(i => i.Quantity * i.PriceSnapshot);
            return Page();
        }

        public async Task<IActionResult> OnGetGeocodeAsync(double lat, double lon)
        {
            var apiKey = YandexApiKey;
            var url = $"https://geocode-maps.yandex.ru/1.x/?apikey={apiKey}" +
                      $"&geocode={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                      $"{lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                      "&format=json&results=1&lang=ru_RU";

            try
            {
                var client = _http.CreateClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("FlowerShop/1.0");
                var json = await client.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                var members = doc.RootElement
                    .GetProperty("response")
                    .GetProperty("GeoObjectCollection")
                    .GetProperty("featureMember");

                if (members.GetArrayLength() == 0)
                    return new JsonResult(new { address = (string?)null });

                var address = members[0]
                    .GetProperty("GeoObject")
                    .GetProperty("metaDataProperty")
                    .GetProperty("GeocoderMetaData")
                    .GetProperty("text")
                    .GetString();

                return new JsonResult(new { address });
            }
            catch
            {
                return new JsonResult(new { address = (string?)null });
            }
        }

        public async Task<ActionResult> OnPostSubmitOrderAsync()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);
            TotalAmount = cart?.Items.Sum(i => i.Quantity * i.PriceSnapshot) ?? 0;

            if (!ModelState.IsValid)
            {
                ModelState.AddModelError(string.Empty, "Данные некорректны.");
                return Page();
            }

            // Проверка зоны доставки — адрес должен содержать "Клетня" или координаты (fallback)
            var isKletnya = System.Text.RegularExpressions.Regex.IsMatch(
                DeliveryAddress, @"клетн|\d{2}\.\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!isKletnya)
            {
                ModelState.AddModelError(string.Empty, "Доставляем только по посёлку Клетня и окрестностям. Выберите точку на карте внутри зелёной зоны.");
                return Page();
            }

            // Защита от сырых координат вместо адреса (формат: "55.12345, 37.12345")
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    DeliveryAddress.Trim(),
                    @"^\-?\d{1,3}\.\d+,\s*\-?\d{1,3}\.\d+$"))
            {
                ModelState.AddModelError(string.Empty, "Укажите текстовый адрес — выберите точку на карте или введите улицу в поле.");
                return Page();
            }

            var user = await _context.UserDomains.FindAsync(userId);

            if (cart is null || cart.Items is null || cart.Items.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Корзина пуста.");
                return Page();
            }

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Пользователь не найден.");
                return Page();
            }

            if (string.IsNullOrWhiteSpace(user.CodeOrder))
            {
                string code;
                do
                {
                    code = GeneratedCode.Generated.GenerateRandomCode();
                } while (await _context.UserDomains.AnyAsync(u => u.CodeOrder == code));

                user.CodeOrder = code;
            }

            var minDateTime = DateTime.Now.AddHours(2);
            if (DeliveryDate < minDateTime)
            {
                ModelState.AddModelError(string.Empty, $"Дата доставки не может быть раньше {minDateTime:dd.MM.yyyy HH:mm}");
                return Page();
            }

            var (openHour, closeHour) = DeliveryDate.DayOfWeek switch
            {
                DayOfWeek.Saturday => (9, 18),
                DayOfWeek.Sunday   => (10, 20),
                _                  => (9, 19)
            };
            if (DeliveryDate.Hour < openHour || DeliveryDate.Hour >= closeHour)
            {
                var dayName = DeliveryDate.DayOfWeek switch
                {
                    DayOfWeek.Monday    => "Понедельник",
                    DayOfWeek.Tuesday   => "Вторник",
                    DayOfWeek.Wednesday => "Среда",
                    DayOfWeek.Thursday  => "Четверг",
                    DayOfWeek.Friday    => "Пятница",
                    DayOfWeek.Saturday  => "Суббота",
                    DayOfWeek.Sunday    => "Воскресенье",
                    _ => "этот день"
                };
                ModelState.AddModelError(string.Empty,
                    $"В {dayName} магазин работает с {openHour}:00 до {closeHour}:00. Выберите другое время.");
                return Page();
            }

            var byBouquet = cart!.Items
                .Where(i => i.BouquetId.HasValue)
                .GroupBy(i => i.BouquetId!.Value)
                .Select(g => new { Id = g.Key, RequiredQty = g.Sum(x => x.Quantity) })
                .ToList();

            var bySoftToy = cart.Items
                .Where(i => i.SoftToyId.HasValue)
                .GroupBy(i => i.SoftToyId!.Value)
                .Select(g => new { Id = g.Key, RequiredQty = g.Sum(x => x.Quantity) })
                .ToList();

            if (byBouquet.Count == 0 && bySoftToy.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "В корзине нет ни букетов, ни мягких игрушек.");
                return Page();
            }

            var bouquetIds = byBouquet.Select(x => x.Id).ToHashSet();
            var softToyIds = bySoftToy.Select(x => x.Id).ToHashSet();

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

            var bouquets = await _context.Bouquets
                .Where(b => bouquetIds.Contains(b.Id)).AsTracking().ToListAsync();
            var softToys = await _context.SoftToys
                .Where(s => softToyIds.Contains(s.Id)).AsTracking().ToListAsync();

            var missingBouquets = bouquetIds.Except(bouquets.Select(b => b.Id)).ToList();
            var missingSoftToys = softToyIds.Except(softToys.Select(s => s.Id)).ToList();

            if (missingBouquets.Count > 0 || missingSoftToys.Count > 0)
            {
                ModelState.AddModelError(string.Empty, "Некоторые позиции недоступны.");
                return Page();
            }

            foreach (var grp in byBouquet)
            {
                var b = bouquets.First(x => x.Id == grp.Id);
                if (b.Quantity < grp.RequiredQty)
                {
                    ModelState.AddModelError(string.Empty, $"Недостаточно на складе: «{b.Name}». Доступно {b.Quantity}, требуется {grp.RequiredQty}.");
                    return Page();
                }
            }

            foreach (var grp in bySoftToy)
            {
                var s = softToys.First(x => x.Id == grp.Id);
                if (s.Quantity < grp.RequiredQty)
                {
                    ModelState.AddModelError(string.Empty, $"Недостаточно на складе: «{s.Name}». Доступно {s.Quantity}, требуется {grp.RequiredQty}.");
                    return Page();
                }
            }

            foreach (var grp in byBouquet)
            {
                var b = bouquets.First(x => x.Id == grp.Id);
                b.Quantity -= grp.RequiredQty;
                if (b.Quantity < 0) b.Quantity = 0;
            }

            foreach (var grp in bySoftToy)
            {
                var s = softToys.First(x => x.Id == grp.Id);
                s.Quantity -= grp.RequiredQty;
                if (s.Quantity < 0) s.Quantity = 0;
            }

            var total = cart.Items.Sum(i => i.Quantity * i.PriceSnapshot);

            user.Phone = Phone;
            var deliveryUtc = DateTime.SpecifyKind(DeliveryDate, DateTimeKind.Utc);

            var order = new OrderEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PickupDate = deliveryUtc,
                DeliveryAddress = DeliveryAddress,
                TotalAmount = total,
                Items = [.. cart.Items.Select(i => new OrderItemEntity
                {
                    Id = Guid.NewGuid(),
                    BouquetId = i.BouquetId,
                    SoftToyId = i.SoftToyId,
                    Quantity = i.Quantity,
                    Price = i.PriceSnapshot
                })]
            };

            _context.Orders.Add(order);
            _context.RemoveRange(cart.Items);
            _context.Carts.Remove(cart);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return RedirectToPage("/Home");
        }
    }
}
