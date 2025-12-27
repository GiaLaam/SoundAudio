using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MyWebApp.Models;
using MyWebApp.Services;
using MyWebApp.Mvc.Services;
using System.Diagnostics;
using System.Security.Claims;


namespace MyWebApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly MusicApiService _musicApiService;
        private readonly PlaylistApiService _playlistApiService;
        private readonly AlbumApiService _albumApiService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly JwtService _jwtService;
        
        public HomeController(
            ILogger<HomeController> logger,
            MusicApiService musicApiService,
            PlaylistApiService playlistApiService,
            AlbumApiService albumApiService,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            JwtService jwtService)
        {
            _logger = logger;
            _musicApiService = musicApiService;
            _playlistApiService = playlistApiService;
            _albumApiService = albumApiService;
            _jwtService = jwtService;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            var musicFiles = await _musicApiService.GetAllAsync();
            var albums = await _albumApiService.GetAllAsync();
            ViewBag.IsLoggedIn = User.Identity.IsAuthenticated;
            ViewBag.Albums = albums;
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

                // Tạo JWT token và lưu vào session
                var roles = await _userManager.GetRolesAsync(user);
                var token = await _jwtService.GenerateToken(user, roles);
                HttpContext.Session.SetString("JwtToken", token);

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
            var musicFiles = await _musicApiService.GetAllAsync();
            var albums = await _albumApiService.GetAllAsync();
            
            List<MusicFile> songResults;
            List<Album> albumResults;
            
            if (string.IsNullOrEmpty(query))
            {
                songResults = musicFiles;
                albumResults = albums;
            }
            else
            {
                songResults = musicFiles.Where(m => 
                    m.NameSong != null && m.NameSong.Contains(query, StringComparison.OrdinalIgnoreCase)
                ).ToList();
                
                albumResults = albums.Where(a => 
                    a.Name != null && a.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }
            
            ViewBag.SearchQuery = query;
            ViewBag.Albums = albumResults;
            ViewBag.IsLoggedIn = User.Identity?.IsAuthenticated ?? false;
            
            if (User.Identity?.IsAuthenticated == true)
            {
                var ownerId = _userManager.GetUserId(User);
                ViewBag.Playlists = await _playlistApiService.GetByOwnerAsync(ownerId, "user");
            }
            
            return View("Search", songResults);
        }

        [HttpGet]
        public IActionResult CheckLoginStatus()
        {
            var isLoggedIn = User.Identity.IsAuthenticated;
            var userId = isLoggedIn ? _userManager.GetUserId(User) : null;
            return Json(new { isLoggedIn, userId });
        }

        // New Login/Register actions for new views
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ email và mật khẩu.";
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    ViewBag.Error = "Người dùng không tồn tại.";
                    return View();
                }

                // Tạo JWT token và lưu vào session
                var roles = await _userManager.GetRolesAsync(user);
                var token = await _jwtService.GenerateToken(user, roles);
                HttpContext.Session.SetString("JwtToken", token);

                // Check role and redirect
                if (await _userManager.IsInRoleAsync(user, SD.Role_Admin))
                    return RedirectToAction("Dashboard", "Admin");

                return RedirectToAction("Index");
            }

            ViewBag.Error = "Sai email hoặc mật khẩu!";
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string fullName, string email, string password, string confirmPassword)
        {
            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || 
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                ViewBag.Error = "Vui lòng điền đầy đủ thông tin.";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu không khớp!";
                return View();
            }

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                ViewBag.Error = "Email đã được sử dụng!";
                return View();
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                // Assign default role (User)
                await _userManager.AddToRoleAsync(user, SD.Role_User);
                
                ViewBag.Success = "Đăng ký thành công! Vui lòng đăng nhập.";
                return View();
            }

            ViewBag.Error = string.Join(", ", result.Errors.Select(e => e.Description));
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
