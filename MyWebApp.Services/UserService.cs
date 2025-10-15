// using Microsoft.EntityFrameworkCore;
// using MyWebApp.Models;
// using System.Collections.Generic;
// using System.Threading.Tasks;

// namespace MyWebApp.Services
// {
//     public class UserService
//     {
//         private readonly MyDbContext _context;

//         public UserService(MyDbContext context)
//         {
//             _context = context;
//         }

//         public async Task<User?> GetByEmailAsync(string email)
//         {
//             return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
//         }

//         public async Task<User?> GetByIdAsync(string id)
//         {
//             return await _context.Users.FindAsync(id);
//         }

//         public async Task CreateAsync(User user)
//         {
//             await _context.Users.AddAsync(user);
//             await _context.SaveChangesAsync();
//         }

//         public async Task UpdateAsync(User user)
//         {
//             _context.Users.Update(user);
//             await _context.SaveChangesAsync();
//         }

//         public async Task DeleteAsync(string id)
//         {
//             var user = await _context.Users.FindAsync(id);
//             if (user != null)
//             {
//                 _context.Users.Remove(user);
//                 await _context.SaveChangesAsync();
//             }
//         }

//         public async Task<List<User>> GetAllAsync()
//         {
//             return await _context.Users.ToListAsync();
//         }
//     }
// }
