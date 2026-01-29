using BCrypt.Net;
using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto.DTOCreate;
using FlowerShop.Dto.DTOGet;
using FlowerShop.Dto.DTOUpdate;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController(FlowerDbContext context) : ControllerBase
    {
        private readonly FlowerDbContext _context = context;

        [HttpGet]
        public async Task<ActionResult<List<GetUserDto>>> GetUsers()
        {
            var users = await _context.UserDomains
                .Select(u => new GetUserDto(
                    u.Id,
                    u.Name,
                    u.Login,
                    u.Phone,
                    u.CodeOrder,
                    u.Orders
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
                            i.BouquetId!.Value, 
                            i.SoftToyId!.Value, 
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
                                i.SoftToy.Rating) : null)).ToList())).ToList())).ToListAsync();
            return Ok(users);
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<GetUserDto>>> SearchUsers([FromQuery] string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return await GetUsers(); 

            var users = await _context.UserDomains
                .Where(u => EF.Functions.Like(u.Name, $"%{name}%") ||
                           EF.Functions.Like(u.Login, $"%{name}%"))
                .Select(u => new GetUserDto(
                    u.Id,
                    u.Name,
                    u.Login,
                    u.Phone,
                    u.CodeOrder,
                    u.Orders.Select(o => new GetOrderDto(
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
                            i.BouquetId!.Value,
                            i.SoftToyId!.Value,
                            i.Quantity,
                            i.Price,
                            new GetBouquetDto(
                                i.Bouquet.Id,
                                i.Bouquet.Name,
                                i.Bouquet.Description,
                                i.Bouquet.Price,
                                i.Bouquet.Quantity,
                                i.Bouquet.ImagePath,
                                i.Bouquet.Rating),
                            new GetSoftToyDto(
                                i.SoftToy.Id,
                                i.SoftToy.Name,
                                i.SoftToy.Description,
                                i.SoftToy.Quantity,
                                i.SoftToy.Price,
                                i.SoftToy.ImagePath,
                                i.SoftToy.Rating))).ToList())).ToList())).ToListAsync();

            return Ok(users);
        }

        [HttpPost]
        public async Task<ActionResult<GetUserDto>> CreateUsers([FromBody] CreateUserDto userDto)
        {
            if (await _context.UserDomains.AnyAsync(u => u.Login == userDto.Login))
            {
                return Conflict("Login already exists.");
            }
            var user = new UserDomain
            {
                Id = Guid.NewGuid(),
                Name = userDto.UserName,
                Login = userDto.Login,
            };
            _context.UserDomains.Add(user);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, userDto);
        }

        [HttpPost("many")]
        public async Task<ActionResult> CreateUserMany([FromBody] List<CreateUserDto> users)
        {
            if(users == null || users.Count < 1)
            {
                return BadRequest("Пустой список пользователей.");
            }
            var existUser = await _context.UserDomains
                .Where(u => users.Select(dto => dto.Login).Contains(u.Login))
                .Select(u => u.Login)
                .ToListAsync();

            if (existUser.Count != 0) return Conflict($"Логины уже существуют: {string.Join(", ", existUser)}");

            var newUsers = users.Select(userDto => new UserDomain
            {
                Id = Guid.NewGuid(),
                Name = userDto.UserName,
                Login = userDto.Login,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(userDto.Password, 12),
                DateRegistration = DateTime.UtcNow
            }).ToList();

            await _context.UserDomains.AddRangeAsync(newUsers);
            await _context.SaveChangesAsync();
            return Ok(newUsers);
        }

        [HttpPut]
        public async Task<ActionResult<GetUserDto>> UpdateUser([FromBody] UpdateUserDto userDto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var user = await _context.UserDomains.FirstOrDefaultAsync(u => u.Id == userDto.UserId, ct);
            if (user == null)
                return NotFound($"Пользователь с Id = {userDto.UserId} не найден");

            string? newLoginRaw = userDto.Login?.Trim();
            string? newLoginNorm = newLoginRaw?.ToLowerInvariant();
            string currentLoginNorm = user.Login.Trim().ToLowerInvariant();

            if (!string.IsNullOrEmpty(newLoginNorm) && newLoginNorm != currentLoginNorm)
            {
                bool loginBusy = await _context.UserDomains
                    .AnyAsync(u => u.Id != user.Id && u.Login.ToLower() == newLoginNorm, ct);

                if (loginBusy)
                    return Conflict($"Логин '{newLoginRaw}' уже занят");

                user.Login = newLoginRaw!; 
            }

            if (!string.IsNullOrWhiteSpace(userDto.Name))
                user.Name = userDto.Name!.Trim();

            if (!string.IsNullOrEmpty(userDto.NewPassword) || !string.IsNullOrEmpty(userDto.OldPassword))
            {
                if (string.IsNullOrEmpty(userDto.OldPassword) || string.IsNullOrEmpty(userDto.NewPassword))
                    return BadRequest("Для смены пароля нужны и старый, и новый пароль.");

                if (userDto.NewPassword!.Length < 6)
                    return BadRequest("Длина пароля должна быть больше 6 символов.");

                if (!BCrypt.Net.BCrypt.Verify(userDto.OldPassword!, user.PasswordHash))
                    return BadRequest("Старый пароль указан неверно.");

                if (userDto.OldPassword == userDto.NewPassword)
                    return BadRequest("Новый пароль не должен совпадать со старым.");

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(userDto.NewPassword, workFactor: 12);
            }

            try
            {
                await _context.SaveChangesAsync(ct);
                return Ok();
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.Message?.Contains("23505") == true ||
                    ex.InnerException?.Message?.Contains("2601") == true ||
                    ex.InnerException?.Message?.Contains("2627") == true)
                {
                    return Conflict("Такой логин уже существует.");
                }

                return BadRequest($"Ошибка при обновлении пользователя: {ex.Message}");
            }
        }

        [HttpDelete]
        public async Task<ActionResult> DeleteUsers(string login)
        {
            var user = await _context.UserDomains
                .Include(u => u.Orders) 
                    .ThenInclude(o => o.Items) 
                .FirstOrDefaultAsync(u => u.Login == login);

            if (user == null)
            {
                return NotFound("Такой пользователь не найден.");
            }

            foreach (var order in user.Orders)
            {
                foreach (var orderItem in order.Items)
                {
                    var bouquet = await _context.Bouquets.FirstOrDefaultAsync(b => b.Id == orderItem.BouquetId);
                    if (bouquet != null)
                    {
                        bouquet.Quantity += orderItem.Quantity;
                    }
                }
            }

            _context.UserDomains.Remove(user);
            await _context.SaveChangesAsync();
            return NoContent();
        }


        [HttpDelete("many")]
        public async Task<ActionResult> DeleateUsersMany()
        {
            var userDeleated = await _context.UserDomains.ExecuteDeleteAsync();
            if (userDeleated == 0)
                return NotFound("Список пользователей пуст.");

            return Ok($"{userDeleated} пользователей удалено.");
        }
    }
}