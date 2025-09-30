using FlowerShop.Data;
using FlowerShop.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CartsController : Controller
    {
        private readonly FlowerDbContext _context;

        public CartsController(FlowerDbContext context) => _context = context;

        [HttpGet]
        public async Task<ActionResult> GetCartsAsync()
        {
            var cart = await _context.Carts.Select(c => new CartEntity()
            {
                Id = c.Id,
                Items = c.Items,
                UserId = c.UserId
            }).ToListAsync();
            return Ok();
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
