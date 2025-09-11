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
    public class BouquetsController : ControllerBase
    {
        private readonly FlowerDbContext _context;
        public BouquetsController(FlowerDbContext context) => _context = context;

        [HttpGet]
        public async Task<ActionResult<List<GetBouquetDto>>> GetBouquets()
        {
            var bouquets = await _context.Bouquets.ToListAsync();
            return Ok(bouquets);
        }

        [HttpPost]
        public async Task<ActionResult<GetBouquetDto>> Create([FromBody] CreateBouquetDto bouquet)
        {
            if (string.IsNullOrWhiteSpace(bouquet.NameBouquet))
                return BadRequest("Name is required.");

            var entity = new BouquetEntity
            {
                Id = Guid.NewGuid(), 
                Name = bouquet.NameBouquet,
                Description = bouquet.DescriptionBouquet,
                Price = bouquet.PriceBouquet,
                Stock = bouquet.Stock,
                ImageUrl = bouquet.ImageUrl
            };

            _context.Bouquets.Add(entity);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBouquetDto), new { id = entity.Id }, entity);
        }
    }
}
