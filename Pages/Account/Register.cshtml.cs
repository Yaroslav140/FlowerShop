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
    public class RegisterModel(FlowerDbContext context) : PageModel
    {
        [BindProperty, Required(ErrorMessage = "Введите имя пользователя")]
        public string UserName { get; set; } = string.Empty;
        [BindProperty, Required(ErrorMessage = "Введите корректный Login")]
        public string Login { get; set; } = string.Empty;
        [BindProperty, Required(ErrorMessage = "Поле с паролем не заполнено"), MinLength(6, ErrorMessage = "Пароль должен быть минимум 6 символов")] 
        public string Password { get; set; } = string.Empty;
        [BindProperty, Required(ErrorMessage = "Поле с паролем не заполнено"), Compare("Password", ErrorMessage = "Пароли не совпадают")]
        public string ConfirmPassword { get; set; } = string.Empty;

        private readonly FlowerDbContext _context = context;

        public async Task<ActionResult> OnPostAsync(string? returnUrl = null, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .FirstOrDefault()?.ErrorMessage;
                return Page();
            }

            var userName = UserName.Trim();
            var login = Login.Trim();

            if (await _context.UserDomains.AnyAsync(u => u.Login == Login, ct))
            {
                TempData["ErrorMessage"] = "Пользователь с таким логином уже существует";  
                return Page();
            }

            var user = new UserDomain
            {
                Id = Guid.NewGuid(),
                Name = userName,
                Login = login,
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
                TempData["ErrorMessage"] = "Пользователь с таким логином уже существует";
                return Page();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Name ?? user.Login),
                new("Login", user.Login)
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
