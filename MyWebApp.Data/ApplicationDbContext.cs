using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Models;

namespace MyWebApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Nếu bạn có thêm bảng thì khai báo ở đây
        // public DbSet<Music> Musics { get; set; }
        // public DbSet<Lyric> Lyrics { get; set; }
    }
}
