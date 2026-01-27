using FlowerShop.Data;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering; 
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

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
        [Required(ErrorMessage = "Необходимо выбрать заказ")]
        public Guid? SelectedOrderId { get; set; } 
        public Guid? UserId { get; set; }

        public List<SelectListItem> AvailableOrders { get; set; } = [];

        public async Task OnGetAsync()
        {
            await LoadOrdersAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadOrdersAsync();
                return Page();
            }

            return RedirectToPage("/Home"); 
        }

        private async Task LoadOrdersAsync()
        {
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (Guid.TryParse(userIdString, out Guid parsedId))
            {
                UserId = parsedId;

                var ordersData = await _context.Orders
                    .Where(o => o.UserId == UserId)
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

                    Text = $"Заказ #{o.Id.ToString().PadLeft(4, '0')[^4..]} от {o.PickupDate:dd.MM.yy HH:mm} ({o.TotalQuantity} шт.)"
                })];
            }
        }
    }
}
