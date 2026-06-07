using FlowerShop.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Web.Pages.Account
{
    public class ConfirmEmailModel(FlowerDbContext context) : PageModel
    {
        private readonly FlowerDbContext _context = context;

        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(string? token, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                ErrorMessage = "Ссылка для подтверждения недействительна.";
                return Page();
            }

            var user = await _context.UserDomains
                .FirstOrDefaultAsync(u => u.EmailConfirmationToken == token, ct);

            if (user is null)
            {
                ErrorMessage = "Ссылка уже была использована или недействительна.";
                return Page();
            }

            user.EmailConfirmed = true;
            user.EmailConfirmationToken = null;
            await _context.SaveChangesAsync(ct);

            Success = true;
            return Page();
        }
    }
}
