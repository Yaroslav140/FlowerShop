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

        [BindProperty, Display(Name = "Email"), Required(ErrorMessage = "Введите email"),
         EmailAddress(ErrorMessage = "Введите корректный email")]
        public string Email { get; set; } = string.Empty;

        [BindProperty, Display(Name = "Пароль"), Required(ErrorMessage = "Введите пароль"), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public async Task<ActionResult> OnPostAsync(string? returnUrl = null, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return Page();

            var email = Email.Trim().ToLowerInvariant();
            var user = await _context.UserDomains
                .FirstOrDefaultAsync(u => u.Email == email, ct);

            if (user is null || !BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Неверный email или пароль");
                return Page();
            }

            if (!user.EmailConfirmed)
            {
                ModelState.AddModelError(string.Empty, "Сначала подтвердите email. Проверьте почту или запросите новое письмо.");
                return Page();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.Name) ? user.Email : user.Name),
                new(ClaimTypes.Email, user.Email)
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
