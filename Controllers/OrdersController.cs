using FlowerShop.Data;
using FlowerShop.Data.Models;
using FlowerShop.Dto;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace FlowerShop.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController
    {
        private readonly FlowerDbContext _context;
        public OrdersController(FlowerDbContext context) => _context = context;

/*        [HttpGet]
        public async Task<ActionResult<List<OrderDto>>> GetOrders()
        {

        }*/
        
    }
}
