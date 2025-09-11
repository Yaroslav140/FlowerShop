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
            var list = await _context.Users
                .Select(u => new GetUserDto(
                    u.Name,
                    u.Email
                )).ToListAsync();
            return Ok(list);
        }

        [HttpGet("{name}")]
        public async Task<ActionResult<List<GetUserDto>>> GetUsersByName(string name)
        {
            var list = await _context.Users
                .Where(u => u.Name == name)
                .ToListAsync();

            return Ok(list);
        }


        [HttpPost]
        public async Task<ActionResult<GetUserDto>> CreateUser([FromBody] CreateUserDto userDto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == userDto.Email))
            {
                return Conflict("Email already exists.");
            }
            var user = new UserDomain
            {
                Id = Guid.NewGuid(),
                Name = userDto.UserName,
                Email = userDto.Email,
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, userDto);
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteUser([FromBody] string email)
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
