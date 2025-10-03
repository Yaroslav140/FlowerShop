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
                    o.Id,
                    o.UserId,
                    o.PickupDate,
                    o.TotalAmount,
                    o.Status,
                    o.Items.Select(i => new GetOrderItemDto(
                        i.Id,
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
        public async Task<ActionResult<OrderEntity>> CreateOrders([FromBody] CreateOrderDto dto)
        {
            if (dto is null || dto.Items is null || dto.Items.Count == 0)
                return BadRequest("Пустой заказ.");

            var bouquetIds = dto.Items.Select(i => i.BouquetId).Distinct().ToList();
            var bouquets = await _context.Bouquets
                .Where(b => bouquetIds.Contains(b.Id))
                .Select(b => new { b.Id, b.Price })
                .ToDictionaryAsync(b => b.Id, b => b.Price);

            var missing = bouquetIds.Where(id => !bouquets.ContainsKey(id)).ToList();
            if (missing.Count > 0)
                return NotFound($"Нет букетов: {string.Join(", ", missing)}");

            var items = dto.Items.Select(i => new OrderItemEntity
            {
                Id = Guid.NewGuid(),
                BouquetId = i.BouquetId,
                Quantity = i.Quantity,
                Price = bouquets[i.BouquetId]
            }).ToList();

            var total = items.Sum(i => i.Price * i.Quantity);

            var newOrder = new OrderEntity
            {
                Id = Guid.NewGuid(),
                UserId = dto.UserId,
                PickupDate = DateTime.SpecifyKind(dto.PickupDate, DateTimeKind.Utc),
                TotalAmount = total,
                Status = dto.Status,
                Items = items
            };

            await _context.Orders.AddAsync(newOrder);
            await _context.SaveChangesAsync();
            return Ok(newOrder);
        }


        [HttpPost("many")]
        public async Task<ActionResult> CreateOrdersMany([FromBody] List<CreateOrderDto> dtos)
        {
            if (dtos is null || dtos.Count == 0) return NoContent();

            var allBouquetIds = dtos.SelectMany(o => o.Items.Select(i => i.BouquetId)).Distinct().ToList();
            var prices = await _context.Bouquets
                .Where(b => allBouquetIds.Contains(b.Id))
                .Select(b => new { b.Id, b.Price })
                .ToDictionaryAsync(b => b.Id, b => b.Price);

            var missing = allBouquetIds.Where(id => !prices.ContainsKey(id)).ToList();
            if (missing.Count > 0)
                return NotFound($"Нет букетов: {string.Join(", ", missing)}");

            var newOrders = new List<OrderEntity>();

            foreach (var dto in dtos)
            {
                var items = dto.Items.Select(i => new OrderItemEntity
                {
                    Id = Guid.NewGuid(),
                    BouquetId = i.BouquetId,
                    Quantity = i.Quantity,
                    Price = prices[i.BouquetId] 
                }).ToList();

                var total = items.Sum(i => i.Price * i.Quantity);

                newOrders.Add(new OrderEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = dto.UserId,
                    PickupDate = DateTime.SpecifyKind(dto.PickupDate, DateTimeKind.Utc),
                    TotalAmount = total,
                    Status = dto.Status,
                    Items = items
                });
            }

            _context.Orders.AddRange(newOrders);
            await _context.SaveChangesAsync();

            var result = newOrders.Select(o => new
            {
                o.Id,
                o.UserId,
                o.PickupDate,
                o.TotalAmount,
                o.Status,
                Items = o.Items.Select(i => new { i.Id, i.BouquetId, i.Quantity, i.Price })
            });

            return Ok(result);
        }


        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> DeleateOrderId(Guid id)
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
