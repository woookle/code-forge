using Microsoft.EntityFrameworkCore;
using CodeForgeAPI.Models;

namespace CodeForgeAPI.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<Entity> Entities { get; set; } = null!;
    public DbSet<Field> Fields { get; set; } = null!;
    public DbSet<Relationship> Relationships { get; set; } = null!;
    public DbSet<VerificationToken> VerificationTokens { get; set; } = null!;
    public DbSet<GenerationHistory> GenerationHistories { get; set; } = null!;
    public DbSet<UserAchievement> UserAchievements { get; set; } = null!;
    public DbSet<AccountActivity> AccountActivities { get; set; } = null!;
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Users configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        
        // Projects configuration
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Projects)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Entities configuration
        modelBuilder.Entity<Entity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => new { e.ProjectId, e.Name }).IsUnique();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Entities)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Fields configuration
        modelBuilder.Entity<Field>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EntityId);
            entity.HasIndex(e => new { e.EntityId, e.Name }).IsUnique();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Entity)
                .WithMany(ent => ent.Fields)
                .HasForeignKey(e => e.EntityId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.RelatedEntity)
                .WithMany()
                .HasForeignKey(e => e.RelatedEntityId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        
        // Relationships configuration
        modelBuilder.Entity<Relationship>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SourceEntityId);
            entity.HasIndex(e => e.TargetEntityId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.SourceEntity)
                .WithMany(ent => ent.SourceRelationships)
                .HasForeignKey(e => e.SourceEntityId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.TargetEntity)
                .WithMany(ent => ent.TargetRelationships)
                .HasForeignKey(e => e.TargetEntityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // GenerationHistory configuration
        modelBuilder.Entity<GenerationHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ProjectId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // UserAchievement configuration
        modelBuilder.Entity<UserAchievement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.AchievementId }).IsUnique();
            entity.Property(e => e.UnlockedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AccountActivity configuration
        modelBuilder.Entity<AccountActivity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
