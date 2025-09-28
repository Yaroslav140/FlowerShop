using FlowerShop.Data;
using FlowerShop.Data.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FlowerShop.Web.Pages.Account
{
    public class RegisterModel : PageModel
    {
        [BindProperty, Required(ErrorMessage = "Введите имя пользователя")]
        public string UserName { get; set; }
        [BindProperty, Required(ErrorMessage = "Введите корректный Login")]
        public string Login { get; set; }
        [BindProperty, Required, MinLength(6, ErrorMessage = "Пароль должен быть минимум 6 символов")] 
        public string Password { get; set; }
        [BindProperty, Required, Compare("Password", ErrorMessage = "Пароли не совпадают")]
        public string ConfirmPassword { get; set; }

        private readonly FlowerDbContext _context;
        public RegisterModel(FlowerDbContext context) => _context = context;

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return Page();

            var userName = UserName?.Trim();

            if (await _context.UserDomains.AnyAsync(u => u.Login == Login, ct))
            {
                ModelState.AddModelError(string.Empty, "Пользователь с таким логином уже существует");
                return Page();
            }

            var user = new UserDomain
            {
                Id = Guid.NewGuid(),
                Name = userName,
                Login = Login,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 12),
                DateRegistration = DateTime.UtcNow
            };

            _context.UserDomains.Add(user);

            try
            {
                await _context.SaveChangesAsync(ct); 
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "Пользователь с такой почтой уже существует");
                return Page();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Name ?? Login),
                new("Login", Login)
            };

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

            var authProps = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProps);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToPage("/Account/Profile");
        }

    }
}
