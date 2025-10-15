using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyWebApp.Controllers
{
    [Authorize(Roles = "Author")]
    public class AuthorController : Controller
    {
        public IActionResult Dashboard()
        {
            ViewBag.Role = "Author";
            return View();
        }
    }
}
