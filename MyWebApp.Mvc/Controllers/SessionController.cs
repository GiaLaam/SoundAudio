using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace MyWebApp.Mvc.Controllers
{
    [Authorize]
    [Route("api/session")]
    public class SessionController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;

        public SessionController(UserManager<ApplicationUser> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

        [HttpGet("signalr-token")]
        public async Task<IActionResult> GetSignalRToken()
        {
            Console.WriteLine($"[SessionController] SignalR token requested - IsAuthenticated: {User.Identity?.IsAuthenticated}");
            
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                Console.WriteLine($"[SessionController] User not found!");
                return Json(new { success = false, message = "User not found" });
            }

            Console.WriteLine($"[SessionController] Generating token for user: {user.UserName} (ID: {user.Id})");

            // Create a short-lived token for SignalR only
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? ""),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim("sub", user.Id) // JWT standard claim
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(60), // Short-lived token
                signingCredentials: credentials
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            Console.WriteLine($"[SessionController] ✅ Token generated successfully for {user.UserName}");

            return Json(new
            {
                success = true,
                token = tokenString,
                userId = user.Id,
                userName = user.UserName
            });
        }
    }
}
