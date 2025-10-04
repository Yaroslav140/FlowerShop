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
        public async Task<ActionResult<GetOrderDto>> CreateOrders([FromBody] CreateOrderDto dto)
        {
            if (dto == null) return BadRequest("Пустой запрос.");
            if (dto.Items == null || dto.Items.Count == 0) return BadRequest("В заказе нет позиций.");

            if (dto.Items.Any(i => i.Quantity <= 0))
                return BadRequest("Количество каждой позиции должно быть > 0.");
            if (dto.Items.Any(i => i.Price < 0))
                return BadRequest("Цена не может быть отрицательной.");

            var bouquetIds = dto.Items.Select(i => i.BouquetId).Distinct().ToList();
            var bouquets = await _context.Bouquets
                .Where(b => bouquetIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            var missing = bouquetIds.Where(id => !bouquets.ContainsKey(id)).ToList();
            if (missing.Count > 0)
                return BadRequest($"Не найдены букеты: {string.Join(", ", missing)}.");

            var needByBouquet = dto.Items
                .GroupBy(i => i.BouquetId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            foreach (var (bouquetId, needQty) in needByBouquet)
            {
                var b = bouquets[bouquetId];
                if (b.Quantity < needQty)
                    return BadRequest($"Недостаточно «{b.Name}»: нужно {needQty}, доступно {b.Quantity}.");
            }

            var newOrder = new OrderEntity
            {
                UserId = dto.UserId,
                PickupDate = dto.PickupDate,
                TotalAmount = dto.TotalAmount, 
                Status = dto.Status,
                Items = dto.Items.Select(i => new OrderItemEntity
                {
                    BouquetId = i.BouquetId,
                    Quantity = i.Quantity,
                    Price = i.Price
                }).ToList()
            };

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var (bouquetId, needQty) in needByBouquet)
                    bouquets[bouquetId].Quantity -= needQty;

                _context.Orders.Add(newOrder);
                _context.Bouquets.UpdateRange(bouquets.Values);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            var result = new GetOrderDto(
                newOrder.Id,
                newOrder.UserId,
                newOrder.PickupDate,
                newOrder.TotalAmount,
                newOrder.Status,
                [.. newOrder.Items.Select(oi => new GetOrderItemDto(
                    oi.Id,
                    oi.BouquetId,
                    oi.Quantity,
                    oi.Price,
                    new GetBouquetDto(
                        bouquets[oi.BouquetId].Id,
                        bouquets[oi.BouquetId].Name,
                        bouquets[oi.BouquetId].Description,
                        bouquets[oi.BouquetId].Price,
                        bouquets[oi.BouquetId].Quantity,
                        bouquets[oi.BouquetId].ImageUrl
                    )
                ))]);

            return Ok(result);
        }


        [HttpPost("many")]
        public async Task<ActionResult> CreateOrdersMany([FromBody] List<CreateOrderDto> dtos)
        {
            if (dtos == null || dtos.Count == 0)
                return BadRequest("Список заказов пуст.");

            var newOrders = new List<OrderEntity>(dtos.Count);
            foreach (var dto in dtos)
            {
                newOrders.Add(new OrderEntity
                {
                    UserId = dto.UserId,
                    PickupDate = dto.PickupDate,
                    TotalAmount = dto.TotalAmount,
                    Status = dto.Status,
                    Items = dto.Items
                        .Select(i => new OrderItemEntity
                        {
                            BouquetId = i.BouquetId,
                            Quantity = i.Quantity,
                            Price = i.Price
                        })
                        .ToList()
                });
            }

            var bouquetIds = newOrders
                .SelectMany(o => o.Items)
                .Select(i => i.BouquetId)
                .Distinct()
                .ToList();

            if (bouquetIds.Count == 0)
                return BadRequest("В заказах нет позиций.");

            var bouquets = await _context.Bouquets
                .Where(b => bouquetIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            var missingIds = bouquetIds.Where(id => !bouquets.ContainsKey(id)).ToList();
            if (missingIds.Count > 0)
                return BadRequest($"Некоторые букеты не найдены: {string.Join(", ", missingIds)}.");

            var requestedByBouquet = newOrders
                .SelectMany(o => o.Items)
                .GroupBy(i => i.BouquetId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            foreach (var (bouquetId, needQty) in requestedByBouquet)
            {
                var b = bouquets[bouquetId];
                if (needQty <= 0)
                    return BadRequest($"Некорректное количество для букета {b.Name}.");

                if (b.Quantity < needQty)
                    return BadRequest($"Недостаточно «{b.Name}»: нужно {needQty}, доступно {b.Quantity}.");
            }

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var (bouquetId, needQty) in requestedByBouquet)
                {
                    bouquets[bouquetId].Quantity -= needQty;
                }

                _context.Orders.AddRange(newOrders);
                _context.Bouquets.UpdateRange(bouquets.Values);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            return Ok(); 
        }

        [HttpPost("{id:guid}/cancel")]
        public async Task<ActionResult> Cancel(Guid id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Bouquet)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound("Order not found.");

            if (order.Status == OrderStatus.Cancelled)
                return Ok();

            if (order.Status is not OrderStatus.New and not OrderStatus.Pending)
                return BadRequest($"Нельзя отменить заказ в статусе {order.Status}.");

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var item in order.Items)
                {
                    item.Bouquet.Quantity += item.Quantity;
                }

                order.Status = OrderStatus.Cancelled;

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            return Ok();
        }

    }
}
