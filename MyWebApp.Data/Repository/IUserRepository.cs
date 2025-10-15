using MyWebApp.Models;

public interface IUserRepository
{
    Task<IEnumerable<ApplicationUser>> GetUsersAsync();
    Task<ApplicationUser?> GetUserByIdAsync(string id);
    Task AddUserAsync(ApplicationUser user);
    Task UpdateUserAsync(ApplicationUser user);
    Task DeleteUserAsync(string id);
}
