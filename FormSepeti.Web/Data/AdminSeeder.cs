// FormSepeti.Web/Data/AdminSeeder.cs
using FormSepeti.Data;
using FormSepeti.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FormSepeti.Web.Data
{
    public static class AdminSeeder
    {
        public static async Task SeedDefaultAdminAsync(ApplicationDbContext context)
        {
            // Eðer hiç admin yoksa default admin oluþtur
            if (!await context.AdminUsers.AnyAsync())
            {
                var defaultAdmin = new AdminUser
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"), // ? Ýlk giriþ þifresi
                    FullName = "System Administrator",
                    Email = "admin@formsepeti.com",
                    Role = "SuperAdmin",
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };

                context.AdminUsers.Add(defaultAdmin);
                await context.SaveChangesAsync();

                Console.WriteLine("? Default admin created:");
                Console.WriteLine("   Username: admin");
                Console.WriteLine("   Password: Admin123!");
                Console.WriteLine("   ??  Please change this password after first login!");
            }
        }
    }
}