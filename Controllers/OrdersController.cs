using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOGet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController(FlowerDbContext context) : ControllerBase
    {
        private readonly FlowerDbContext _context = context;

        [HttpGet]
        public async Task<ActionResult<List<GetOrderDto>>> GetOrders()
        {
            var userId = Guid.Parse(User.Identity.Name!);
            var orders = await _context.Orders
                .Select(o => new GetOrderDto(
                    userId,
                    o.PickupDate,
                    o.TotalAmount,
                    o.Status,
                    o.Items.Select(i => new GetOrderItemDto(
                        i.BouquetId, 
                        i.FlowerId, 
                        i.Quantity,
                        i.Price)
                    ).ToList())).ToListAsync();
            return Ok(orders);
        }

        [HttpPost]
        public async Task<ActionResult<List<GetOrderDto>>> CreateOrders([FromBody] OrderEntity order)
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
        public async Task<ActionResult> DeleateOrder(Guid id)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
            {
                return NotFound("Order not found.");
            }
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
