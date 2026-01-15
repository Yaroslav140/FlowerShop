using FlowerShop.Data;
using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOCreate;
using FlowerShop.Dto.DTOGet;
using FlowerShop.Dto.DTOUpdate;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace FlowerShop.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController(FlowerDbContext context) : ControllerBase
    {
        private readonly FlowerDbContext _context = context;

        [HttpGet]
        public async Task<ActionResult<List<GetOrderDto>>> GetOrders(Guid? userId)
        {
            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                    .ThenInclude(i => i.Bouquet)
                .Where(o => userId != null
                    ? o.UserId == userId
                    : o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Completed)
                .Select(o => new GetOrderDto(
                    o.Id,
                    o.User.Name,
                    o.PickupDate.ToString("dd.MM.yyyy"),
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
                ))
                .ToListAsync();

            return Ok(orders);
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<GetOrderDto>>> SearchOrders([FromQuery] string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return await GetOrders(null);

            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.User)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Bouquet)
                .Where(o => EF.Functions.Like(o.User.Name, $"%{name}%") ||
                           EF.Functions.Like(o.User.Login ?? "", $"%{name}%") ||
                           EF.Functions.Like(o.Id.ToString(), $"%{name}%"))
                .ToListAsync();
            var result = orders.Select(o => new GetOrderDto(
                o.Id,
                o.User.Name,
                o.PickupDate.ToString("dd.MM.yyyy"),
                o.TotalAmount,
                o.Status,
                [.. o.Items.Select(i => new GetOrderItemDto(
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
                ))]
            )).ToList();
            return Ok(result);
        }


        [HttpPost]
        public async Task<ActionResult<GetOrderDto>> CreateOrders([FromBody] CreateOrderDto dto)
        {
            if (dto == null) return BadRequest("Пустой запрос.");
            if (dto.Items == null || dto.Items.Count == 0) return BadRequest("В заказе нет позиций.");
            if (dto.Items.Any(i => i.Quantity <= 0)) return BadRequest("Количество каждой позиции должно быть > 0.");
            if (dto.Items.Any(i => i.Price < 0)) return BadRequest("Цена не может быть отрицательной.");

            var userName = (dto.Username ?? "").Trim();
            var login = (dto.Login ?? "").Trim();
            if (string.IsNullOrWhiteSpace(userName) && string.IsNullOrEmpty(login))
                return BadRequest("Имя клиента или логин обязателен.");

            var pickupUtc = dto.PickupDate.Kind == DateTimeKind.Utc ? dto.PickupDate : DateTime.SpecifyKind(dto.PickupDate, DateTimeKind.Utc);

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

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                UserDomain? user = null;

                if (dto.UserId != Guid.Empty)
                    user = await _context.UserDomains.FirstOrDefaultAsync(u => u.Id == dto.UserId);

                if (user == null)
                    user = await _context.UserDomains.FirstOrDefaultAsync(u => u.Login == login);

                if (user == null)
                {
                    var passwordHash = BCrypt.Net.BCrypt.HashPassword(userName ?? login);

                    user = new UserDomain
                    {
                        Id = Guid.NewGuid(),
                        Name = string.IsNullOrEmpty(userName) ? login : userName,
                        Login = string.IsNullOrEmpty(login) ? userName : login,
                        PasswordHash = passwordHash,
                    };

                    _context.UserDomains.Add(user);
                }

                foreach (var (bouquetId, needQty) in needByBouquet)
                    bouquets[bouquetId].Quantity -= needQty;

                user.CodeOrder = GeneratedCode.Generated.GenerateRandomCode();

                var newOrder = new OrderEntity
                {
                    UserId = user.Id,
                    PickupDate = pickupUtc,
                    TotalAmount = dto.TotalAmount,
                    Status = dto.Status,
                    Items = [.. dto.Items.Select(i => new OrderItemEntity
                    {
                        BouquetId = i.BouquetId,
                        Quantity = i.Quantity,
                        Price = i.Price
                    })],
                    User = user
                };

                _context.Orders.Add(newOrder);
                _context.Bouquets.UpdateRange(bouquets.Values);

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    user = await _context.UserDomains.FirstOrDefaultAsync(u => u.Name == userName);
                    if (user == null) throw;

                    newOrder.UserId = user.Id;
                    newOrder.User = user;

                    await _context.SaveChangesAsync();
                }

                await tx.CommitAsync();

                var result = new GetOrderDto(
                    newOrder.Id,
                    user.Name,
                    newOrder.PickupDate.ToString("dd.MM.yyyy"),
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
                ))]
                );

                return Ok(result);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
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
                    Items = [.. dto.Items
                        .Select(i => new OrderItemEntity
                        {
                            BouquetId = i.BouquetId,
                            Quantity = i.Quantity,
                            Price = i.Price
                        })]
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

        [HttpPut]
        public async Task<ActionResult> Update([FromBody] UpdateOrderDto dto)
        {
            if (dto == null) return BadRequest("Пустой запрос.");
            if (dto.Items == null || dto.Items.Count == 0) return BadRequest("В заказе нет позиций.");
            if (dto.Items.Any(i => i.Quantity <= 0)) return BadRequest("Количество каждой позиции должно быть > 0.");
            if (dto.Items.Any(i => i.Price < 0)) return BadRequest("Цена не может быть отрицательной.");

            var userName = (dto.Username ?? "").Trim();
            if (string.IsNullOrWhiteSpace(userName))
                return BadRequest("Имя клиента обязательно.");

            var pickupUtc = dto.PickupDate.Kind == DateTimeKind.Utc ? dto.PickupDate : DateTime.SpecifyKind(dto.PickupDate, DateTimeKind.Utc);

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                .ThenInclude(i => i.Bouquet)
                .FirstOrDefaultAsync(o => o.Id == dto.OrderId);

            if (order == null)
                return NotFound("Заказ не найден.");

            if (order.Status is OrderStatus.Completed or OrderStatus.Cancelled)
                return BadRequest($"Нельзя редактировать заказ в статусе {order.Status}.");

            var newNeedByBouquet = dto.Items
                .GroupBy(i => i.BouquetId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var oldNeedByBouquet = order.Items
                .GroupBy(i => i.BouquetId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var allBouquetIds = newNeedByBouquet.Keys
                .Union(oldNeedByBouquet.Keys)
                .Distinct()
                .ToList();

            var bouquets = await _context.Bouquets
                .Where(b => allBouquetIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            var missing = allBouquetIds.Where(id => !bouquets.ContainsKey(id)).ToList();
            if (missing.Count > 0)
                return BadRequest($"Не найдены букеты: {string.Join(", ", missing)}.");

            foreach (var bouquetId in allBouquetIds)
            {
                oldNeedByBouquet.TryGetValue(bouquetId, out var oldQty);
                newNeedByBouquet.TryGetValue(bouquetId, out var newQty);

                var delta = newQty - oldQty;
                if (delta > 0 && bouquets[bouquetId].Quantity < delta)
                    return BadRequest($"Недостаточно «{bouquets[bouquetId].Name}»: нужно добавить {delta}, доступно {bouquets[bouquetId].Quantity}.");
            }

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var bouquetId in allBouquetIds)
                {
                    oldNeedByBouquet.TryGetValue(bouquetId, out var oldQty);
                    newNeedByBouquet.TryGetValue(bouquetId, out var newQty);

                    var delta = newQty - oldQty;
                    if (delta != 0)
                        bouquets[bouquetId].Quantity -= delta;
                }
                order.User.Name = userName;

                var byId = order.Items.ToDictionary(i => i.Id);

                var incomingExistingIds = dto.Items
                    .Where(i => i.OrderItemId.HasValue)
                    .Select(i => i.OrderItemId!.Value)
                    .ToHashSet();

                var toRemove = order.Items.Where(i => !incomingExistingIds.Contains(i.Id)).ToList();
                _context.OrderItems.RemoveRange(toRemove);

                foreach (var it in dto.Items)
                {
                    if (it.OrderItemId.HasValue && byId.TryGetValue(it.OrderItemId.Value, out var existingItem))
                    {
                        existingItem.BouquetId = it.BouquetId;
                        existingItem.Quantity = it.Quantity;
                        existingItem.Price = it.Price;
                    }
                    else
                    {
                        order.Items.Add(new OrderItemEntity
                        {
                            BouquetId = it.BouquetId,
                            Quantity = it.Quantity,
                            Price = it.Price
                        });
                    }
                }

                order.PickupDate = pickupUtc;
                order.Status = dto.Status;
                order.TotalAmount = dto.TotalAmount;

                _context.Bouquets.UpdateRange(bouquets.Values);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return Ok();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> DeleteOrder(Guid id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
                return NotFound("Заказ не найден.");

            var bouquetIds = order.Items.Select(i => i.BouquetId).ToList();
            var bouquets = await _context.Bouquets
                .Where(b => bouquetIds.Contains(b.Id))
                .ToListAsync();

            foreach (var item in bouquets)
            {
                var orderItem = order.Items.FirstOrDefault(oi => oi.BouquetId == item.Id);
                if (orderItem != null)
                {
                    item.Quantity += orderItem.Quantity;
                }
            }

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
