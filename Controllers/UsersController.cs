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
    public class UsersController : ControllerBase
    {
        private readonly FlowerDbContext _context;
        public UsersController(FlowerDbContext context) => _context = context;

        [HttpGet]
        public async Task<ActionResult<List<GetUserDto>>> GetUsers()
        {
            var list = await _context.UserDomains
                .Select(u => new GetUserDto(
                    u.Name,
                    u.Login,
                    u.Orders
                    .Select(o => new GetOrderDto(o.OrderDate, o.TotalAmount))
                    .ToList()
                )).ToListAsync();
            return Ok(list);
        }

        [HttpPost]
        public async Task<ActionResult<GetUserDto>> CreateUser([FromBody] CreateUserDto userDto)
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

            if (existUser.Any()) return Conflict($"Логины уже существуют: {string.Join(", ", existUser)}");

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
        public async Task<ActionResult> DeleteUser(string login)
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
        public async Task<ActionResult> DeleateUserMany()
        {
            var user = await _context.UserDomains.ToListAsync();
            if (user == null || user.Count < 1)
            {
                return NoContent();
            }
            _context.UserDomains.RemoveRange(user);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
