using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Models;
using MyWebApp.Services;
using MyWebApp.Mvc.Services;
using Microsoft.AspNetCore.Identity;

namespace MyWebApp.Controllers
{
    public class UserController : Controller
    {
        private readonly ILogger<UserController> _logger;
        private readonly MusicApiService _musicApiService;
        private readonly PlaylistApiService _playlistApiService;
        private readonly AlbumApiService _albumApiService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly PlaylistService _playlistService; // Direct DB access

        public UserController(
            ILogger<UserController> logger,
            MusicApiService musicApiService,
            PlaylistApiService playlistApiService,
            AlbumApiService albumApiService,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            PlaylistService playlistService)
        {
            _logger = logger;
            _musicApiService = musicApiService;
            _playlistApiService = playlistApiService;
            _albumApiService = albumApiService;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _playlistService = playlistService;
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
            var musicFiles = await _musicApiService.GetAllAsync();
            var playlists = await _playlistApiService.GetByOwnerAsync(ownerId, "user");
            var albums = await _albumApiService.GetAllAsync();

            ViewBag.IsLoggedIn = true;
            ViewBag.ViewMode = "Default";
            ViewBag.Playlists = playlists;
            ViewBag.Albums = albums;

            return View(musicFiles);
        }

        [Authorize]
        public async Task<IActionResult> Profile()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string fullName, string currentPassword, string newPassword, string confirmNewPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                ViewBag.Error = "Người dùng không tồn tại.";
                return View("Profile");
            }

            // Update full name
            if (!string.IsNullOrEmpty(fullName))
            {
                user.FullName = fullName;
            }

            // Update password if provided
            if (!string.IsNullOrEmpty(currentPassword) && !string.IsNullOrEmpty(newPassword))
            {
                if (newPassword != confirmNewPassword)
                {
                    ViewBag.Error = "Mật khẩu mới không khớp!";
                    return View("Profile");
                }

                var changePasswordResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
                if (!changePasswordResult.Succeeded)
                {
                    ViewBag.Error = "Mật khẩu hiện tại không đúng hoặc mật khẩu mới không hợp lệ!";
                    return View("Profile");
                }
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                ViewBag.Success = "Cập nhật thông tin thành công!";
            }
            else
            {
                ViewBag.Error = "Có lỗi xảy ra khi cập nhật thông tin.";
            }

            return View("Profile");
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
                    var musicFiles = await _musicApiService.GetAllAsync();
                    var playlists = await _playlistApiService.GetByOwnerAsync(user.Id, "user");
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
                    var musicFiles = await _musicApiService.GetAllAsync();
                    var playlists = await _playlistApiService.GetByOwnerAsync(user.Id, "user");
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
                var musicFiles = await _musicApiService.GetAllAsync();
                var playlists = await _playlistApiService.GetByOwnerAsync(user.Id, "user");
                ViewBag.Playlists = playlists;
                return View("NguoiDung", musicFiles);
            }
        }


        public async Task<IActionResult> Search(string query)
        {
            var musicFiles = await _musicApiService.GetAllAsync();
            var results = string.IsNullOrEmpty(query)
                ? musicFiles
                : musicFiles.Where(m => m.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            ViewBag.SearchResults = results;
            ViewBag.ViewMode = "Search";

            if (User.Identity.IsAuthenticated)
            {
                ViewBag.IsLoggedIn = true;
                var ownerId = _userManager.GetUserId(User);
                ViewBag.Playlists = await _playlistApiService.GetByOwnerAsync(ownerId, "user");
                return View("~/Views/User/NguoiDung.cshtml", results);
            }
            else
            {
                ViewBag.IsLoggedIn = false;
                return View("Index", results);
            }
        }

        [Authorize]
        public async Task<IActionResult> Playlist()
        {
            var ownerId = _userManager.GetUserId(User);
            // Use direct database access instead of API
            var playlists = await _playlistService.GetByOwnerAsync(ownerId, "user");
            return View(playlists);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePlaylist(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                TempData["Error"] = "Tên playlist không được để trống.";
                return RedirectToAction("Playlist");
            }

            var ownerId = _userManager.GetUserId(User);
            var playlist = new Playlist
            {
                Name = name,
                OwnerId = ownerId,
                OwnerType = "user",
                MusicIds = new List<string>()
            };

            try
            {
                await _playlistApiService.CreateAsync(playlist);
                TempData["Success"] = "Tạo playlist thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Có lỗi xảy ra: {ex.Message}";
            }

            return RedirectToAction("Playlist");
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
                    var playlist = await _playlistApiService.GetByIdAsync(playlistId);
                    if (playlist == null || playlist.OwnerId != ownerId || playlist.OwnerType != "user")
                        return Json(new { success = false, message = "Playlist không thuộc về bạn!" });

                    if (!playlist.MusicIds.Contains(songId))
                    {
                        playlist.MusicIds.Add(songId);
                        await _playlistApiService.UpdateAsync(playlist);
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

                    await _playlistApiService.CreateAsync(newPlaylist);

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
            var playlist = await _playlistApiService.GetByIdAsync(id);
            if (playlist == null)
                return Json(new { success = false, message = "Playlist không tồn tại." });

            playlist.Name = newName;
            await _playlistApiService.UpdateAsync(playlist);

            return Json(new { success = true });
        }
    }
}