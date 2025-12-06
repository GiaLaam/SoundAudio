using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Models;
using System.Security.Claims;

namespace MyWebApp.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SessionApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public SessionApiController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet("whoami")]
        [Authorize(AuthenticationSchemes = "Cookies,Bearer")]
        public async Task<IActionResult> WhoAmI()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst("sub")?.Value
                        ?? User.FindFirst(ClaimTypes.Name)?.Value
                        ?? User.Identity?.Name;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "Not authenticated" });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(new
            {
                userId = user.Id,
                userName = user.UserName,
                email = user.Email,
                isAuthenticated = true
            });
        }

        [HttpGet("debug-claims")]
        [Authorize(AuthenticationSchemes = "Cookies,Bearer")]
        public IActionResult DebugClaims()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            var authType = User.Identity?.AuthenticationType;
            var name = User.Identity?.Name;

            return Ok(new
            {
                isAuthenticated,
                authenticationType = authType,
                identityName = name,
                claims
            });
        }
    }
}
