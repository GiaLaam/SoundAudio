using Microsoft.AspNetCore.Identity;

namespace MyWebApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
    }
}
