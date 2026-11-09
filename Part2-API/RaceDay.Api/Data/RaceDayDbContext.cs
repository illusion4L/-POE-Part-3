using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Role>(e =>
        {
            e.ToTable("Role");
            e.HasKey(x => x.RoleId);
            e.Property(x => x.RoleName).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.HasIndex(x => x.RoleName).IsUnique().HasDatabaseName("UQ_Role_RoleName");
            e.HasData(
                new Role { RoleId = 1, RoleName = "Organiser" },
                new Role { RoleId = 2, RoleName = "Participant" });
        });

        mb.Entity<User>(e =>
        {
            e.ToTable("User");
            e.HasKey(x => x.UserId);
            e.Property(x => x.FullName).HasMaxLength(100).IsUnicode(false).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsUnicode(false).IsRequired();
            e.Property(x => x.PasswordHash).HasColumnType("varbinary(max)").IsRequired();
            e.Property(x => x.PasswordSalt).HasColumnType("varbinary(max)").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UQ_User_Email");
            e.HasOne(x => x.Role).WithMany(r => r.Users)
                .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Event>(e =>
        {
            e.ToTable("Event", t =>
            {
                t.HasCheckConstraint("CK_Event_EventType", "[EventType] IN ('Run', 'Walk', 'Cycle')");
                t.HasCheckConstraint("CK_Event_Distance", "[Distance] > 0");
            });
            e.HasKey(x => x.EventId);
            e.Property(x => x.Name).HasMaxLength(150).IsUnicode(false).IsRequired();
            e.Property(x => x.Description).HasColumnType("varchar(max)");
            e.Property(x => x.EventDate).HasColumnType("datetime2");
            e.Property(x => x.Location).HasMaxLength(150).IsUnicode(false).IsRequired();
            e.Property(x => x.Distance).HasColumnType("decimal(6,2)");
            e.Property(x => x.EventType).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(x => x.BannerBlobName).HasMaxLength(260).IsUnicode(false);
            e.HasOne(x => x.Organiser).WithMany(u => u.OrganisedEvents)
                .HasForeignKey(x => x.OrganiserId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Category>(e =>
        {
            e.ToTable("Category", t =>
                t.HasCheckConstraint("CK_Category_Age",
                    "[MinAge] IS NULL OR [MaxAge] IS NULL OR [MinAge] <= [MaxAge]"));
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.Name).HasMaxLength(50).IsUnicode(false).IsRequired();
            e.Property(x => x.CategoryDistance).HasColumnType("decimal(6,2)");
            e.HasOne(x => x.Event).WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolment", t =>
                t.HasCheckConstraint("CK_Enrolment_Status", "[Status] IN ('Pending', 'Confirmed', 'Cancelled')"));
            e.HasKey(x => x.EnrolmentId);
            e.Property(x => x.EnrolmentDate).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.Status).HasMaxLength(20).IsUnicode(false).IsRequired().HasDefaultValue(EnrolmentStatuses.Pending);
            e.HasIndex(x => new { x.ParticipantId, x.EventId }).IsUnique()
                .HasDatabaseName("UQ_Enrolment_Participant_Event");
            e.HasOne(x => x.Participant).WithMany(u => u.Enrolments)
                .HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Event).WithMany(ev => ev.Enrolments)
                .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Result>(e =>
        {
            e.ToTable("Result");
            e.HasKey(x => x.ResultId);
            e.Property(x => x.FinishTime).HasColumnType("time");
            e.Property(x => x.RecordedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(x => x.Enrolment).WithOne(en => en.Result)
                .HasForeignKey<Result>(x => x.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.EnrolmentId).IsUnique().HasDatabaseName("UQ_Result_Enrolment");
        });
    }
}
