using EcaInformationSystem.Domain.Entities;
using EcaInformationSystem.Domain.Entities.ChatEntities;
using EcaInformationSystem.Domain.Entities.PostEntities;
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

        // ✅ NEW — Chat feature
        public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
        public DbSet<ChatRoomMember> ChatRoomMembers => Set<ChatRoomMember>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<ChatAttachment> ChatAttachments => Set<ChatAttachment>();
        public DbSet<ChatMention> ChatMentions => Set<ChatMention>();
        public DbSet<ChatReadStatus> ChatReadStatuses => Set<ChatReadStatus>();
        public DbSet<ChatMessageReaction> ChatMessageReactions => Set<ChatMessageReaction>();
        public DbSet<FormDocument> FormDocuments => Set<FormDocument>();
        public DbSet<FormFolder> FormFolders => Set<FormFolder>();
        public DbSet<FormActivityLog> FormActivityLogs => Set<FormActivityLog>();
        public DbSet<BeneficiaryPaymentHistory> BeneficiaryPaymentHistories => Set<BeneficiaryPaymentHistory>();
        //PostEntities
        public DbSet<Post> Posts => Set<Post>();
        public DbSet<PostComment> PostComments => Set<PostComment>();
        public DbSet<PostLike> PostLikes => Set<PostLike>();
        public DbSet<PostView> PostViews => Set<PostView>();
        public DbSet<PostImage> PostImages => Set<PostImage>();
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
                entity.Property(e => e.FileData).IsRequired().HasColumnType("varbinary(max)");
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

            modelBuilder.Entity<Log>()
                .HasIndex(l => l.CreatedAt)
                .IsDescending(); // SQL Server 2016+/EF Core 7+; omit .IsDescending() on older versions — still helps a lot

            modelBuilder.Entity<Log>()
                .HasIndex(l => new { l.UserName, l.CreatedAt });

            // ═══════════════════════════════════════════════════════════════════
            // CHAT FEATURE
            // ═══════════════════════════════════════════════════════════════════

            modelBuilder.Entity<ChatRoom>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Type)
                      .HasConversion<int>()
                      .IsRequired();

                // ✅ Only ONE Global room should ever exist. Filtered unique index —
                // applies only to rows where Type = 2 (Global) — lets Regional/Direct
                // rows coexist freely while guaranteeing Global is a true singleton.
                entity.HasIndex(x => x.Type)
                      .IsUnique()
                      .HasFilter("[Type] = 2")
                      .HasDatabaseName("UQ_ChatRoom_SingleGlobalRoom");

                // ✅ Only ONE room per RegionCode for Regional rooms. Same filtered-unique
                // pattern — prevents GetOrCreateRegionalRoomAsync from ever racing into
                // two rooms for the same region under concurrent first-time creation.
                entity.HasIndex(x => new { x.Type, x.RegionCode })
                      .IsUnique()
                      .HasFilter("[Type] = 1")
                      .HasDatabaseName("UQ_ChatRoom_OneRoomPerRegion");
            });

            modelBuilder.Entity<ChatRoomMember>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasOne(x => x.Room)
                      .WithMany(x => x.Members)
                      .HasForeignKey(x => x.RoomId)
                      .OnDelete(DeleteBehavior.Cascade);

                // ✅ A user can't be added to the same Direct room twice
                entity.HasIndex(x => new { x.RoomId, x.UserId })
                      .IsUnique()
                      .HasDatabaseName("UQ_ChatRoomMember_RoomUser");

                // ✅ Powers GetUserDirectRoomsAsync — "all rooms this user belongs to"
                entity.HasIndex(x => x.UserId)
                      .HasDatabaseName("IX_ChatRoomMember_UserId");
            });

            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Content).HasMaxLength(4000);

                entity.HasOne(x => x.Room)
                      .WithMany(x => x.Messages)
                      .HasForeignKey(x => x.RoomId)
                      .OnDelete(DeleteBehavior.Cascade);

                // ✅ THE core chat index — every history fetch, unread count, and
                // "latest message" lookup filters by RoomId and orders by SentAt.
                // Descending because pagination always fetches newest-first.
                entity.HasIndex(x => new { x.RoomId, x.SentAt })
                      .IsDescending(false, true)
                      .HasDatabaseName("IX_ChatMessage_Room_SentAt");

                // ✅ Supports "does this room have unread messages after X" and
                // oversight/audit queries that need a sender's message history
                entity.HasIndex(x => x.SenderId)
                      .HasDatabaseName("IX_ChatMessage_SenderId");

                entity.HasIndex(x => x.ReplyToMessageId)
                    .HasDatabaseName("IX_ChatMessage_ReplyToMessageId");
            });

            modelBuilder.Entity<ChatAttachment>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.FileData).IsRequired().HasColumnType("varbinary(max)");
                entity.Property(x => x.ThumbnailData).HasColumnType("varbinary(max)");
                entity.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
                entity.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(500);
                entity.Property(x => x.Type).HasConversion<int>().IsRequired();

                // ✅ CHANGED — one-to-MANY now, multiple attachments can share a message
                entity.HasOne(x => x.Message)
                      .WithMany(x => x.Attachments)
                      .HasForeignKey(x => x.ChatMessageId)
                      .OnDelete(DeleteBehavior.Cascade);

                // ✅ CHANGED — plain index, no longer unique
                entity.HasIndex(x => x.ChatMessageId)
                      .HasDatabaseName("IX_ChatAttachments_ChatMessageId");
            });

            modelBuilder.Entity<ChatMention>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasOne(x => x.Message)
                      .WithMany(x => x.Mentions)
                      .HasForeignKey(x => x.ChatMessageId)
                      .OnDelete(DeleteBehavior.Cascade);

                // ✅ Powers GetMentionJumpListAsync — "every message that mentions me"
                entity.HasIndex(x => x.MentionedUserId)
                      .HasDatabaseName("IX_ChatMention_MentionedUserId");

                entity.HasIndex(x => x.ChatMessageId)
                      .HasDatabaseName("IX_ChatMention_ChatMessageId");
            });

            modelBuilder.Entity<ChatReadStatus>(entity =>
            {
                entity.HasKey(x => x.Id);

                // ✅ One read-status row per (user, room) pair — upsert target for
                // UpsertReadStatusAsync, and the lookup key for unread-count queries
                entity.HasIndex(x => new { x.RoomId, x.UserId })
                      .IsUnique()
                      .HasDatabaseName("UQ_ChatReadStatus_RoomUser");
            });

            modelBuilder.Entity<ChatMessageReaction>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Type).HasConversion<int>().IsRequired();

                entity.HasOne(x => x.Message)
                      .WithMany() // ChatMessage doesn't need a Reactions nav property unless you want one — omitted to keep ChatMessage lean, reactions are always queried by RoomId/MessageId directly
                      .HasForeignKey(x => x.ChatMessageId)
                      .OnDelete(DeleteBehavior.Cascade);

                // ✅ One reaction per user per message — enforces "swap, don't stack"
                entity.HasIndex(x => new { x.ChatMessageId, x.UserId })
                      .IsUnique()
                      .HasDatabaseName("UQ_ChatMessageReaction_MessageUser");

                entity.HasIndex(x => x.ChatMessageId)
                      .HasDatabaseName("IX_ChatMessageReaction_ChatMessageId");
            });

            modelBuilder.Entity<FormFolder>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Name).IsRequired().HasMaxLength(150);
                entity.Property(x => x.CreatedBy).HasMaxLength(200);
                entity.Property(x => x.UpdatedBy).HasMaxLength(200);

                entity.HasIndex(x => x.IsDeleted);
                entity.HasIndex(x => x.Name);
            });

            modelBuilder.Entity<FormDocument>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Title).IsRequired().HasMaxLength(300);
                entity.Property(x => x.Category).HasMaxLength(100);
                entity.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(500);
                entity.Property(x => x.ContentType).IsRequired().HasMaxLength(150);
                entity.Property(x => x.FileData).IsRequired().HasColumnType("varbinary(max)");
                entity.Property(x => x.UploadedBy).HasMaxLength(200);
                entity.Property(x => x.UpdatedBy).HasMaxLength(200);

                entity.Property(x => x.RowVersion).IsRowVersion();

                entity.HasIndex(x => x.IsDeleted);
                entity.HasIndex(x => x.Category);

                // ✅ NEW — nullable FK; SetNull means a folder delete never cascades to files
                entity.HasOne(x => x.Folder)
                    .WithMany(f => f.Documents)
                    .HasForeignKey(x => x.FolderId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(x => x.FolderId);
            });

            modelBuilder.Entity<FormActivityLog>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Action).IsRequired().HasMaxLength(50);
                entity.Property(x => x.TargetName).IsRequired().HasMaxLength(300);
                entity.Property(x => x.UserName).HasMaxLength(200);

                entity.HasIndex(x => x.CreatedAt).IsDescending();
                entity.HasIndex(x => x.FolderId);
                entity.HasIndex(x => x.FormDocumentId);
            });
            modelBuilder.Entity<BeneficiaryPaymentHistory>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasOne(x => x.Beneficiary)
                      .WithMany()
                      .HasForeignKey(x => x.BeneficiaryInformationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => new { x.BeneficiaryInformationId, x.PaymentDate })
                      .HasDatabaseName("IX_PaymentHistory_Beneficiary_PaymentDate");

                entity.HasIndex(x => new { x.FiscalYear, x.PayrollQuarter, x.PaymentStatus })
                      .HasDatabaseName("IX_PaymentHistory_FiscalYear_Quarter_Status");

                entity.Property(x => x.CreatedBy).HasMaxLength(256);
                entity.Property(x => x.ModifiedBy).HasMaxLength(256);
            });

            modelBuilder.Entity<BeneficiaryInformation>(entity =>
            {
                entity.HasIndex(b => b.CurrentPaymentHistoryId)
                      .HasDatabaseName("IX_BeneficiaryInformation_CurrentPaymentHistoryId");
            });
            // ═══════════════════════════════════════════════════════════════════
            // POSTS / FEED FEATURE
            // ═══════════════════════════════════════════════════════════════════
            modelBuilder.Entity<Post>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Content).IsRequired().HasMaxLength(2000);

                // Feed always orders newest-first — this is the one index that matters most.
                entity.HasIndex(x => new { x.IsDeleted, x.CreatedAt })
                      .HasDatabaseName("IX_Post_IsDeleted_CreatedAt");
            });

            modelBuilder.Entity<PostComment>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Content).IsRequired().HasMaxLength(1000);

                // Shadow FK — no nav property needed on Post, same idea as ChatMessageReaction.
                entity.HasOne<Post>()
                      .WithMany()
                      .HasForeignKey(x => x.PostId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => new { x.PostId, x.IsDeleted, x.CreatedAt })
                      .HasDatabaseName("IX_PostComment_Post_CreatedAt");
            });

            modelBuilder.Entity<PostLike>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasOne<Post>()
                      .WithMany()
                      .HasForeignKey(x => x.PostId)
                      .OnDelete(DeleteBehavior.Cascade);

                // ✅ Enforces "one like per viewer per post" at the DB level —
                // ToggleLikeAsync relies on this to swap, not stack.
                entity.HasIndex(x => new { x.PostId, x.LikerKey })
                      .IsUnique()
                      .HasDatabaseName("UQ_PostLike_Post_Liker");

                entity.Property(x => x.ReactionType).HasConversion<int>();
            });

            modelBuilder.Entity<PostView>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasOne<Post>()
                      .WithMany()
                      .HasForeignKey(x => x.PostId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => new { x.PostId, x.ViewerKey })
                      .IsUnique()
                      .HasDatabaseName("UQ_PostView_Post_Viewer");
            });
            modelBuilder.Entity<PostImage>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.FileName).IsRequired().HasMaxLength(500);
                entity.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
                entity.Property(x => x.ImageData).IsRequired().HasColumnType("varbinary(max)");
                entity.Property(x => x.ThumbnailData).IsRequired().HasColumnType("varbinary(max)");

                entity.HasOne<Post>()
                      .WithMany()
                      .HasForeignKey(x => x.PostId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => new { x.PostId, x.DisplayOrder })
                      .HasDatabaseName("IX_PostImage_Post_DisplayOrder");
            });
        }
    }
}
