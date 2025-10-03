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
    public class BouquetsController(FlowerDbContext context) : ControllerBase
    {
        private readonly FlowerDbContext _context = context;

        [HttpGet]
        public async Task<ActionResult<List<GetBouquetDto>>> GetBouquets()
        {
            var bouquets = await _context.Bouquets
                .Select(b => new GetBouquetDto(
                    b.Id,
                    b.Name,
                    b.Description,
                    b.Price,
                    b.Quantity,
                    b.ImageUrl
                    )).ToListAsync();
            return Ok(bouquets);
        }
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<BouquetEntity>> GetBouquetId(Guid id)
        {
            var bouquet = await _context.Bouquets.Where(i => i.Id == id).FirstOrDefaultAsync();
            return bouquet is null ? NoContent() : Ok(bouquet);
        }
        [HttpPost]
        public async Task<ActionResult<GetBouquetDto>> CreateBouquet([FromBody] CreateBouquetDto bouquet)
        {
            if (string.IsNullOrWhiteSpace(bouquet.NameBouquet) && _context.Bouquets.Any(n => n.Name == bouquet.NameBouquet))
                return BadRequest("Ошибка в имени.");

            var entity = new BouquetEntity
            {
                Id = Guid.NewGuid(),
                Name = bouquet.NameBouquet,
                Description = bouquet.DescriptionBouquet,
                Price = bouquet.PriceBouquet,
                Quantity = bouquet.Quantity,
                ImageUrl = bouquet.ImageUrl
            };

            _context.Bouquets.Add(entity);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("many")]
        public async Task<ActionResult<List<GetBouquetDto>>> CreateBouquetsMany([FromBody] List<CreateBouquetDto> bouquetDtos)
        {
            if (bouquetDtos.Any(n => string.IsNullOrWhiteSpace(n.NameBouquet)))
                return BadRequest("Некоторые имена букетов пустые.");

            var dtoNames = bouquetDtos.Select(x => x.NameBouquet).ToList();

            if (_context.Bouquets.Any(db => dtoNames.Contains(db.Name)))
                return BadRequest("Некоторые имена уже существуют в базе.");

            var lisEntity = new List<CreateBouquetDto>();
            var entities = bouquetDtos.Select(dto => new BouquetEntity
            {
                Name = dto.NameBouquet,
                Price = dto.PriceBouquet,
                Description = dto.DescriptionBouquet,
                Quantity = dto.Quantity,
                ImageUrl = dto.ImageUrl
                
            }).ToList();

            _context.Bouquets.AddRange(entities);
            await _context.SaveChangesAsync();

            return Ok();

        }
        [HttpDelete]
        public async Task<ActionResult> DeleateBouquet(string Name)
        {
            var bouquet = await _context.Bouquets.FirstOrDefaultAsync(b => b.Name == Name);
            if (bouquet == null)
            {
                return NotFound("Букет не найден.");
            }
            _context.Bouquets.Remove(bouquet);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("many")]
        public async Task<ActionResult> DeleateBouquetsMany([FromBody]string[] names)
        {
            var bouquets = await _context.Bouquets
                .Where(n => names.Contains(n.Name))
                .ToListAsync();
            if (bouquets.Count == 0)
            {
                return NotFound("Букет не найден.");
            }
            _context.Bouquets.RemoveRange(bouquets);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("all")]
        public async Task<ActionResult> DeleteAllBouquets()
        {
            await _context.CartItems.ExecuteDeleteAsync();

            var deleted = await _context.Bouquets.ExecuteDeleteAsync();

            if (deleted == 0)
                return NotFound("Букеты не найдены.");

            return Ok($"{deleted} букетов удалено вместе с позициями в корзине.");
        }

    }
}
