using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlowerShop.Web.Pages
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        public void OnGet()
        {
            if(!User.Identity?.IsAuthenticated ?? true)
            {
                Response.Redirect("/Register");
            }
        }
    }
}
