using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Models;
using MyWebApp.Services;

namespace MyWebApp.Controllers.Api
{
    [ApiController]
    [Route("api/user")]
    public class UserApiController : ControllerBase
    {
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
            RoleManager<IdentityRole> roleManager)
        {
            _musicService = musicService;
            _playlistService = playlistService;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
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
            return Ok(new { success = true, user, roles });
        }

        /// <summary>
        /// Lấy danh sách tất cả bài hát (đăng nhập mới dùng được).
        /// </summary>
        [Authorize]
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
        [Authorize]
        [HttpGet("profile")]
        [ProducesResponseType(typeof(ApplicationUser), 200)]
        public async Task<IActionResult> GetProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            return Ok(user);
        }

        /// <summary>
        /// Cập nhật hồ sơ người dùng.
        /// </summary>
        [Authorize]
        [HttpPut("update-profile")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng." });

            user.FullName = model.Name;
            user.Email = model.Email;
            user.UserName = model.Email;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
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

        public class UpdateProfileRequest
        {
            public string UserId { get; set; } = "";
            public string Name { get; set; } = "";
            public string Email { get; set; } = "";
        }
    }
}
