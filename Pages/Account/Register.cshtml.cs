using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FlowerShop.Web.Pages.Account
{
    public class RegisterModel(FlowerDbContext context, IEmailService emailService) : PageModel
    {
        private readonly FlowerDbContext _context = context;
        private readonly IEmailService _emailService = emailService;

        [BindProperty, Display(Name = "Имя пользователя"), Required(ErrorMessage = "Введите имя пользователя")]
        public string UserName { get; set; } = string.Empty;

        [BindProperty, Display(Name = "Email"), Required(ErrorMessage = "Введите email"),
         EmailAddress(ErrorMessage = "Введите корректный email (example@mail.ru)")]
        public string Email { get; set; } = string.Empty;

        [BindProperty, Display(Name = "Пароль"), Required(ErrorMessage = "Поле с паролем не заполнено"),
         MinLength(6, ErrorMessage = "Пароль должен быть минимум 6 символов"), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [BindProperty, Display(Name = "Повторите пароль"), Required(ErrorMessage = "Поле с паролем не заполнено"),
         Compare("Password", ErrorMessage = "Пароли не совпадают"), DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;

        [BindProperty, Display(Name = "Согласие с обработкой персональных данных"),
         Range(typeof(bool), "true", "true", ErrorMessage = "Необходимо дать согласие на обработку персональных данных")]
        public bool ConsentPersonalData { get; set; }

        public async Task<ActionResult> OnPostAsync(string Website, string? returnUrl = null, CancellationToken ct = default)
        {
            if (!string.IsNullOrEmpty(Website))
                return RedirectToPage("/Account/Login");

            ModelState.Remove("WebSite");
            if (!ModelState.IsValid)
                return Page();

            var userName = UserName.Trim();
            var email = Email.Trim().ToLowerInvariant();

            if (await _context.UserDomains.AnyAsync(u => u.Email == email, ct))
            {
                ModelState.AddModelError(string.Empty, "Пользователь с таким email уже зарегистрирован");
                return Page();
            }

            var token = Guid.NewGuid().ToString("N");

            var user = new UserDomain
            {
                Id = Guid.NewGuid(),
                Name = userName,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 12),
                DateRegistration = DateTime.UtcNow,
                EmailConfirmed = false,
                EmailConfirmationToken = token
            };

            _context.UserDomains.Add(user);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "Пользователь с таким email уже зарегистрирован");
                return Page();
            }

            var confirmLink = Url.PageLink("/Account/ConfirmEmail", values: new { token })!;
            try
            {
                await _emailService.SendConfirmationEmailAsync(email, userName, confirmLink, ct);
            }
            catch
            {
                // Письмо не отправилось — пользователь создан, можно повторно отправить
            }

            return RedirectToPage("/Account/EmailSent", new { email });
        }
    }
}
