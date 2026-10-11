using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services.Data.Seed;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Infrastructure.Services.Data
{
    /// <summary>
    /// Represents the EF Core database context used by the application.
    /// </summary>
    public sealed class ConnectoDbContext : DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Need> Needs => Set<Need>();
        public DbSet<Offering> Offerings => Set<Offering>();
        public DbSet<NeedSkill> NeedSkills => Set<NeedSkill>();
        public DbSet<OfferingSkill> OfferingSkills => Set<OfferingSkill>();
        public DbSet<MemberBlock> MemberBlocks => Set<MemberBlock>();
        public DbSet<SafetyReport> SafetyReports => Set<SafetyReport>();
        public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
        public DbSet<ConsumerReceipt> ConsumerReceipts => Set<ConsumerReceipt>();
        public DbSet<JourneyEvent> JourneyEvents => Set<JourneyEvent>();
        public DbSet<PilotSignal> PilotSignals => Set<PilotSignal>();
        public DbSet<ConnectionRequest> ConnectionRequests => Set<ConnectionRequest>();
        public DbSet<Engagement> Engagements => Set<Engagement>();
        public DbSet<EngagementOutcomeEvent> EngagementOutcomeEvents => Set<EngagementOutcomeEvent>();
        public DbSet<EngagementReview> EngagementReviews => Set<EngagementReview>();
        public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
        public DbSet<MediaAccessAudit> MediaAccessAudits => Set<MediaAccessAudit>();
        public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
        public DbSet<ConversationReceipt> ConversationReceipts => Set<ConversationReceipt>();

        public ConnectoDbContext(
            DbContextOptions<ConnectoDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            ConfigureUser(modelBuilder);
            ConfigureNeed(modelBuilder);
            ConfigureOffering(modelBuilder);
            ConfigureMatch(modelBuilder);
            modelBuilder.Entity<PilotSignal>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Kind).HasMaxLength(16).IsRequired();
                entity.Property(x => x.ClientKey).HasMaxLength(100).IsRequired();
                entity.HasIndex(x => new { x.MemberId, x.Kind, x.ClientKey }).IsUnique();
                entity.HasIndex(x => new { x.Kind, x.OccurredAt });
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<Need>().WithMany().HasForeignKey(x => x.NeedId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<Offering>().WithMany().HasForeignKey(x => x.OfferingId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<ConnectionRequest>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Terms).HasMaxLength(2000).IsRequired();
                entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
                entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
                entity.Property(x => x.PendingPairKey).HasMaxLength(73);
                entity.Property(x => x.ProposedAmount).HasPrecision(18, 2);
                entity.Property(x => x.Revision).IsConcurrencyToken();
                entity.HasIndex(x => x.PendingPairKey).IsUnique().HasFilter("[PendingPairKey] IS NOT NULL");
                entity.HasIndex(x => new { x.SenderId, x.CreatedAt });
                entity.HasIndex(x => new { x.RecipientId, x.CreatedAt });
                entity.HasOne<Need>().WithMany().HasForeignKey(x => x.NeedId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<Offering>().WithMany().HasForeignKey(x => x.OfferingId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<Engagement>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.NeedTitle).HasMaxLength(200).IsRequired();
                entity.Property(x => x.OfferingTitle).HasMaxLength(200).IsRequired();
                entity.Property(x => x.Terms).HasMaxLength(2000).IsRequired();
                entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
                entity.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
                entity.Property(x => x.AgreedAmount).HasPrecision(18, 2);
                entity.Property(x => x.OutcomeState).HasMaxLength(32).IsRequired();
                entity.Property(x => x.OutcomeRevision).IsConcurrencyToken();
                entity.Property(x => x.DisputeReviewerSubjectId).HasMaxLength(128);
                entity.Property(x => x.DisputeReviewNote).HasMaxLength(1000);
                entity.HasIndex(x => x.NeedId).IsUnique();
                entity.HasIndex(x => x.ConnectionRequestId).IsUnique();
                entity.HasOne<ConnectionRequest>().WithMany().HasForeignKey(x => x.ConnectionRequestId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<Need>().WithMany().HasForeignKey(x => x.NeedId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<Offering>().WithMany().HasForeignKey(x => x.OfferingId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<EngagementOutcomeEvent>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).ValueGeneratedOnAdd();
                entity.Property(x => x.Action).HasMaxLength(24).IsRequired();
                entity.Property(x => x.ReportedOutcome).HasMaxLength(24);
                entity.Property(x => x.Reason).HasMaxLength(1000);
                entity.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
                entity.HasIndex(x => new { x.EngagementId, x.Id });
                entity.HasIndex(x => new { x.EngagementId, x.ActorId, x.IdempotencyKey }).IsUnique();
                entity.HasOne<Engagement>().WithMany().HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<EngagementReview>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.ReviewerRole).HasMaxLength(16).IsRequired();
                entity.Property(x => x.Comment).HasMaxLength(2000);
                entity.Property(x => x.ExclusionReason).HasMaxLength(1000);
                entity.Property(x => x.ExcludedBySubjectId).HasMaxLength(128);
                entity.HasIndex(x => new { x.EngagementId, x.ReviewerId }).IsUnique();
                entity.HasIndex(x => x.RevieweeId);
                entity.HasOne<Engagement>().WithMany().HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.RevieweeId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<MediaAsset>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Purpose).HasMaxLength(16).IsRequired();
                entity.Property(x => x.Kind).HasMaxLength(16).IsRequired();
                entity.Property(x => x.Stage).HasMaxLength(16).IsRequired();
                entity.Property(x => x.Stage).IsConcurrencyToken();
                entity.Property(x => x.QuarantineKey).HasMaxLength(160).IsRequired();
                entity.Property(x => x.DisplayKey).HasMaxLength(160);
                entity.Property(x => x.ContentType).HasMaxLength(32);
                entity.Property(x => x.RejectionReason).HasMaxLength(100);
                entity.Property(x => x.RemovalReason).HasMaxLength(1000);
                entity.Property(x => x.RemovedBySubjectId).HasMaxLength(128);
                entity.HasIndex(x => new { x.EngagementId, x.UploaderId, x.Purpose });
                entity.HasIndex(x => new { x.Stage, x.CreatedAt });
                entity.HasOne<Engagement>().WithMany().HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.UploaderId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<EngagementReview>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<MediaAccessAudit>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).ValueGeneratedOnAdd();
                entity.Property(x => x.Purpose).HasMaxLength(40).IsRequired();
                entity.HasIndex(x => new { x.MediaAssetId, x.OccurredAt });
                entity.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.MediaAssetId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<ConversationMessage>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).ValueGeneratedOnAdd();
                entity.Property(x => x.ClientKey).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Text).HasMaxLength(2000).IsRequired();
                entity.HasIndex(x => new { x.EngagementId, x.Id });
                entity.HasIndex(x => new { x.EngagementId, x.SenderId, x.ClientKey }).IsUnique();
                entity.HasOne<Engagement>().WithMany().HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<ConversationReceipt>(entity =>
            {
                entity.HasKey(x => new { x.EngagementId, x.MemberId });
                entity.HasOne<Engagement>().WithMany().HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<MemberBlock>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => new { x.BlockerId, x.BlockedId }).IsUnique();
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.BlockerId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.BlockedId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<SafetyReport>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Reason).HasMaxLength(40).IsRequired();
                entity.Property(x => x.Details).HasMaxLength(2000).IsRequired();
                entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
                entity.Property(x => x.OperatorSubjectId).HasMaxLength(128);
                entity.HasIndex(x => new { x.Status, x.CreatedAt });
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<Offering>().WithMany().HasForeignKey(x => x.OfferingId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.MediaAssetId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<OutboxEvent>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Type).HasMaxLength(80).IsRequired();
                entity.HasIndex(x => new { x.DeliveredAt, x.OccurredAt });
            });
            modelBuilder.Entity<ConsumerReceipt>(entity =>
            {
                entity.HasKey(x => new { x.EventId, x.Consumer });
                entity.Property(x => x.Consumer).HasMaxLength(80);
                entity.HasOne<OutboxEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<JourneyEvent>(entity =>
            {
                entity.HasKey(x => x.EventId);
                entity.Property(x => x.Type).HasMaxLength(80).IsRequired();
                entity.HasOne<OutboxEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<NeedSkill>(entity =>
            {
                entity.HasKey(x => x.Id);


                entity.Property(x => x.Skill)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.HasIndex(x => new { x.NeedId, x.Skill });

                entity.HasOne(x => x.Need)
                    .WithMany(x => x.Skills)
                    .HasForeignKey(x => x.NeedId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<OfferingSkill>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Skill)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.HasIndex(x => new { x.OfferingId, x.Skill });

                entity.HasOne(x => x.Offering)
                    .WithMany(x => x.Skills)
                    .HasForeignKey(x => x.OfferingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            SeedData.Seed(modelBuilder);
        }

        private static void ConfigureUser(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.AuthSubjectId).HasMaxLength(128);
                entity.HasIndex(x => x.AuthSubjectId).IsUnique().HasFilter("[AuthSubjectId] IS NOT NULL");

                entity.Property(x => x.FirstName)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.MiddleName)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.LastName)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.Address)
                    .HasMaxLength(300);

                entity.Property(x => x.City)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.Country)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.Bio)
                    .HasMaxLength(2000);
            });
        }

        private static void ConfigureNeed(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Need>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Title)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.Description)
                    .HasMaxLength(4000)
                    .IsRequired();

                entity.Property(x => x.Category)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.BudgetMin)
                    .HasPrecision(18, 2);

                entity.Property(x => x.BudgetMax)
                    .HasPrecision(18, 2);

                entity.Property(x => x.Currency).HasMaxLength(3);
                entity.Property(x => x.EstimatedHours).HasPrecision(9, 2);
                entity.Property(x => x.Revision).IsConcurrencyToken();

                entity.Property(x => x.Location)
                    .HasMaxLength(200);

                entity.HasOne(x => x.User)
                    .WithMany(x => x.Needs)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureOffering(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Offering>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Title)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.Description)
                    .HasMaxLength(4000)
                    .IsRequired();

                entity.Property(x => x.Category)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.PriceMin)
                    .HasPrecision(18, 2);

                entity.Property(x => x.PriceMax)
                    .HasPrecision(18, 2);

                entity.Property(x => x.Currency).HasMaxLength(3);
                entity.Property(x => x.EstimatedHours).HasPrecision(9, 2);
                entity.Property(x => x.Revision).IsConcurrencyToken();

                entity.Property(x => x.Location)
                    .HasMaxLength(200);

                entity.HasOne(x => x.User)
                    .WithMany(x => x.Offerings)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureMatch(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Match>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Score)
                    .HasPrecision(5, 2);

                entity.Property(x => x.SkillsScore)
                    .HasPrecision(5, 2);

                entity.Property(x => x.RequirementsScore)
                    .HasPrecision(5, 2);

                entity.Property(x => x.AvailabilityScore)
                    .HasPrecision(5, 2);

                entity.Property(x => x.BudgetScore)
                    .HasPrecision(5, 2);

                entity.Property(x => x.LocationScore)
                    .HasPrecision(5, 2);

                entity.Property(x => x.ExperienceScore)
                    .HasPrecision(5, 2);

                entity.Property(x => x.ReputationScore)
                    .HasPrecision(5, 2);

                entity.HasOne<Need>()
                    .WithMany()
                    .HasForeignKey(x => x.NeedId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<Offering>()
                    .WithMany()
                    .HasForeignKey(x => x.OfferingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
