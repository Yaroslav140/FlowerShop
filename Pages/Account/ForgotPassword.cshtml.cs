using FlowerShop.Data;
using FlowerShop.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FlowerShop.Web.Pages.Account
{
    public class ForgotPasswordModel(FlowerDbContext context, IEmailService emailService) : PageModel
    {
        private readonly FlowerDbContext _context = context;
        private readonly IEmailService _emailService = emailService;

        [BindProperty, Display(Name = "Email"), Required(ErrorMessage = "Введите email"),
         EmailAddress(ErrorMessage = "Введите корректный email")]
        public string Email { get; set; } = string.Empty;

        public bool Sent { get; set; }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
        {
            if (!ModelState.IsValid) return Page();

            var email = Email.Trim().ToLowerInvariant();
            var user = await _context.UserDomains.FirstOrDefaultAsync(u => u.Email == email, ct);

            if (user != null && user.EmailConfirmed)
            {
                var token = Guid.NewGuid().ToString("N");
                user.PasswordResetToken = token;
                user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
                await _context.SaveChangesAsync(ct);

                var link = Url.PageLink("/Account/ResetPassword", values: new { token })!;
                try { await _emailService.SendPasswordResetEmailAsync(email, user.Name, link, ct); }
                catch { }
            }

            // Показываем одно сообщение независимо от результата — защита от перебора
            Sent = true;
            return Page();
        }
    }
}
