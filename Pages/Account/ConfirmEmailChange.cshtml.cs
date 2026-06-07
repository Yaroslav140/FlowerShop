using FlowerShop.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Web.Pages.Account
{
    public class ConfirmEmailChangeModel(FlowerDbContext context) : PageModel
    {
        private readonly FlowerDbContext _context = context;

        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(string? token, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                ErrorMessage = "Недействительная ссылка.";
                return Page();
            }

            var user = await _context.UserDomains
                .FirstOrDefaultAsync(u => u.EmailChangeToken == token, ct);

            if (user is null || string.IsNullOrWhiteSpace(user.PendingEmail))
            {
                ErrorMessage = "Ссылка уже использована или недействительна.";
                return Page();
            }

            user.Email = user.PendingEmail;
            user.PendingEmail = null;
            user.EmailChangeToken = null;
            await _context.SaveChangesAsync(ct);

            // Сбрасываем сессию — пользователь должен войти с новым email
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            Success = true;
            return Page();
        }
    }
}
