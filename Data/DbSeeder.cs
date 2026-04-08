using DormitoryManagementSystem.Models;

namespace DormitoryManagementSystem.Data
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext context)
        {
            // Veritabanının oluşturulduğundan emin ol
            // Recreate database with new schema (Name, Surname, NationalId, Email columns)
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            // Eğer veritabanında hiç "Role" yoksa, 3 ana rolü ekle
            if (!context.Roles.Any())
            {
                context.Roles.AddRange(
                    new Role { RoleName = "Admin" },
                    new Role { RoleName = "Staff" },
                    new Role { RoleName = "Student" }
                );
                context.SaveChanges();
            }

            // Eğer sistemde hiç kullanıcı yoksa, bir "Admin" hesabı oluştur (Şifre: 12345)
            if (!context.Users.Any())
            {
                var adminRole = context.Roles.FirstOrDefault(r => r.RoleName == "Admin");
                if (adminRole != null)
                {
                    context.Users.Add(new User
                    {
                        Username = "admin",
                        PasswordHash = "12345", // Gerçek projelerde burası mutlaka Hashlenmeli (örn. SHA256)!
                        RoleId = adminRole.Id,
                        IsActive = true
                    });
                    context.SaveChanges();
                }
            }
        }
    }
}
