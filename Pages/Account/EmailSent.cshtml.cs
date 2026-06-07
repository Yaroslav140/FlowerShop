using FlowerShop.Data;
using FlowerShop.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Web.Pages.Account
{
    public class EmailSentModel(FlowerDbContext context, IEmailService emailService) : PageModel
    {
        private readonly FlowerDbContext _context = context;
        private readonly IEmailService _emailService = emailService;

        [BindProperty(SupportsGet = true)]
        public string Email { get; set; } = string.Empty;

        public void OnGet() { }

        public async Task<IActionResult> OnPostResendAsync(string email, CancellationToken ct = default)
        {
            var user = await _context.UserDomains
                .FirstOrDefaultAsync(u => u.Email == email, ct);

            if (user is null || user.EmailConfirmed)
                return RedirectToPage("/Account/Login");

            var token = Guid.NewGuid().ToString("N");
            user.EmailConfirmationToken = token;
            await _context.SaveChangesAsync(ct);

            var link = Url.PageLink("/Account/ConfirmEmail", values: new { token })!;
            await _emailService.SendConfirmationEmailAsync(user.Email, user.Name, link, ct);

            Email = email;
            return Page();
        }
    }
}
