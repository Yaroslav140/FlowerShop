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
            var cart =_context.Carts.Select(c => new CartEntity()
            {
                Id = c.Id,
                CreatedAt = c.CreatedAt,
                Items = c.Items,
                UpdatedAt = c.UpdatedAt,
                UserId = c.UserId
            }).ToListAsync();
            return Ok();
        }
    }
}
