using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// Stores analysis metadata only. Packet captures live in object storage and are never
/// persisted here.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Analysis> Analyses => Set<Analysis>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Username).HasMaxLength(256);
            entity.Property(u => u.Email).HasMaxLength(320);
            entity.HasIndex(u => u.Email);
        });

        modelBuilder.Entity<Analysis>(entity =>
        {
            entity.ToTable("analyses");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.FileName).HasMaxLength(260).IsRequired();
            entity.Property(a => a.ContentType).HasMaxLength(128);
            entity.Property(a => a.BucketName).HasMaxLength(128).IsRequired();
            entity.Property(a => a.ObjectKey).HasMaxLength(1024).IsRequired();
            entity.Property(a => a.UploadId).HasMaxLength(256);
            entity.Property(a => a.Prompt).HasMaxLength(4000);
            entity.Property(a => a.Verdict).HasMaxLength(1024);
            entity.Property(a => a.ErrorMessage).HasMaxLength(2048);

            // Persisted as a string so status values stay readable in the database and
            // reordering the enum cannot silently remap existing rows.
            entity.Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            // jsonb rather than text: Postgres can index and query into the Zeek output later.
            entity.Property(a => a.ZeekResultJson).HasColumnType("jsonb");

            entity.HasIndex(a => new { a.UserId, a.CreatedAt });
            entity.HasIndex(a => a.ObjectKey).IsUnique();

            entity.HasOne(a => a.User)
                .WithMany(u => u.Analyses)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
