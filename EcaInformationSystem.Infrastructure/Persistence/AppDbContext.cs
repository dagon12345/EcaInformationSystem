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
        public DbSet<BeneficiaryFinding> BeneficiaryFindings => Set<BeneficiaryFinding>();
        public DbSet<BeneficiaryDocument> BeneficiaryDocuments => Set<BeneficiaryDocument>();
        public DbSet<PdoJurisdiction> PdoJurisdictions => Set<PdoJurisdiction>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BeneficiaryFinding>(entity =>
            {
                entity.HasKey(x => x.Id);

                // One-to-one: one beneficiary has at most one finding record
                entity.HasOne(x => x.BeneficiaryInformation)
                      .WithOne(x => x.Finding)
                      .HasForeignKey<BeneficiaryFinding>(x => x.BeneficiaryInformationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => x.BeneficiaryInformationId).IsUnique();

                // Useful for filtering "all unresolved" across the system
                entity.HasIndex(x => x.FindingStatus);
            });

            //Indexing
            modelBuilder.Entity<BeneficiaryInformation>(entity =>
            {
                entity.HasKey(x => x.Id);

                // Composite indexes for common filtering patterns
                entity.HasIndex(x => new { x.IsDeleted, x.Region });
                entity.HasIndex(x => new { x.IsDeleted, x.Province });
                entity.HasIndex(x => new { x.IsDeleted, x.Municipality });
                entity.HasIndex(x => new { x.IsDeleted, x.Barangay });
                entity.HasIndex(x => new { x.IsDeleted, x.BirthDate });
                entity.HasIndex(x => new { x.IsDeleted, x.Sex });
                entity.HasIndex(x => new { x.IsDeleted, x.IsCompliant });
                entity.HasIndex(x => new { x.IsDeleted, x.IsEligible });
                // Optional: sorting support for paged queries
                entity.HasIndex(x => new { x.IsDeleted, x.LastName, x.FirstName, x.MiddleName });
                // Duplicate detection support
                entity.HasIndex(x => new
                {
                    x.LastName,
                    x.FirstName,
                    x.MiddleName,
                    x.BirthDate,
                    x.OscaIdNumber,
                    x.NcscRrn
                });

                //Co Status
                entity.HasIndex(x => new { x.IsDeleted, x.CoStatus });
                entity.HasIndex(x => new { x.IsDeleted, x.CoStatus, x.IsCompliant });
                entity.HasIndex(x => new { x.IsDeleted, x.CoStatus, x.IsEligible });
                //Reference Index
                entity.HasIndex(x => new { x.IsDeleted, x.Quarter, x.Batch, x.RefYear });
                entity.HasIndex(x => new { x.IsDeleted, x.Quarter });
                entity.HasIndex(x => new { x.IsDeleted, x.Batch });
                entity.HasIndex(x => new { x.IsDeleted, x.RefYear });


            });

            modelBuilder.Entity<Log>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => x.BeneficiaryInformationId);
                entity.HasIndex(x => new { x.BeneficiaryInformationId, x.CreatedAt });
            });

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

            modelBuilder.Entity<BeneficiaryInformation>()
            .Property(x => x.RowVersion)
            .IsRowVersion();

            modelBuilder.Entity<BeneficiaryDocument>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
                entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(500);
                entity.Property(e => e.FilePath).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.ContentType).HasMaxLength(100);
                entity.Property(e => e.UploadedBy).HasMaxLength(200);

                entity.HasOne(e => e.BeneficiaryInformation)
                    .WithMany()
                    .HasForeignKey(e => e.BeneficiaryInformationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.BeneficiaryInformationId);
                entity.HasIndex(e => e.IsDeleted);
            });
            modelBuilder.Entity<PdoJurisdiction>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasOne(x => x.User)
                    .WithMany(x => x.Jurisdictions)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // One PDO can't be assigned the same municipality twice
                entity.HasIndex(x => new { x.UserId, x.PsgcCodeMunicipality })
                    .IsUnique();

                entity.HasIndex(x => x.UserId);
                entity.HasIndex(x => x.PsgcCodeMunicipality);
            });

            modelBuilder.Entity<PendingUserRegistration>(entity =>
            {
                // ✅ existing config stays — just add SuperAdmin to allowed roles
                entity.ToTable("PendingUserRegistrations");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.FullName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.Position).IsRequired().HasMaxLength(150);
                entity.Property(x => x.UserName).IsRequired().HasMaxLength(100);
                entity.Property(x => x.PasswordHash).IsRequired();
                entity.Property(x => x.ApprovalStatus).IsRequired();
                entity.Property(x => x.Role).IsRequired().HasMaxLength(50)
                      .HasDefaultValue("Viewer");
                entity.HasIndex(x => x.UserName);
            });
            
            modelBuilder.Entity<PendingUserRegistration>()
                .HasIndex(x => x.UserName)
                .IsUnique();            
        }
    }
}
