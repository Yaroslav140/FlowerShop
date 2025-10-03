using FlowerShop.Data;
using FlowerShop.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FlowerShop.Web.Pages.Account
{
    public class EditProfileModel(FlowerDbContext context) : PageModel
    {
        private readonly FlowerDbContext _context = context;
        [BindProperty]
        public UserDomain UserProfile { get; set; } = default!;
        public async Task<ActionResult> OnGetAsync()
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty);
            if (userId == null)
            {
                return RedirectToPage("/Account/Login");
            }
            UserProfile = await _context.UserDomains.FindAsync(userId);
            if (UserProfile == null)
            {
                return NotFound();
            }
            return Page();
        }

        public async Task<ActionResult> OnPostSaveProfileAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out var userId))
                return RedirectToPage("/Account/Login");

            var profile = await _context.UserDomains
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (profile == null)
                return NotFound();

            profile.Name = UserProfile.Name;
            profile.Phone = UserProfile.Phone;
            var exitsLogin = await _context.UserDomains.Where(l => l.Login == UserProfile.Login).FirstOrDefaultAsync();
            if (exitsLogin  != null)
            {
                TempData["Error"] = "Такой логин уже существует";
                return Page();
            }
            profile.Login = UserProfile.Login;

            await _context.SaveChangesAsync();

            return RedirectToPage("/Account/Profile");
        }
    }
}
