using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyWebApp.Models;
using MyWebApp.Data; // Add this line if ApplicationDbContext is in MyWebApp.Data namespace


public class EFUserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public EFUserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ApplicationUser>> GetUsersAsync()
    {
        return await _context.Users.ToListAsync();
    }

    public async Task<ApplicationUser> GetUserByIdAsync(string id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task AddUserAsync(ApplicationUser user)
    {
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateUserAsync(ApplicationUser user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(string id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }
}
