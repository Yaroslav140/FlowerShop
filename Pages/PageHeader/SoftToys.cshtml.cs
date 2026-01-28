using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlowerShop.Web.Pages.PageHeader
{
    public class SoftToysModel : PageModel
    {
        public List<GetSoftToyDto> GetSoftToy { get; set; }
    }
}
