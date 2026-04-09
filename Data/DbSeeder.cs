using Microsoft.EntityFrameworkCore;
using DormitoryManagementSystem.Models;

namespace DormitoryManagementSystem.Data
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext context)
        {
            // Create database if it doesn't exist, preserve existing data
            // context.Database.EnsureDeleted(); // (Uncomment only if you want to clear all data)
            context.Database.EnsureCreated();

            // If no roles exist in the database, add the 3 main roles
            if (!context.Roles.Any())
            {
                context.Roles.AddRange(
                    new Role { RoleName = "Admin" },
                    new Role { RoleName = "Staff" },
                    new Role { RoleName = "Student" }
                );
                context.SaveChanges();
            }

            // If no users exist, create an initial "Admin" account (Password: 12345)
            if (!context.Users.Any())
            {
                var adminRole = context.Roles.FirstOrDefault(r => r.RoleName == "Admin");
                if (adminRole != null)
                {
                    context.Users.Add(new User
                    {
                        Username = "admin",
                        PasswordHash = "12345", // NOTE: Passwords must be hashed (e.g., SHA256) in real projects!
                        RoleId = adminRole.Id,
                        IsActive = true
                    });
                    context.SaveChanges();
                }
            }
        }
    }
}
