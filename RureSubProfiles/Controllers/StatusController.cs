using Microsoft.AspNetCore.Mvc;

namespace RureSubProfiles.Controllers
{
    public class StatusController : Controller
    {
        [HttpGet("/status")]
        public IActionResult GetStatus()
        {
            return Ok("Server is working!");
        }

    }
}
