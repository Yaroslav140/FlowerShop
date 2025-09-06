using FlowerShop.Data;
using FlowerShop.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly FlowerDbContext _context;
        public OrdersController(FlowerDbContext context) => _context = context;

        [HttpGet]
        public async Task<ActionResult<List<OrderEntity>>> GetOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                .ToListAsync();
            return Ok(orders);
        }

        [HttpPost]
        public async Task<ActionResult<List<OrderEntity>>> CreateOrders([FromBody] OrderEntity order)
        {
            try
            {
                order.TotalAmount = order.Items.Sum(i => i.Price * i.Quantity);

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
            return CreatedAtAction(nameof(GetOrders), new { id = order.Id }, order);
        }

        [HttpDelete]
        public async Task<ActionResult<List<OrderEntity>>> DeleateOrder(Guid id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
                return NotFound("Order not found.");

            _context.OrderItems.RemoveRange(_context.OrderItems.Where(oi => oi.OrderId == id));
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            return Ok(await _context.Orders.ToListAsync());
        }
    }
}
