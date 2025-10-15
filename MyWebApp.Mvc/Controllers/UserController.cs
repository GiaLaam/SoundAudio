using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Models;
using MyWebApp.Services;
using Microsoft.AspNetCore.Identity;

namespace MyWebApp.Controllers
{
    public class UserController : Controller
    {
        private readonly ILogger<UserController> _logger;
        private readonly MusicService _musicService;
        private readonly PlaylistService _playlistService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserController(
            ILogger<UserController> logger,
            MusicService musicService,
            PlaylistService playlistService,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager)
        {
            _logger = logger;
            _musicService = musicService;
            _playlistService = playlistService;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
        }

        [HttpPost]
        public async Task<IActionResult> DangNhap(string email, string password)
        {
            var result = await _signInManager.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    ViewBag.Error = "Người dùng không tồn tại.";
                    return View();
                }

                // ✅ Kiểm tra vai trò và redirect tương ứng
                if (await _userManager.IsInRoleAsync(user, SD.Role_Admin))
                    return RedirectToAction("Dashboard", "Admin");

                if (await _userManager.IsInRoleAsync(user, SD.Role_Author))
                    return RedirectToAction("Author", "Home");

                return RedirectToAction("NguoiDung");
            }

            ViewBag.Error = "Sai tài khoản hoặc mật khẩu!";
            return View();
        }
        public async Task<IActionResult> NguoiDung()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("DangNhap");

            var ownerId = _userManager.GetUserId(User);
            var musicFiles = await _musicService.GetAllAsync();
            var playlists = await _playlistService.GetByOwnerAsync(ownerId, "user");

            ViewBag.IsLoggedIn = true;
            ViewBag.ViewMode = "Default";
            ViewBag.Playlists = playlists;

            return View(musicFiles);
        }

        public async Task<IActionResult> Profile()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("DangNhap");

            var ownerId = _userManager.GetUserId(User);
            var user = await _userManager.GetUserAsync(User);
            var musicFiles = await _musicService.GetAllAsync();
            var playlists = await _playlistService.GetByOwnerAsync(ownerId, "user");

            ViewBag.ViewMode = "Profile";
            ViewBag.IsLoggedIn = true;
            ViewBag.UserData = user;
            ViewBag.Playlists = playlists;

            return View("NguoiDung", musicFiles);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(string userId, string name, string email, string currentPassword, string newPassword, string confirmPassword)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy người dùng." });

            user.Email = email;
            user.UserName = email;
            user.FullName = name; // Nếu bạn có trường FullName

            IdentityResult result;

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                if (string.IsNullOrWhiteSpace(currentPassword))
                    return Json(new { success = false, message = "Vui lòng nhập mật khẩu hiện tại." });

                if (newPassword != confirmPassword)
                    return Json(new { success = false, message = "Mật khẩu xác nhận không khớp." });

                result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
                if (!result.Succeeded)
                    return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }

            result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                return Json(new { success = true });
            }

            return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }


        [HttpPost]
        public async Task<IActionResult> SaveProfile(string Name, string Email, string Password, string RePassword)
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("DangNhap");

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("DangNhap");

            // Đổi tên và email
            user.FullName = Name;
            user.Email = Email;
            user.UserName = Email;

            if (!string.IsNullOrEmpty(Password))
            {
                if (Password != RePassword)
                {
                    ViewBag.Error = "Mật khẩu không khớp.";
                    ViewBag.ViewMode = "Profile";
                    ViewBag.UserData = user;
                    var musicFiles = await _musicService.GetAllAsync();
                    var playlists = await _playlistService.GetByOwnerAsync(user.Id, "user");
                    ViewBag.Playlists = playlists;
                    return View("NguoiDung", musicFiles);
                }

                // Đổi mật khẩu an toàn: cần yêu cầu mật khẩu hiện tại
                // Nếu không có, phải đặt lại qua token → nhưng ở đây ta hash trực tiếp (chỉ nếu bạn là admin hoặc đổi không yêu cầu currentPassword)
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, Password);

                if (!result.Succeeded)
                {
                    ViewBag.Error = "Lỗi khi đổi mật khẩu: " + string.Join(", ", result.Errors.Select(e => e.Description));
                    ViewBag.ViewMode = "Profile";
                    ViewBag.UserData = user;
                    var musicFiles = await _musicService.GetAllAsync();
                    var playlists = await _playlistService.GetByOwnerAsync(user.Id, "user");
                    ViewBag.Playlists = playlists;
                    return View("NguoiDung", musicFiles);
                }
            }

            var updateResult = await _userManager.UpdateAsync(user);

            if (updateResult.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                return RedirectToAction("Profile");
            }
            else
            {
                ViewBag.Error = "Lỗi cập nhật thông tin: " + string.Join(", ", updateResult.Errors.Select(e => e.Description));
                ViewBag.ViewMode = "Profile";
                ViewBag.UserData = user;
                var musicFiles = await _musicService.GetAllAsync();
                var playlists = await _playlistService.GetByOwnerAsync(user.Id, "user");
                ViewBag.Playlists = playlists;
                return View("NguoiDung", musicFiles);
            }
        }


        public async Task<IActionResult> Search(string query)
        {
            var musicFiles = await _musicService.GetAllAsync();
            var results = string.IsNullOrEmpty(query)
                ? musicFiles
                : musicFiles.Where(m => m.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            ViewBag.SearchResults = results;
            ViewBag.ViewMode = "Search";

            if (User.Identity.IsAuthenticated)
            {
                ViewBag.IsLoggedIn = true;
                var ownerId = _userManager.GetUserId(User);
                ViewBag.Playlists = await _playlistService.GetByOwnerAsync(ownerId, "user");
                return View("~/Views/User/NguoiDung.cshtml", results);
            }
            else
            {
                ViewBag.IsLoggedIn = false;
                return View("Index", results);
            }
        }

        public async Task<IActionResult> Playlist()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("DangNhap");

            var ownerId = _userManager.GetUserId(User);
            var playlists = await _playlistService.GetByOwnerAsync(ownerId, "user");
            var musicFiles = await _musicService.GetAllAsync();

            ViewBag.ViewMode = "Playlist";
            ViewBag.IsLoggedIn = true;
            ViewBag.Playlists = playlists;

            return View("NguoiDung", musicFiles);
        }

        [HttpPost]
        [ActionName("AddToPlaylist")]
        public async Task<IActionResult> AddToPlaylist(string songId, string? playlistId, string? newPlaylistName)
        {
            var ownerId = _userManager.GetUserId(User);
            _logger.LogInformation("AddToPlaylist called: ownerId={ownerId}, songId={songId}, playlistId={playlistId}, newPlaylistName={newPlaylistName}", ownerId, songId, playlistId, newPlaylistName);

            if (string.IsNullOrEmpty(ownerId) || string.IsNullOrEmpty(songId))
                return Json(new { success = false, message = "Thông tin không hợp lệ!" });

            try
            {
                if (!string.IsNullOrEmpty(playlistId))
                {
                    var playlist = await _playlistService.GetByIdAsync(playlistId);
                    if (playlist == null || playlist.OwnerId != ownerId || playlist.OwnerType != "user")
                        return Json(new { success = false, message = "Playlist không thuộc về bạn!" });

                    if (!playlist.MusicIds.Contains(songId))
                    {
                        playlist.MusicIds.Add(songId);
                        await _playlistService.UpdateAsync(playlist);
                    }

                    return Json(new { success = true, message = "Đã thêm bài hát vào playlist!" });
                }
                else if (!string.IsNullOrEmpty(newPlaylistName))
                {
                    var newPlaylist = new Playlist
                    {
                        OwnerId = ownerId,
                        OwnerType = "user",
                        Name = newPlaylistName,
                        MusicIds = new List<string> { songId }
                    };

                    await _playlistService.CreateAsync(newPlaylist);

                    return Json(new { success = true, message = "Đã tạo playlist mới và thêm bài hát!" });
                }

                return Json(new { success = false, message = "Vui lòng chọn playlist hoặc nhập tên mới!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm bài hát vào playlist.");
                return Json(new { success = false, message = "Đã xảy ra lỗi. Vui lòng thử lại sau." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateName(string id, string newName)
        {
            var playlist = await _playlistService.GetByIdAsync(id);
            if (playlist == null)
                return Json(new { success = false, message = "Playlist không tồn tại." });

            playlist.Name = newName;
            await _playlistService.UpdateAsync(playlist);

            return Json(new { success = true });
        }
    }
}