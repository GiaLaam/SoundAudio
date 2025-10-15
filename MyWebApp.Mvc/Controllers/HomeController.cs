using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MyWebApp.Models;
using MyWebApp.Services;
using System.Diagnostics;
using System.Security.Claims;


namespace MyWebApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly MusicService _musicService;
        private readonly PlaylistService _playlistService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        public HomeController(
            ILogger<HomeController> logger,
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

        public async Task<IActionResult> Index()
        {
            var musicFiles = await _musicService.GetAllAsync();
            ViewBag.IsLoggedIn = User.Identity.IsAuthenticated;
            return View(musicFiles);
        }

        [HttpGet]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Home", new { ReturnUrl = returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            if (remoteError != null)
            {
                ModelState.AddModelError(string.Empty, $"Lỗi từ nhà cung cấp: {remoteError}");
                return RedirectToAction("DangNhap");
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return RedirectToAction("DangNhap");
            }

            // Đã từng đăng nhập bằng Google
            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (signInResult.Succeeded)
            {
                return RedirectToAction("NguoiDung", "User");
            }

            // ✅ Nếu chưa có tài khoản, tạo mới
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = info.Principal.FindFirstValue(ClaimTypes.Name),
                AvatarUrl = info.Principal.FindFirstValue("picture") ?? ""  // Nếu có
            };

            var result = await _userManager.CreateAsync(user);
            if (result.Succeeded)
            {
                await _userManager.AddLoginAsync(user, info);
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("NguoiDung", "User");
            }

            TempData["Error"] = "Không thể tạo tài khoản từ Google.";
            return RedirectToAction("DangNhap");
        }


        [HttpGet]
        public IActionResult DangNhap()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("NguoiDung", "User");


            return View();
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

                return RedirectToAction("NguoiDung", "User");
            }

            ViewBag.Error = "Sai tài khoản hoặc mật khẩu!";
            return View();
        }

        public async Task<IActionResult> DangKy()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("NguoiDung", "User");


            var roles = _roleManager.Roles.Select(r => r.Name).ToList();
            ViewBag.Roles = SD.Roles; // Gửi danh sách role
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> DangKy(string name, string email, string password, string repassword, string role)
        {
            if (password != repassword)
            {
                ViewBag.Error = "Mật khẩu nhập lại không khớp!";
                ViewBag.Roles = SD.Roles;
                return View();
            }

            // ✅ Kiểm tra role có tồn tại không
            if (!SD.Roles.Contains(role))
            {
                ViewBag.Error = "Vai trò không hợp lệ!";
                ViewBag.Roles = SD.Roles;
                return View();
            }

            // ✅ Kiểm tra email đã tồn tại chưa
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                ViewBag.Error = "Email đã được sử dụng!";
                ViewBag.Roles = SD.Roles;
                return View();
            }

            // ✅ Tạo user mới
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = name,
                AvatarUrl = "/images/default-avatar.png" // Đặt ảnh đại diện mặc định
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);
                await _signInManager.SignInAsync(user, isPersistent: false);

                return role switch
                {
                    SD.Role_Admin => RedirectToAction("Dashboard", "Admin"),
                    SD.Role_Author => RedirectToAction("Author", "Home"),
                    _ => RedirectToAction("NguoiDung", "User"),

                };
            }

            // ✅ Log lỗi chi tiết ra Console & ViewBag
            var errorMessages = result.Errors.Select(e => e.Description).ToList();
            foreach (var err in errorMessages)
            {
                Console.WriteLine($"❌ Identity Error: {err}");
            }

            ViewBag.Error = "Đăng ký thất bại: " + string.Join("; ", errorMessages);
            ViewBag.Roles = SD.Roles;
            return View();
        }



        public async Task<IActionResult> DangXuat()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Search(string query)
        {
            var musicFiles = await _musicService.GetAllAsync();
            var results = string.IsNullOrEmpty(query)
                ? musicFiles
                : musicFiles.Where(m => m.NameSong.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

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

        [HttpGet]
        public IActionResult CheckLoginStatus()
        {
            var isLoggedIn = User.Identity.IsAuthenticated;
            var userId = isLoggedIn ? _userManager.GetUserId(User) : null;
            return Json(new { isLoggedIn, userId });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
