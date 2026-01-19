using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using BHMHockey.Api.Models.Entities;

namespace BHMHockey.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Tables
    public DbSet<User> Users { get; set; }
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<OrganizationSubscription> OrganizationSubscriptions { get; set; }
    public DbSet<OrganizationAdmin> OrganizationAdmins { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<EventRegistration> EventRegistrations { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<BadgeType> BadgeTypes { get; set; }
    public DbSet<UserBadge> UserBadges { get; set; }
    public DbSet<Tournament> Tournaments { get; set; }
    public DbSet<TournamentAdmin> TournamentAdmins { get; set; }
    public DbSet<TournamentAuditLog> TournamentAuditLogs { get; set; }
    public DbSet<TournamentTeam> TournamentTeams { get; set; }
    public DbSet<TournamentTeamMember> TournamentTeamMembers { get; set; }
    public DbSet<TournamentMatch> TournamentMatches { get; set; }
    public DbSet<TournamentRegistration> TournamentRegistrations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Check if using InMemory database (for tests) vs PostgreSQL (production)
        var isInMemory = Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Role).HasMaxLength(50).HasDefaultValue("Player");

            // Store Positions as JSONB for multi-position support
            if (isInMemory)
            {
                // InMemory needs a value converter to handle Dictionary
                var dictionaryConverter = new ValueConverter<Dictionary<string, string>?, string?>(
                    v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null)
                );
                var dictionaryComparer = new ValueComparer<Dictionary<string, string>?>(
                    (c1, c2) => c1 != null && c2 != null ? c1.SequenceEqual(c2) : c1 == c2,
                    c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c == null ? null : new Dictionary<string, string>(c)
                );
                entity.Property(e => e.Positions)
                    .HasConversion(dictionaryConverter)
                    .Metadata.SetValueComparer(dictionaryComparer);
            }
            else
            {
                // PostgreSQL with EnableDynamicJson() handles Dictionary -> jsonb natively
                entity.Property(e => e.Positions).HasColumnType("jsonb");
            }
        });

        // Organization configuration
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Name).IsUnique();  // Prevent duplicate org names
            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Store SkillLevels as JSONB array for multi-skill level support
            if (isInMemory)
            {
                var listConverter = new ValueConverter<List<string>?, string?>(
                    v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null)
                );
                var listComparer = new ValueComparer<List<string>?>(
                    (c1, c2) => c1 != null && c2 != null ? c1.SequenceEqual(c2) : c1 == c2,
                    c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c == null ? null : new List<string>(c)
                );
                entity.Property(e => e.SkillLevels)
                    .HasConversion(listConverter)
                    .Metadata.SetValueComparer(listComparer);
            }
            else
            {
                entity.Property(e => e.SkillLevels).HasColumnType("jsonb");
            }
        });

        // OrganizationSubscription configuration
        modelBuilder.Entity<OrganizationSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrganizationId, e.UserId }).IsUnique();
            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Subscriptions)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // OrganizationAdmin configuration
        modelBuilder.Entity<OrganizationAdmin>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrganizationId, e.UserId }).IsUnique();
            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Admins)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AddedByUser)
                .WithMany()
                .HasForeignKey(e => e.AddedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Event configuration
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Name is optional - entity property is string?
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Published");
            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Events)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Store SkillLevels as JSONB array for multi-skill level support
            if (isInMemory)
            {
                var listConverter = new ValueConverter<List<string>?, string?>(
                    v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null)
                );
                var listComparer = new ValueComparer<List<string>?>(
                    (c1, c2) => c1 != null && c2 != null ? c1.SequenceEqual(c2) : c1 == c2,
                    c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c == null ? null : new List<string>(c)
                );
                entity.Property(e => e.SkillLevels)
                    .HasConversion(listConverter)
                    .Metadata.SetValueComparer(listComparer);
            }
            else
            {
                entity.Property(e => e.SkillLevels).HasColumnType("jsonb");
            }
        });

        // EventRegistration configuration
        modelBuilder.Entity<EventRegistration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EventId, e.UserId }).IsUnique();
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Registered");
            entity.HasOne(e => e.Event)
                .WithMany(ev => ev.Registrations)
                .HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Notification configuration
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Index for fetching user's notifications ordered by date
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });

            // Index for unread count queries
            entity.HasIndex(e => new { e.UserId, e.ReadAt });

            // Index for cleanup job (delete old notifications)
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Body).HasMaxLength(1000);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Event)
                .WithMany()
                .HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // BadgeType configuration
        modelBuilder.Entity<BadgeType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.IconName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
        });

        // UserBadge configuration
        modelBuilder.Entity<UserBadge>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Unique constraint: user can only have each badge type once
            entity.HasIndex(e => new { e.UserId, e.BadgeTypeId }).IsUnique();

            // Index for querying user's badges
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.BadgeType)
                .WithMany()
                .HasForeignKey(e => e.BadgeTypeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Store Context as JSONB
            if (isInMemory)
            {
                var contextConverter = new ValueConverter<Dictionary<string, object>?, string?>(
                    v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null)
                );
                var contextComparer = new ValueComparer<Dictionary<string, object>?>(
                    (c1, c2) => c1 != null && c2 != null ? c1.Count == c2.Count && !c1.Except(c2).Any() : c1 == c2,
                    c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c == null ? null : new Dictionary<string, object>(c)
                );
                entity.Property(e => e.Context)
                    .HasConversion(contextConverter)
                    .Metadata.SetValueComparer(contextComparer);
            }
            else
            {
                entity.Property(e => e.Context).HasColumnType("jsonb");
            }
        });

        // Tournament configuration
        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Format).IsRequired().HasMaxLength(50).HasDefaultValue("SingleElimination");
            entity.Property(e => e.TeamFormation).IsRequired().HasMaxLength(50).HasDefaultValue("OrganizerAssigned");
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Draft");

            // Index on status for filtering public tournaments
            entity.HasIndex(e => e.Status);

            // Index on organization for org-scoped queries
            entity.HasIndex(e => e.OrganizationId);

            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            // JSONB columns for configuration
            if (isInMemory)
            {
                // InMemory needs no special handling for string columns
                // The JSONB columns are stored as plain strings
            }
            else
            {
                entity.Property(e => e.NotificationSettings).HasColumnType("jsonb");
                entity.Property(e => e.CustomQuestions).HasColumnType("jsonb");
                entity.Property(e => e.EligibilityRequirements).HasColumnType("jsonb");
                entity.Property(e => e.TiebreakerOrder).HasColumnType("jsonb");
            }
        });

        // TournamentAdmin configuration
        modelBuilder.Entity<TournamentAdmin>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50).HasDefaultValue("Admin");

            // Unique constraint: user can only have one admin role per tournament
            entity.HasIndex(e => new { e.TournamentId, e.UserId }).IsUnique();

            // Index for querying user's tournament history
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.Tournament)
                .WithMany(t => t.Admins)
                .HasForeignKey(e => e.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AddedByUser)
                .WithMany()
                .HasForeignKey(e => e.AddedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // TournamentAuditLog configuration
        modelBuilder.Entity<TournamentAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FromStatus).HasMaxLength(50);
            entity.Property(e => e.ToStatus).HasMaxLength(50);

            // Index for querying audit logs by tournament
            entity.HasIndex(e => e.TournamentId);
            entity.HasIndex(e => e.Timestamp);

            entity.HasOne(e => e.Tournament)
                .WithMany()
                .HasForeignKey(e => e.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // JSONB for PostgreSQL, regular string for InMemory
            if (!Database.ProviderName?.Contains("InMemory") ?? false)
            {
                entity.Property(e => e.Details).HasColumnType("jsonb");
            }
        });

        // TournamentTeam configuration
        modelBuilder.Entity<TournamentTeam>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Registered");

            // Index for querying teams by tournament
            entity.HasIndex(e => e.TournamentId);

            // Index for querying teams by status
            entity.HasIndex(e => new { e.TournamentId, e.Status });

            entity.HasOne(e => e.Tournament)
                .WithMany()
                .HasForeignKey(e => e.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Captain)
                .WithMany()
                .HasForeignKey(e => e.CaptainUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // TournamentMatch configuration
        modelBuilder.Entity<TournamentMatch>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Scheduled");
            entity.Property(e => e.BracketPosition).HasMaxLength(50);
            entity.Property(e => e.Venue).HasMaxLength(255);

            // Index for querying matches by tournament
            entity.HasIndex(e => e.TournamentId);

            // Index for bracket position queries
            entity.HasIndex(e => new { e.TournamentId, e.Round, e.MatchNumber });

            entity.HasOne(e => e.Tournament)
                .WithMany()
                .HasForeignKey(e => e.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.HomeTeam)
                .WithMany()
                .HasForeignKey(e => e.HomeTeamId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.AwayTeam)
                .WithMany()
                .HasForeignKey(e => e.AwayTeamId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.WinnerTeam)
                .WithMany()
                .HasForeignKey(e => e.WinnerTeamId)
                .OnDelete(DeleteBehavior.SetNull);

            // Self-referencing relationships for bracket navigation
            entity.HasOne(e => e.NextMatch)
                .WithMany()
                .HasForeignKey(e => e.NextMatchId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.LoserNextMatch)
                .WithMany()
                .HasForeignKey(e => e.LoserNextMatchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // TournamentRegistration configuration
        modelBuilder.Entity<TournamentRegistration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Registered");
            entity.Property(e => e.Position).HasMaxLength(50);
            entity.Property(e => e.WaiverStatus).HasMaxLength(50);
            entity.Property(e => e.PaymentStatus).HasMaxLength(50);

            // Unique constraint: user can only have one registration per tournament
            entity.HasIndex(e => new { e.TournamentId, e.UserId }).IsUnique();

            // Index for querying registrations by tournament
            entity.HasIndex(e => e.TournamentId);

            // Index for querying registrations by status
            entity.HasIndex(e => new { e.TournamentId, e.Status });

            // Index for querying user's tournament history
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.Tournament)
                .WithMany()
                .HasForeignKey(e => e.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AssignedTeam)
                .WithMany()
                .HasForeignKey(e => e.AssignedTeamId)
                .OnDelete(DeleteBehavior.SetNull);

            // JSONB for PostgreSQL, regular string for InMemory
            if (!Database.ProviderName?.Contains("InMemory") ?? false)
            {
                entity.Property(e => e.CustomResponses).HasColumnType("jsonb");
            }
        });

        // TournamentTeamMember configuration
        modelBuilder.Entity<TournamentTeamMember>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Team)
                .WithMany()
                .HasForeignKey(e => e.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: one user per team
            entity.HasIndex(e => new { e.TeamId, e.UserId }).IsUnique();

            // Index for querying user's tournament history
            entity.HasIndex(e => e.UserId);
        });
    }
}
