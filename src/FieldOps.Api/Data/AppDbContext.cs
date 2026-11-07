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
    }

}