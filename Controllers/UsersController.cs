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

        [HttpDelete]
        public async Task<IActionResult> DeleteUser(string login)
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
    }
}
