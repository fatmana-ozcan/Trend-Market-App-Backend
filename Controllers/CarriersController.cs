using Microsoft.AspNetCore.Mvc;
using TrendMarketServer.Models;

namespace TrendMarketServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CarriersController : ControllerBase
    {
        // Seçilebilecek kargo firmalarının sabit listesi (herkese açık, giriş gerektirmez).
        [HttpGet]
        public IActionResult GetCarriers() => Ok(Carriers.All);
    }
}
