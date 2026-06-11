// Infrastructure/Persistence/SuperAdminSeeder.cs
using EcaInformationSystem.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Persistence
{
    public static class SuperAdminSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            // ✅ Only seed if no SuperAdmin exists
            if (await context.PendingUserRegistrations
                .AnyAsync(u => u.Role == "SuperAdmin"))
                return;

            var hasher = new PasswordHasher<PendingUserRegistration>();
            var admin  = new PendingUserRegistration
            {
                Id             = Guid.NewGuid(),
                FullName       = "System Super Admin",
                Position       = "System Administrator",
                BirthDate      = new DateTime(1990, 1, 1),
                UserName       = "superadmin",
                IsActivated    = true,
                ApprovalStatus = 1,
                Role           = "SuperAdmin",
                RequestedAt    = DateTime.UtcNow,
                ReviewedAt     = DateTime.UtcNow,
                ReviewedBy     = "System",
                Remarks        = "Default super admin — change password immediately."
            };

            // ✅ Default password: REDACTED_DEFAULT_PASSWORD
            // MUST be changed immediately after first login
            admin.PasswordHash = hasher.HashPassword(admin, "REDACTED_DEFAULT_PASSWORD");

            await context.PendingUserRegistrations.AddAsync(admin);
            await context.SaveChangesAsync();
        }
    }
}