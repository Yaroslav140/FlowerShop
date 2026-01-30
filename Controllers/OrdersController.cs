using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOCreate;
using FlowerShop.Dto.DTOGet;
using FlowerShop.Dto.DTOUpdate;
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
        public async Task<ActionResult<List<GetOrderDto>>> GetOrders(Guid? userId)
        {
            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                    .ThenInclude(i => i.Bouquet)
                .Include(o => o.Items)
                    .ThenInclude(i => i.SoftToy)
                .Where(o => userId != null
                    ? o.UserId == userId
                    : o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Completed)
                .Select(o => new GetOrderDto(
                    o.Id,
                    o.User.Name,
                    o.User.Login,
                    o.PickupDate,
                    o.DeliveryAddress,
                    o.TotalAmount,
                    o.Status,
                    o.CanReview,
                    o.Items.Select(i => new GetOrderItemDto(
                        i.Id,
                        i.BouquetId!,
                        i.SoftToyId!,
                        i.Quantity,
                        i.Price,
                        i.Bouquet != null ? new GetBouquetDto(
                            i.Bouquet.Id,
                            i.Bouquet.Name,
                            i.Bouquet.Description,
                            i.Bouquet.Price,
                            i.Bouquet.Quantity,
                            i.Bouquet.ImagePath,
                            i.Bouquet.Rating) : null,
                        i.SoftToy != null ? new GetSoftToyDto(
                            i.SoftToy.Id,
                            i.SoftToy.Name,
                            i.SoftToy.Description,
                            i.SoftToy.Quantity,
                            i.SoftToy.Price,
                            i.SoftToy.ImagePath,
                            i.SoftToy.Rating) : null
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
                o.User.Login,
                o.PickupDate,
                o.DeliveryAddress,
                o.TotalAmount,
                o.Status,
                o.CanReview,
                [.. o.Items.Select(i => new GetOrderItemDto(
                    i.Id,
                    i.BouquetId!,
                    i.SoftToyId!,
                    i.Quantity,
                    i.Price,
                    new GetBouquetDto(
                        i.Bouquet.Id,
                        i.Bouquet.Name,
                        i.Bouquet.Description,
                        i.Bouquet.Price,
                        i.Bouquet.Quantity,
                        i.Bouquet.ImagePath,
                        i.Bouquet.Rating
                    ),
                    new GetSoftToyDto(
                        i.SoftToy.Id,
                        i.SoftToy.Name,
                        i.SoftToy.Description,
                        i.SoftToy.Quantity,
                        i.SoftToy.Price,
                        i.SoftToy.ImagePath,
                        i.SoftToy.Rating
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

            var pickupUtc = dto.PickupDate.Kind == DateTimeKind.Utc
                ? dto.PickupDate
                : DateTime.SpecifyKind(dto.PickupDate, DateTimeKind.Utc);

            var bouquetIds = dto.Items
                .Where(i => i.BouquetId.HasValue)
                .Select(i => i.BouquetId!.Value)
                .Distinct()
                .ToList();

            var softToyIds = dto.Items
                .Where(i => i.SoftToyId.HasValue)
                .Select(i => i.SoftToyId!.Value)
                .Distinct()
                .ToList();

            var bouquets = bouquetIds.Count > 0
                ? await _context.Bouquets
                    .Where(b => bouquetIds.Contains(b.Id))
                    .ToDictionaryAsync(b => b.Id)
                : [];

            var softToys = softToyIds.Count > 0
                ? await _context.SoftToys
                    .Where(s => softToyIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id)
                : [];

            var missingBouquets = bouquetIds
                .Where(id => !bouquets.ContainsKey(id))
                .ToList();

            var missingSoftToys = softToyIds
                .Where(id => !softToys.ContainsKey(id))
                .ToList();

            if (missingBouquets.Count > 0 || missingSoftToys.Count > 0)
            {
                var msg = new List<string>();
                if (missingBouquets.Count > 0)
                    msg.Add($"букеты: {string.Join(", ", missingBouquets)}");
                if (missingSoftToys.Count > 0)
                    msg.Add($"мягкие игрушки: {string.Join(", ", missingSoftToys)}");

                return BadRequest($"Не найдены позиции: {string.Join("; ", msg)}.");
            }

            var needByBouquet = dto.Items
                .Where(i => i.BouquetId.HasValue)
                .GroupBy(i => i.BouquetId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            foreach (var (bouquetId, needQty) in needByBouquet)
            {
                var b = bouquets[bouquetId];
                if (b.Quantity < needQty)
                    return BadRequest($"Недостаточно «{b.Name}»: нужно {needQty}, доступно {b.Quantity}.");
            }

            var needBySoftToy = dto.Items
                .Where(i => i.SoftToyId.HasValue)
                .GroupBy(i => i.SoftToyId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            foreach (var (softToyId, needQty) in needBySoftToy)
            {
                var s = softToys[softToyId];
                if (s.Quantity < needQty)
                    return BadRequest($"Недостаточно «{s.Name}»: нужно {needQty}, доступно {s.Quantity}.");
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
                {
                    bouquets[bouquetId].Quantity -= needQty;
                    if (bouquets[bouquetId].Quantity < 0)
                        bouquets[bouquetId].Quantity = 0;
                }

                foreach (var (softToyId, needQty) in needBySoftToy)
                {
                    softToys[softToyId].Quantity -= needQty;
                    if (softToys[softToyId].Quantity < 0)
                        softToys[softToyId].Quantity = 0;
                }

                user.CodeOrder = GeneratedCode.Generated.GenerateRandomCode();

                var newOrder = new OrderEntity
                {
                    UserId = user.Id,
                    PickupDate = pickupUtc,
                    TotalAmount = dto.TotalAmount,
                    Status = dto.Status,
                    Items = dto.Items.Select(i => new OrderItemEntity
                    {
                        BouquetId = i.BouquetId,
                        SoftToyId = i.SoftToyId,
                        Quantity = i.Quantity,
                        Price = i.Price
                    }).ToList(),
                    User = user
                };

                _context.Orders.Add(newOrder);

                if (bouquets.Count > 0)
                    _context.Bouquets.UpdateRange(bouquets.Values);

                if (softToys.Count > 0)
                    _context.SoftToys.UpdateRange(softToys.Values);

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
                    user.Login,
                    newOrder.PickupDate,
                    newOrder.DeliveryAddress,
                    newOrder.TotalAmount,
                    newOrder.Status,
                    newOrder.CanReview,
                    newOrder.Items.Select(oi => new GetOrderItemDto(
                        oi.Id,
                        oi.BouquetId,
                        oi.SoftToyId,
                        oi.Quantity,
                        oi.Price,
                        oi.BouquetId.HasValue
                            ? new GetBouquetDto(
                                bouquets[oi.BouquetId.Value].Id,
                                bouquets[oi.BouquetId.Value].Name,
                                bouquets[oi.BouquetId.Value].Description,
                                bouquets[oi.BouquetId.Value].Price,
                                bouquets[oi.BouquetId.Value].Quantity,
                                bouquets[oi.BouquetId.Value].ImagePath,
                                bouquets[oi.BouquetId.Value].Rating
                            )
                            : null,
                        oi.SoftToyId.HasValue
                            ? new GetSoftToyDto(
                                softToys[oi.SoftToyId.Value].Id,
                                softToys[oi.SoftToyId.Value].Name,
                                softToys[oi.SoftToyId.Value].Description,
                                softToys[oi.SoftToyId.Value].Quantity,
                                softToys[oi.SoftToyId.Value].Price,
                                softToys[oi.SoftToyId.Value].ImagePath,
                                softToys[oi.SoftToyId.Value].Rating
                            )
                            : null
                    )).ToList()
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

            var missingIds = bouquetIds.Where(id => !bouquets.ContainsKey(id!.Value)).ToList();
            if (missingIds.Count > 0)
                return BadRequest($"Некоторые букеты не найдены: {string.Join(", ", missingIds)}.");

            var requestedByBouquet = newOrders
                .SelectMany(o => o.Items)
                .GroupBy(i => i.BouquetId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            foreach (var (bouquetId, needQty) in requestedByBouquet)
            {
                var b = bouquets[bouquetId!.Value];
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
                    bouquets[bouquetId!.Value].Quantity -= needQty;
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

            var pickupUtc = dto.PickupDate.Kind == DateTimeKind.Utc
                ? dto.PickupDate
                : DateTime.SpecifyKind(dto.PickupDate, DateTimeKind.Utc);

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Bouquet)
                .Include(o => o.Items)
                    .ThenInclude(i => i.SoftToy)
                .FirstOrDefaultAsync(o => o.Id == dto.OrderId);

            if (order == null) return NotFound("Заказ не найден.");

            if (order.Status is OrderStatus.Completed or OrderStatus.Cancelled)
                return BadRequest($"Нельзя редактировать заказ в статусе {order.Status}.");

            if (dto.Items.Any(i => !i.BouquetId.HasValue && !i.SoftToyId.HasValue))
                return BadRequest("Позиция должна содержать букет или мягкую игрушку.");

            var newBouquets = dto.Items
                .Where(i => i.BouquetId.HasValue)
                .GroupBy(i => i.BouquetId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var newToys = dto.Items
                .Where(i => i.SoftToyId.HasValue)
                .GroupBy(i => i.SoftToyId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var oldBouquets = order.Items
                .Where(i => i.BouquetId.HasValue)
                .GroupBy(i => i.BouquetId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var oldToys = order.Items
                .Where(i => i.SoftToyId.HasValue)
                .GroupBy(i => i.SoftToyId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var bouquetIds = newBouquets.Keys.Union(oldBouquets.Keys).ToList();
            var toyIds = newToys.Keys.Union(oldToys.Keys).ToList();

            var bouquets = await _context.Bouquets
                .Where(b => bouquetIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            var toys = await _context.SoftToys
                .Where(t => toyIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id);

            var missingBouquets = bouquetIds.Where(id => !bouquets.ContainsKey(id)).ToList();
            var missingToys = toyIds.Where(id => !toys.ContainsKey(id)).ToList();

            if (missingBouquets.Any())
                return BadRequest($"Не найдены букеты: {string.Join(", ", missingBouquets)}.");
            if (missingToys.Any())
                return BadRequest($"Не найдены игрушки: {string.Join(", ", missingToys)}.");

            foreach (var bouquetId in bouquetIds)
            {
                oldBouquets.TryGetValue(bouquetId, out var oldQty);
                newBouquets.TryGetValue(bouquetId, out var newQty);

                var delta = newQty - oldQty;
                if (delta > 0 && bouquets[bouquetId].Quantity < delta)
                    return BadRequest($"Недостаточно «{bouquets[bouquetId].Name}»: нужно добавить {delta}, доступно {bouquets[bouquetId].Quantity}.");
            }

            foreach (var toyId in toyIds)
            {
                oldToys.TryGetValue(toyId, out var oldQty);
                newToys.TryGetValue(toyId, out var newQty);

                var delta = newQty - oldQty;
                if (delta > 0 && toys[toyId].Quantity < delta)
                    return BadRequest($"Недостаточно «{toys[toyId].Name}»: нужно добавить {delta}, доступно {toys[toyId].Quantity}.");
            }

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var bouquetId in bouquetIds)
                {
                    oldBouquets.TryGetValue(bouquetId, out var oldQty);
                    newBouquets.TryGetValue(bouquetId, out var newQty);
                    var delta = newQty - oldQty;

                    if (delta != 0)
                        bouquets[bouquetId].Quantity -= delta;
                }

                foreach (var toyId in toyIds)
                {
                    oldToys.TryGetValue(toyId, out var oldQty);
                    newToys.TryGetValue(toyId, out var newQty);
                    var delta = newQty - oldQty;

                    if (delta != 0)
                        toys[toyId].Quantity -= delta;
                }

                order.User.Name = userName;

                var existingItemsById = order.Items.ToDictionary(i => i.Id);
                var incomingIds = dto.Items
                    .Where(i => i.OrderItemId.HasValue)
                    .Select(i => i.OrderItemId!.Value)
                    .ToHashSet();

                var toRemove = order.Items.Where(i => !incomingIds.Contains(i.Id)).ToList();
                _context.OrderItems.RemoveRange(toRemove);

                foreach (var item in dto.Items)
                {
                    if (item.OrderItemId.HasValue && existingItemsById.TryGetValue(item.OrderItemId.Value, out var existing))
                    {
                        existing.BouquetId = item.BouquetId;
                        existing.SoftToyId = item.SoftToyId;
                        existing.Quantity = item.Quantity;
                        existing.Price = item.Price;
                    }
                    else
                    {
                        order.Items.Add(new OrderItemEntity
                        {
                            BouquetId = item.BouquetId,
                            SoftToyId = item.SoftToyId,
                            Quantity = item.Quantity,
                            Price = item.Price
                        });
                    }
                }

                order.PickupDate = pickupUtc;
                order.Status = dto.Status;
                order.TotalAmount = dto.TotalAmount;
                order.CanReview = dto.Status == OrderStatus.Completed;

                _context.Bouquets.UpdateRange(bouquets.Values);
                _context.SoftToys.UpdateRange(toys.Values);

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
