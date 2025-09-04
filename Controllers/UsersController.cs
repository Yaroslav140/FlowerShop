using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto;
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
        public async Task<ActionResult<List<UserDto>>> GetUsers()
        {
            var list = await _context.Users
                .Select(u => new UserDto(
                    u.Name,
                    u.Email,
                    u.OrderCount,
                    u.OrderId
                )).ToListAsync();
            return Ok(list);
        }

        [HttpPost]
        public async Task<ActionResult<UserDto>> CreateUser([FromBody] UserDto userDto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == userDto.Email))
            {
                return Conflict("Email already exists.");
            }
            var user = new UserDomain
            {
                Id = Guid.NewGuid(),
                Name = userDto.Username,
                Email = userDto.Email,
                OrderCount = userDto.OrderCount,
                OrderId = userDto.OrderId
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, userDto);
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteUser([FromQuery] string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                return NotFound("User not found.");
            }
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
