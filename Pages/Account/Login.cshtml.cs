using FlowerShop.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FlowerShop.Web.Pages.Account
{
    public class LoginModel(FlowerDbContext context) : PageModel
    {
        private readonly FlowerDbContext _context = context;
        [BindProperty, Required(ErrorMessage = "Введите логин")]
        public string Login { get; set; }

        [BindProperty, Required(ErrorMessage = "Введите пароль")]
        public string Password { get; set; }

        public async Task<ActionResult> OnPostAsync(string? returnUrl = null, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return Page();

            if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Password))
            {
                ModelState.AddModelError(string.Empty, "Укажите логин и пароль");
                return Page();
            }

            var user = await _context.UserDomains
                .FirstOrDefaultAsync(u => u.Login == Login, ct);

            if (user is null || !BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Неверный логин или пароль");
                return Page();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.Name) ? user.Login : user.Name),
                new Claim(ClaimTypes.Email, user.Login)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToPage("/Account/Profile"); 

        }
    }
}
