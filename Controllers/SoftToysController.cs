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
    public class SoftToysController(FlowerDbContext context, IWebHostEnvironment env) : ControllerBase
    {
        private readonly FlowerDbContext _context = context;
        private readonly IWebHostEnvironment _env = env;

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

        [HttpGet("search")]
        public async Task<ActionResult<List<GetSoftToyDto>>> SearchSoftToys([FromQuery] string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return await GetSoftToys();

            var softToys = await _context.SoftToys
                .Where(s => EF.Functions.Like(s.Name, $"%{name}%"))
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

        [HttpPost]
        public async Task<ActionResult> CreateSoftToy([FromBody] CreateSoftToyDto softToy)
        {
            if (softToy == null)
                return BadRequest("Нет данных.");

            if (string.IsNullOrWhiteSpace(softToy.Name))
                return BadRequest("Имя мягкой игрушки пустое.");

            var exitsSoftToy = await _context.SoftToys
                .Where(n => n.Name == softToy.Name)
                .FirstOrDefaultAsync();

            if (exitsSoftToy != null)
                return BadRequest("Такая мягкая игрушка есть.");

            if (softToy.Quantity < 0)
                return BadRequest("Такое количество не может быть на складе.");

            var entitySoftToy = new SoftToyEntity
            {
                Name = softToy.Name,
                Description = softToy.Description,
                Quantity = softToy.Quantity,
                Price = softToy.Price,
                ImagePath = softToy.ImagePath,
                Rating = 0
            };

            _context.SoftToys.Add(entitySoftToy);
            await _context.SaveChangesAsync();
            return Ok(softToy);
        }

        [HttpPost("many")]
        public async Task<ActionResult> CreateSoftToys([FromBody] List<CreateSoftToyDto> softToys)
        {
            if (softToys.Any(s => string.IsNullOrWhiteSpace(s.Name)))
                return BadRequest("Некоторые имена игрушек пустые.");

            var names = softToys.Select(s => s.Name).ToList();

            if (_context.SoftToys.Any(db => names.Contains(db.Name)))
                return BadRequest("Некоторые имена уже существуют в базе.");

            var entities = softToys.Select(dto => new SoftToyEntity
            {
                Name = dto.Name,
                Description = dto.Description,
                Quantity = dto.Quantity,
                Price = dto.Price,
                ImagePath = dto.ImagePath,
                Rating = 0
            });

            _context.SoftToys.AddRange(entities);
            await _context.SaveChangesAsync();
            return Ok(softToys);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult> UpdateSoftToy(Guid id, [FromBody] UpdateBouquetDto dto)
        {
            var existing = await _context.SoftToys
                .Where(s => s.Id == id)
                .FirstOrDefaultAsync();

            if (existing == null)
                return BadRequest("Мягкая игрушка не найдена.");

            existing.Name = dto.Name;
            existing.Description = dto.Description;
            existing.Price = dto.Price;
            existing.Quantity = dto.Quantity;
            existing.ImagePath = dto.ImagePath;

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
                var folder = Path.Combine(_env.WebRootPath, "uploads", "softToys");
                Directory.CreateDirectory(folder);

                var fileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(folder, fileName);

                await using var stream = System.IO.File.Create(filePath);
                await file.CopyToAsync(stream);

                return Ok($"/uploads/softToys/{fileName}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Ошибка сохранения файла: {ex.Message}");
            }
        }

        [HttpDelete]
        public async Task<ActionResult> DeleteSoftToy(string name)
        {
            var softToy = await _context.SoftToys.FirstOrDefaultAsync(s => s.Name == name);
            if (softToy == null)
                return NotFound("Мягкая игрушка не найдена.");

            _context.SoftToys.Remove(softToy);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("all")]
        public async Task<ActionResult> DeleteAllSoftToys()
        {
            var deleted = await _context.SoftToys.ExecuteDeleteAsync();

            if (deleted == 0)
                return NotFound("Мягких игрушек не найдено.");

            return Ok($"{deleted} игрушек удалено.");
        }
    }
}
