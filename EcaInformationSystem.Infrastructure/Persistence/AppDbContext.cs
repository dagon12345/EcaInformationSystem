using EcaInformationSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EcaInformationSystem.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<Product> Products => Set<Product>();
        public DbSet<BeneficiaryInformation> BeneficiaryInformations => Set<BeneficiaryInformation>();
        public DbSet<Region> Regions => Set<Region>();
        public DbSet<Province> Provinces => Set<Province>();
        public DbSet<Municipality> Municipalities => Set<Municipality>();
        public DbSet<Barangay> Barangays => Set<Barangay>();
        public DbSet<PendingUserRegistration> PendingUserRegistrations => Set<PendingUserRegistration>();
        public DbSet<Log> Logs => Set<Log>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(200);
                entity.Property(x => x.Price)
                    .HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<PendingUserRegistration>(entity =>
            {
                entity.ToTable("PendingUserRegistrations");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.FullName)
                    .IsRequired()
                    .HasMaxLength(200);
                entity.Property(x => x.Position)
                    .IsRequired()
                    .HasMaxLength(150);
                entity.Property(x => x.UserName)
                    .IsRequired()
                    .HasMaxLength(100);
                entity.Property(x => x.PasswordHash)
                    .IsRequired();
                entity.Property(x => x.ApprovalStatus)
                    .IsRequired();
                entity.HasIndex(x => x.UserName);
            });

            modelBuilder.Entity<PendingUserRegistration>()
            .HasIndex(x => x.UserName)
            .IsUnique();

        }
    }
}
