// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Identity;
// using Microsoft.AspNetCore.Mvc;
// using System.Security.Claims;
// using MyWebApp.Models;

// [AllowAnonymous]
// public class AccountController : Controller
// {
//     private readonly SignInManager<ApplicationUser> _signInManager;
//     private readonly UserManager<ApplicationUser> _userManager;

//     public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
//     {
//         _signInManager = signInManager;
//         _userManager = userManager;
//     }

//     [HttpGet]
//     public IActionResult ExternalLogin(string provider, string? returnUrl = null)
//     {
//         var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl });
//         var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
//         return Challenge(properties, provider);
//     }

//     [HttpGet]
//     [AllowAnonymous]
//     public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
//     {
//         returnUrl ??= Url.Content("~/");

//         if (remoteError != null)
//         {
//             ModelState.AddModelError(string.Empty, $"Lỗi đăng nhập: {remoteError}");
//             return RedirectToAction("DangNhap", "Home");
//         }

//         var info = await _signInManager.GetExternalLoginInfoAsync();
//         if (info == null)
//         {
//             return RedirectToAction("DangNhap", "Home");
//         }

//         var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
//         if (result.Succeeded)
//         {
//             return LocalRedirect(returnUrl); // ✅ chuyển hướng đúng trang sau đăng nhập
//         }

//         // Tạo tài khoản mới nếu chưa có
//         var email = info.Principal.FindFirstValue(ClaimTypes.Email);
//         var name = info.Principal.FindFirstValue(ClaimTypes.Name);

//         var user = new ApplicationUser { UserName = email, Email = email, FullName = name };
//         var createResult = await _userManager.CreateAsync(user);

//         if (createResult.Succeeded)
//         {
//             await _userManager.AddLoginAsync(user, info);
//             await _signInManager.SignInAsync(user, isPersistent: false);
//             return LocalRedirect(returnUrl); // ✅ chuyển hướng sau khi tạo tài khoản mới
//         }

//         // Nếu lỗi tạo tài khoản
//         foreach (var error in createResult.Errors)
//         {
//             ModelState.AddModelError(string.Empty, error.Description);
//         }

//         return RedirectToAction("DangNhap", "Home");
//     }
// }

