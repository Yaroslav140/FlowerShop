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
    public class BouquetsController(FlowerDbContext context, IWebHostEnvironment env) : ControllerBase
    {
        private readonly FlowerDbContext _context = context;
        private readonly IWebHostEnvironment _env = env;
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
                    b.ImagePath,
                    b.Rating
                    )).ToListAsync();
            return Ok(bouquets);
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<GetBouquetDto>>> SearchBouquets([FromQuery] string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return await GetBouquets();

            var bouquets = await _context.Bouquets
                .Where(b => EF.Functions.Like(b.Name, $"%{name}%"))
                .Select(b => new GetBouquetDto(
                    b.Id,
                    b.Name,
                    b.Description,
                    b.Price,
                    b.Quantity,
                    b.ImagePath,
                    b.Rating
                ))
                .ToListAsync();

            return Ok(bouquets);
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
                ImagePath = bouquet.ImagePath
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

            var entities = bouquetDtos.Select(dto => new BouquetEntity
            {
                Name = dto.NameBouquet,
                Price = dto.PriceBouquet,
                Description = dto.DescriptionBouquet,
                Quantity = dto.Quantity,
                ImagePath = dto.ImagePath

            }).ToList();

            _context.Bouquets.AddRange(entities);
            await _context.SaveChangesAsync();
            return Ok(bouquetDtos);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult> UpdateBouquet(Guid id, [FromBody] UpdateBouquetDto dtoBouquet)
        {
            var existingBouquet = await _context.Bouquets
                .Where(i => i.Id == id)
                .FirstOrDefaultAsync();

            if (existingBouquet == null)
                return BadRequest("Букет не найден.");

            var oldImagePath = existingBouquet.ImagePath;

            _context.Entry(existingBouquet).CurrentValues.SetValues(dtoBouquet);

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("upload-image")]
        public async Task<ActionResult<string>> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Файл не выбран.");

            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext))
                return BadRequest("Допустимы только jpg, jpeg, png, webp.");

            try
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads", "bouquets");
                Directory.CreateDirectory(folder);

                var fileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(folder, fileName);

                await using var stream = System.IO.File.Create(filePath);
                await file.CopyToAsync(stream);

                return Ok($"/uploads/bouquets/{fileName}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Ошибка сохранения файла: {ex.Message}");
            }
        }

        [HttpDelete]
        public async Task<ActionResult> DeleteBouquet(string Name)
        {
            var bouquet = await _context.Bouquets.FirstOrDefaultAsync(b => b.Name == Name);
            if (bouquet == null)
                return NotFound("Букет не найден.");

            _context.Bouquets.Remove(bouquet);
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
