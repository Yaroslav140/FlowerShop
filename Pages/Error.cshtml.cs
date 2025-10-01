using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlowerShop.Web.Pages
{
    public class ErrorModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public int Code { get; set; }

        public string Message { get; set; }

        public void OnGet(int code)
        {
            Code = code;
            Message = code switch
            {
                404 => "�������� �� �������",
                500 => "������ �� �������",
                _ => "���-�� ����� �� ���"
            };
        }
    }
}
