using Microsoft.EntityFrameworkCore;

namespace RureSubProfiles.Models;

public class ProfilesDbContext : DbContext
{
    public DbSet<Profile> Profiles { get; set; }
    public DbSet<InboxMessage> InboxMessages { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    public ProfilesDbContext(DbContextOptions<ProfilesDbContext> options) : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Profile>().ToTable("Profiles");

        modelBuilder.Entity<Profile>()
            .HasIndex(p => p.UserId)
            .IsUnique();

        modelBuilder.Entity<Profile>()
            .HasIndex(p => p.RedisId)
            .IsUnique();

        modelBuilder.Entity<Profile>()
            .Property(p => p.ShowFollowers)
            .HasDefaultValue(true);

        modelBuilder.Entity<Profile>()
            .Property(p => p.ShowFollowings)
            .HasDefaultValue(true);
    }
}
