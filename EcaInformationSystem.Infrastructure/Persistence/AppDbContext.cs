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

            // Infrastructure/Persistence/AppDbContext.cs
            // Replace the entire BeneficiaryInformation index block with this.

            modelBuilder.Entity<BeneficiaryInformation>(entity =>
            {
                entity.HasKey(x => x.Id);

                // ═══════════════════════════════════════════════════════════════════
                // WHY THIS BLOCK REPLACES YOUR OLD ONE:
                //
                // Old indexes were "filter column(s) only" — e.g. (IsDeleted, Province).
                // Every grid query ALSO sorts by LastName/FirstName/MiddleName (or
                // BatchCode, or BirthDate). An index that only covers the filter forces
                // SQL Server to either:
                //   (a) seek the filter index, then run a separate SORT on the result, or
                //   (b) scan the name-sort index and filter row-by-row as it goes.
                // Either way you pay for two operations instead of one, and that extra
                // Sort operator is exactly the kind of cost that scales badly as the
                // intermediate row count grows toward your 5000-row case.
                //
                // These composite indexes put filter column(s) FIRST, sort columns
                // SECOND, in the same index — so a single ordered index seek satisfies
                // both the WHERE and the ORDER BY, with zero extra Sort operator.
                // ═══════════════════════════════════════════════════════════════════

                entity.HasIndex(x => new { x.IsDeleted, x.LastName, x.FirstName, x.MiddleName })
                      .HasDatabaseName("IX_Beneficiary_NameSort_Default");

                entity.HasIndex(x => new { x.IsDeleted, x.Province, x.LastName, x.FirstName, x.MiddleName })
                      .HasDatabaseName("IX_Beneficiary_Province_NameSort");

                entity.HasIndex(x => new { x.IsDeleted, x.Municipality, x.LastName, x.FirstName, x.MiddleName })
                      .HasDatabaseName("IX_Beneficiary_Municipality_NameSort");

                entity.HasIndex(x => new { x.IsDeleted, x.Barangay, x.LastName, x.FirstName, x.MiddleName })
                      .HasDatabaseName("IX_Beneficiary_Barangay_NameSort");

                entity.HasIndex(x => new { x.IsDeleted, x.BatchCode, x.LastName, x.FirstName, x.MiddleName })
                      .HasDatabaseName("IX_Beneficiary_BatchCode_NameSort");

                entity.HasIndex(x => new { x.IsDeleted, x.BirthDate, x.LastName, x.FirstName })
                      .HasDatabaseName("IX_Beneficiary_BirthDate_NameSort");

                // ── Region intentionally NOT given its own composite. ──────────────
                // WHY: you're the Caraga regional office — Region is almost certainly
                // the same value on 99%+ of rows. An index on a near-constant column
                // gives the optimizer almost no selectivity benefit; it will usually
                // ignore it and scan anyway. If you ever genuinely filter across
                // multiple regions in practice, add it back — but don't pay write
                // cost for an index that reads will rarely use.

                // ── Status filters — narrow, no sort dependency. These support
                // COUNT-style queries (dashboard, bulk-by-filter previews) where row
                // order doesn't matter, so they don't need the name-sort columns.
                entity.HasIndex(x => new { x.IsDeleted, x.PaymentStatus })
                      .HasDatabaseName("IX_Beneficiary_PaymentStatus");

                entity.HasIndex(x => new { x.IsDeleted, x.IsCompliant })
                      .HasDatabaseName("IX_Beneficiary_IsCompliant");

                entity.HasIndex(x => new { x.IsDeleted, x.IsEligible })
                      .HasDatabaseName("IX_Beneficiary_IsEligible");

                entity.HasIndex(x => new { x.IsDeleted, x.CoStatus })
                      .HasDatabaseName("IX_Beneficiary_CoStatus");

                entity.HasIndex(x => new { x.IsDeleted, x.CoStatus, x.IsCompliant })
                      .HasDatabaseName("IX_Beneficiary_CoStatus_Compliant");

                entity.HasIndex(x => new { x.IsDeleted, x.CoStatus, x.IsEligible })
                      .HasDatabaseName("IX_Beneficiary_CoStatus_Eligible");

                entity.HasIndex(x => new { x.IsDeleted, x.Quarter, x.Batch, x.RefYear })
                      .HasDatabaseName("IX_Beneficiary_RefNumber");

                // ── Duplicate detection — unchanged from your original, this one
                // was already correctly shaped for its purpose.
                entity.HasIndex(x => new
                {
                    x.LastName,
                    x.FirstName,
                    x.MiddleName,
                    x.BirthDate,
                    x.OscaIdNumber,
                    x.NcscRrn
                }).HasDatabaseName("IX_Beneficiary_DuplicateDetection");

                // ── REMOVED from your original, deliberately:
                //   (IsDeleted, Region)         -> near-constant column, low value
                //   (IsDeleted, Sex)            -> only 2-3 distinct values, optimizer
                //                                  will ignore this in favor of a scan
                //   (IsDeleted, Quarter) alone  -> redundant subset of RefNumber index
                //   (IsDeleted, Batch) alone    -> redundant subset of RefNumber index
                //   (IsDeleted, RefYear) alone  -> redundant subset of RefNumber index
                // Each of these was pure write overhead (every INSERT/UPDATE maintains
                // every index) with little to no read benefit.
            });

            modelBuilder.Entity<Province>()
                .HasIndex(p => p.PsgcCodeProvince)
                .IsUnique()
                .HasDatabaseName("UQ_Province_PsgcCode");

            modelBuilder.Entity<Municipality>()
                .HasIndex(m => m.PsgcCodeMunicipality)
                .IsUnique()
                .HasDatabaseName("UQ_Municipality_PsgcCode");

            modelBuilder.Entity<Region>()
                .HasIndex(r => r.PsgcCodeRegion)
                .IsUnique()
                .HasDatabaseName("UQ_Region_PsgcCode");
            // Add this in OnModelCreating, alongside your Barangay-related config if
            // you have any, or as a standalone block.

            modelBuilder.Entity<Barangay>()
                .HasIndex(b => b.PsgcCodeBarangay)
                .IsUnique()
                .HasDatabaseName("UQ_Barangay_PsgcCode");

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
                            entity.Property(e => e.FilePath).HasMaxLength(1000);              // ⚠️ TEMPORARY — IsRequired() removed so old rows stay valid; column dropped in Step 7
                            entity.Property(e => e.FileData).HasColumnType("varbinary(max)"); // ✅ NEW — nullable for now, tightened to required in Step 7
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
