using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOCreate;
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
            var orders = await _context.Orders
                .Select(o => new GetOrderDto(
                    o.UserId,
                    o.PickupDate,
                    o.TotalAmount,
                    o.Status,
                    o.Items.Select(i => new GetOrderItemDto(
                        i.BouquetId,
                        i.Quantity,
                        i.Price,
                        new GetBouquetDto(
                            i.Bouquet.Id,
                            i.Bouquet.Name,
                            i.Bouquet.Description,
                            i.Bouquet.Price,
                            i.Bouquet.Quantity,
                            i.Bouquet.ImageUrl
                        )
                    )).ToList()
                )).ToListAsync();

            return Ok(orders);
        }

        [HttpPost]
        public async Task<ActionResult<List<OrderEntity>>> CreateOrders([FromBody] CreateOrderDto order)
        {
            if (order is null)
                return NoContent();

            var newOrder = new OrderEntity
            {
                Id = Guid.NewGuid(),
                UserId = order.UserId,
                PickupDate = order.PickupDate,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                Items = [.. order.Items.Select(i => new OrderItemEntity
                {
                    Id = Guid.NewGuid(),
                    Quantity = i.Quantity,
                    Price = i.Price
                })]
            };
            await _context.Orders.AddAsync(newOrder);
            await _context.SaveChangesAsync();
            return Ok(newOrder);
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
        [HttpDelete("many")]
        public async Task<ActionResult> DeleateOrderMany()
        {
            var order = await _context.Orders.ToListAsync();
            if (order == null)
            {
                return NotFound("Order not found.");
            }
            _context.Orders.RemoveRange(order);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
