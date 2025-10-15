using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyWebApp.Controllers.Api
{
    [ApiController]
    [Route("api/author")]
    [Authorize(Roles = "Author")]
    public class AuthorApiController : ControllerBase
    {
        /// <summary>
        /// Trả về thông tin dashboard của tác giả.
        /// </summary>
        [HttpGet("dashboard")]
        [ProducesResponseType(200)]
        public IActionResult GetDashboard()
        {
            return Ok(new { role = "Author", message = "Welcome Author!" });
        }
    }
}
