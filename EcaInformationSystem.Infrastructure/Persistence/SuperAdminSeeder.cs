// Infrastructure/Persistence/SuperAdminSeeder.cs
using EcaInformationSystem.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Persistence
{
    public static class SuperAdminSeeder
    {
        // Caraga Region (Region XIII) PSGC code — matches the "160000000"
        // value already seen in JWTs throughout this system's earlier testing.
        private const int DefaultRegionCode = 1600000000;

        public static async Task SeedAsync(AppDbContext context)
        {
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
                Region         = DefaultRegionCode,   // ✅ matches int? Region on the entity
                RequestedAt    = DateTime.UtcNow,
                ReviewedAt     = DateTime.UtcNow,
                ReviewedBy     = "System",
                Remarks        = "Default super admin — change password immediately."
            };

            // No hardcoded default: read from env (set alongside the other MONSTERASP_*
            // deploy secrets) or fall back to a random one-time password printed to the
            // server's own console log — never committed to source control.
            var seedPassword = Environment.GetEnvironmentVariable("SUPERADMIN_SEED_PASSWORD");
            if (string.IsNullOrWhiteSpace(seedPassword))
            {
                seedPassword = $"{Guid.NewGuid():N}!Aa1";
                Console.WriteLine(
                    $"[SuperAdminSeeder] SUPERADMIN_SEED_PASSWORD not set — generated one-time password: {seedPassword}. Log in and change it immediately.");
            }
            admin.PasswordHash = hasher.HashPassword(admin, seedPassword);

            await context.PendingUserRegistrations.AddAsync(admin);
            await context.SaveChangesAsync();
        }
    }
}