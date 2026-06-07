using FlowerShop.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FlowerShop.Web.Pages.Account
{
    public class ResetPasswordModel(FlowerDbContext context) : PageModel
    {
        private readonly FlowerDbContext _context = context;

        [BindProperty(SupportsGet = true)]
        public string Token { get; set; } = string.Empty;

        [BindProperty, Display(Name = "Новый пароль"),
         Required(ErrorMessage = "Введите пароль"),
         MinLength(6, ErrorMessage = "Минимум 6 символов"),
         DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty, Display(Name = "Повторите пароль"),
         Required(ErrorMessage = "Повторите пароль"),
         Compare("NewPassword", ErrorMessage = "Пароли не совпадают"),
         DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;

        public bool TokenValid { get; set; }

        public async Task OnGetAsync(CancellationToken ct = default)
        {
            var user = await _context.UserDomains
                .FirstOrDefaultAsync(u => u.PasswordResetToken == Token, ct);

            TokenValid = user != null && user.PasswordResetTokenExpiry > DateTime.UtcNow;
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
        {
            var user = await _context.UserDomains
                .FirstOrDefaultAsync(u => u.PasswordResetToken == Token, ct);

            if (user == null || user.PasswordResetTokenExpiry <= DateTime.UtcNow)
            {
                TokenValid = false;
                return Page();
            }

            TokenValid = true;

            if (!ModelState.IsValid) return Page();

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword, workFactor: 12);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            await _context.SaveChangesAsync(ct);

            return RedirectToPage("/Account/Login");
        }
    }
}
