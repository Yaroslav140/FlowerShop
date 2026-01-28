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
    public class SoftToysController(FlowerDbContext context) : Controller
    {
        private readonly FlowerDbContext _context = context;

        [HttpGet]
        public async Task<ActionResult<List<GetSoftToyDto>>> GetSoftToys()
        {
            var softToys = await _context.SoftToys
                .Select(s => new GetSoftToyDto(
                    s.Id,
                    s.Name,
                    s.Description,
                    s.Quantity,
                    s.Price,
                    s.ImagePath,
                    s.Rating))
                .ToListAsync();

            return Ok(softToys);
        }

        [HttpPost("many")]
        public async Task<ActionResult> CreateSoftToys([FromBody]List<CreateSoftToyDto> softToys)
        {
            if(softToys.Any(s => string.IsNullOrWhiteSpace(s.Name)))
                return BadRequest("Некоторые имена букетов пустые.");

            var names = softToys.Select(s => s.Name).ToList();

            if(_context.SoftToys.Any(db => names.Contains(db.Name)))
                return BadRequest("Некоторые имена уже существуют в базе.");

            var entityes = softToys.Select(dto => new SoftToyEntity
            {
                Name = dto.Name,
                Description = dto.Description,
                Quantity = dto.Quantity,
                Price = dto.Price,
                ImagePath = dto.ImagePath,
                Rating = 0
            });

            _context.SoftToys.AddRange(entityes);
            await _context.SaveChangesAsync();
            return Ok(softToys);
        }


        [HttpDelete("all")]
        public async Task<ActionResult> DeleteSoftToys()
        {
            var deleted = await _context.SoftToys.ExecuteDeleteAsync();

            if (deleted == 0)
                return NotFound("Мягких игрушек не найдено.");

            return Ok(deleted);
        }
    }
}
