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
                    u.Orders
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
                                i.Bouquet.ImageUrl))).ToList())).ToList())).ToListAsync();
            return Ok(users);
        }

        [HttpGet("count")]
        public async Task<ActionResult> GetUsersCount()
        {
            var user = await _context.UserDomains.ToListAsync();
            return Ok($"Пользователей {user.Count}");
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GetUserDto>> GetUsersGuid(Guid id)
        {
            var user = await _context.UserDomains.Where(i => i.Id == id).FirstOrDefaultAsync();
            if (user is null)
                return NoContent();
            return Ok($"Пользователе {user.Login}");
        }

        [HttpGet("full info")]
        public async Task<ActionResult<List<UserDomain>>> GetUsersFullInfo()
        {
            var users = await _context.UserDomains.Include(u => u.Orders).ThenInclude(o => o.Items).ThenInclude(i => i.Bouquet).ToListAsync();
            var result = users.Select(u => new
            {
                u.Id,
                u.Name,
                u.Login,
                u.PasswordHash

            }).ToList();
            return Ok(result);
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

        [HttpDelete]
        public async Task<ActionResult> DeleteUsers(string login)
        {
            var user = await _context.UserDomains.FirstOrDefaultAsync(u => u.Login == login);
            if (user == null)
            {
                return NotFound("User not found.");
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
