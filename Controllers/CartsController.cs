using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CartsController(FlowerDbContext context) : Controller
    {
        private readonly FlowerDbContext _context = context;

        [HttpGet]
        public async Task<ActionResult<List<GetCartDto>>> GetCartsAsync()
        {
            var cart = await _context.Carts.Select(c => new GetCartDto(
                c.Id,
                c.UserId,
                c.Items.Select(ci => new GetCartItemDto(
                    ci.BouquetId,
                    ci.Quantity,
                    ci.PriceSnapshot)).ToList())).ToListAsync();
            return Ok(cart);
        }


        [HttpDelete]
        public async Task<ActionResult> DeleateCarts()
        {
            var carts = await _context.Carts.ExecuteDeleteAsync();
            if (carts == 0)
                return NotFound("Нету такой корзины");
            return Ok(carts);
        }
    }
}
