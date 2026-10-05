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
