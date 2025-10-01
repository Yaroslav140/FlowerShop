using FlowerShop.Data;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace FlowerShop.Web.Pages.Account
{
    public class ProfileModel(FlowerDbContext context) : PageModel
    {
        private readonly FlowerDbContext _context = context;

        public string Username {  get; set; } = string.Empty;
        public string Login {  get; set; } = string.Empty;
        public DateTime DateRegister {  get; set; }

        public List<GetOrderDto> Orders { get; set; } = [];

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
                    DateRegister = user.DateRegistration;
                }
                Orders = await _context.Orders.Where(o => o.UserId == userId).Select(o => new GetOrderDto(
                    o.UserId,
                    o.PickupDate,
                    o.TotalAmount,
                    o.Status,
                    o.Items.Select(oi => new GetOrderItemDto(oi.BouquetId, oi.Quantity, oi.Price,
                        new GetBouquetDto(
                            oi.Bouquet.Id, 
                            oi.Bouquet.Name,
                            oi.Bouquet.Description,
                            oi.Bouquet.Price,
                            oi.Bouquet.Quantity,
                            oi.Bouquet.ImageUrl))).ToList().ToList())).ToListAsync();
            }
        }

        public async Task<ActionResult> OnPostExitAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Home");
        }
    }
}