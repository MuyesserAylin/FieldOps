using FieldOps.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<FaultType> FaultTypes => Set<FaultType>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobStatusHistory> JobStatusHistories => Set<JobStatusHistory>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamSkill> TeamSkills => Set<TeamSkill>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AssignmentDecision> AssignmentDecisions => Set<AssignmentDecision>();
    public DbSet<AssignmentDecisionCandidate> AssignmentDecisionCandidates => Set<AssignmentDecisionCandidate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.Entity<Skill>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(50);
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<FaultType>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasOne(x => x.RequiredSkill)
                .WithMany()
                .HasForeignKey(x => x.RequiredSkillId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Job>(e =>
        {
            e.Property(x => x.Location).HasColumnType("geography (point, 4326)").IsRequired();
            e.Property(x => x.Description).IsRequired().HasMaxLength(2000);
            e.Property(x => x.TrackingCode).IsRequired().HasMaxLength(32);
            e.HasIndex(x => x.TrackingCode).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.FaultType)
                .WithMany()
                .HasForeignKey(x => x.FaultTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.AssignedTeam)
                .WithMany()
                .HasForeignKey(x => x.AssignedTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.AssignedTeamId, x.AssignedAt });

            e.ToTable(t => t.HasCheckConstraint(
                "ck_jobs_assignment_pair",
                "(assigned_team_id IS NULL) = (assigned_at IS NULL)"));
        });

        modelBuilder.Entity<JobStatusHistory>(e =>
        {
            e.Property(x => x.OldStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Job)
                .WithMany()
                .HasForeignKey(x => x.JobId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.JobId, x.ChangedAt });
        });

        modelBuilder.Entity<Skill>().HasData(
            new Skill { Id = 1, Name = "Gas" },
            new Skill { Id = 2, Name = "Plumbing" },
            new Skill { Id = 3, Name = "Electrical" },
            new Skill { Id = 4, Name = "RoadWork" });

        modelBuilder.Entity<FaultType>().HasData(
             new FaultType { Id = 1, Name = "Gas leak", RequiredSkillId = 1, EstimatedDurationMinutes = 90, DefaultUrgency = Urgency.Critical },
             new FaultType { Id = 2, Name = "Burst water pipe", RequiredSkillId = 2, EstimatedDurationMinutes = 120, DefaultUrgency = Urgency.High },
             new FaultType { Id = 3, Name = "Power outage", RequiredSkillId = 3, EstimatedDurationMinutes = 90, DefaultUrgency = Urgency.High },
             new FaultType { Id = 4, Name = "Pothole", RequiredSkillId = 4, EstimatedDurationMinutes = 60, DefaultUrgency = Urgency.Medium },
             new FaultType { Id = 5, Name = "Street light failure", RequiredSkillId = 3, EstimatedDurationMinutes = 30, DefaultUrgency = Urgency.Low });

        modelBuilder.Entity<User>(e =>
        {
            e.Property(x => x.UserName).HasMaxLength(50);
            e.HasIndex(x => x.UserName).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(200);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Team>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Location).HasColumnType("geography (point, 4326)").IsRequired();
            e.Property(x => x.LocationUpdatedAt).IsRequired();

            e.Navigation(x => x.Skills).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Navigation(x => x.Shifts).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<TeamSkill>(e =>
        {
            e.HasKey(x => new { x.TeamId, x.SkillId });
            e.HasOne(x => x.Team)
                .WithMany(t => t.Skills)
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Skill)
                .WithMany()
                .HasForeignKey(x => x.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_team_skills_level", "level BETWEEN 1 AND 3"));
        });

        modelBuilder.Entity<Shift>(e =>
        {
            e.HasOne(x => x.Team)
                .WithMany(t => t.Shifts)
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("ck_shifts_ends_after_starts", "ends_at > starts_at"));
            e.HasIndex(x => new { x.TeamId, x.StartsAt });
        });


        modelBuilder.Entity<AssignmentDecision>(e =>
        {
            e.Property(x => x.WeightSet).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Job)
                .WithMany()
                .HasForeignKey(x => x.JobId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.JobId, x.DecidedAt });

            e.Navigation(x => x.Candidates).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<AssignmentDecisionCandidate>(e =>
        {
            e.Property(x => x.EliminationReason).HasConversion<string>().HasMaxLength(30);

            e.HasOne(x => x.Decision)
                .WithMany(d => d.Candidates)
                .HasForeignKey(x => x.DecisionId)
                .HasConstraintName("fk_decision_candidates_decision")
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.DecisionId, x.TeamId }).IsUnique();

            e.HasIndex(x => new { x.DecisionId, x.Rank }).IsUnique();

            e.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_assignment_decision_candidates_state",
                    "(elimination_reason IS NOT NULL AND distance_score IS NULL AND workload_score IS NULL " +
                    "AND skill_score IS NULL AND response_score IS NULL AND total_score IS NULL AND rank IS NULL) " +
                    "OR (elimination_reason IS NULL AND distance_score IS NOT NULL AND workload_score IS NOT NULL " +
                    "AND skill_score IS NOT NULL AND response_score IS NOT NULL AND total_score IS NOT NULL AND rank IS NOT NULL)");

                t.HasCheckConstraint(
                    "ck_assignment_decision_candidates_rank",
                    "rank IS NULL OR rank >= 1");
            });
        });
    }

}