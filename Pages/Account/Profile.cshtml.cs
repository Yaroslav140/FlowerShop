using FlowerShop.Data;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlowerShop.Web.Pages.Account
{
    public class ProfileModel : PageModel
    {
        private readonly FlowerDbContext _context;
        public string Username {  get; set; }
        public string Login {  get; set; }
        public DateTime DateRegister {  get; set; }

        public List<GetOrderDto> Orders { get; set; } = [];
        public ProfileModel(FlowerDbContext context) => _context = context;
        public void OnGet()
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
            }
        }

        public async Task<IActionResult> OnPostExitAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Home");
        }
    }
}