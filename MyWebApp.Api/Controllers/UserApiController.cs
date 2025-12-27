using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using MyWebApp.Models;
using MyWebApp.Services;

namespace MyWebApp.Controllers.Api
{
    [ApiController]
    [Route("api/user")]
    public class UserApiController : ControllerBase
    {
        private readonly JwtService _jwtService;
        private readonly MusicService _musicService;
        private readonly PlaylistService _playlistService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserApiController(
            MusicService musicService,
            PlaylistService playlistService,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            JwtService jwtService)
        {
            _musicService = musicService;
            _playlistService = playlistService;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _jwtService = jwtService;
        }

        /// <summary>
        /// Đăng nhập người dùng.
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _signInManager.PasswordSignInAsync(request.Email, request.Password, true, false);
            if (!result.Succeeded)
                return Unauthorized(new { message = "Sai tài khoản hoặc mật khẩu!" });

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng." });

            var roles = await _userManager.GetRolesAsync(user);
            var token = await _jwtService.GenerateToken(user, roles);
            return Ok(new { success = true, user, roles, token });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest model)
        {
            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
                return BadRequest(new { success = false, message = "Email và mật khẩu không được để trống." });

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
                return BadRequest(new { success = false, message = "Email đã được sử dụng." });

            var newUser = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName ?? ""
            };

            var result = await _userManager.CreateAsync(newUser, model.Password);
            if (result.Succeeded)
            {
                return Ok(new
                {
                    success = true,
                    message = "Đăng ký thành công!",
                    user = newUser
                });
            }

            return BadRequest(new { success = false, errors = result.Errors });
        }

        /// <summary>
        /// Lấy danh sách tất cả bài hát (đăng nhập mới dùng được).
        /// </summary>
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{CookieAuthenticationDefaults.AuthenticationScheme}")]
        [HttpGet("songs")]
        [ProducesResponseType(typeof(IEnumerable<MusicFile>), 200)]
        public async Task<IActionResult> GetAllSongs()
        {
            var songs = await _musicService.GetAllAsync();
            return Ok(songs);
        }

        /// <summary>
        /// Lấy thông tin hồ sơ người dùng hiện tại.
        /// </summary>
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{CookieAuthenticationDefaults.AuthenticationScheme}")]
        [HttpGet("profile")]
        [ProducesResponseType(typeof(ApplicationUser), 200)]
        public async Task<IActionResult> GetProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            return Ok(user);
        }

        /// <summary>
        /// Cập nhật hồ sơ người dùng hiện tại.
        /// </summary>
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{CookieAuthenticationDefaults.AuthenticationScheme}")]
        [HttpPut("update-profile")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng." });

            // Chỉ cho phép user update profile của chính mình
            if (currentUser.Id != model.UserId)
                return Forbid();

            currentUser.FullName = model.Name;
            currentUser.Email = model.Email;
            currentUser.UserName = model.Email;

            var result = await _userManager.UpdateAsync(currentUser);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(currentUser);
                return Ok(new { success = true });
            }
            return BadRequest(new { success = false, errors = result.Errors });
        }

        // DTOs
        public class LoginRequest
        {
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
        }

        public class RegisterRequest
        {
            public string? Email { get; set; }
            public string? Password { get; set; }
            public string? FullName { get; set; }
        }

        public class UpdateProfileRequest
        {
            public string UserId { get; set; } = "";
            public string Name { get; set; } = "";
            public string Email { get; set; } = "";
        }
    }
}
