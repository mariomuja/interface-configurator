using Microsoft.EntityFrameworkCore;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Models;
using InterfaceConfigurator.Main.Core.Services;
using System.Linq.Expressions;

namespace InterfaceConfigurator.Main.Data;

/// <summary>
/// DbContext for InterfaceConfigDb database (formerly MessageBox)
/// Stores interface configurations, adapter instances, process logs, statistics, features, and users.
/// </summary>
public class InterfaceConfigDbContext : DbContext
{
    public InterfaceConfigDbContext(DbContextOptions<InterfaceConfigDbContext> options)
        : base(options)
    {
    }

    public DbSet<AdapterInstance> AdapterInstances { get; set; }
    public DbSet<ProcessLog> ProcessLogs { get; set; }
    public DbSet<ProcessingStatistics> ProcessingStatistics { get; set; }
    public DbSet<Feature> Features { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<InterfaceConfiguration> InterfaceConfigurations { get; set; }
    public DbSet<AdapterSubscription> AdapterSubscriptions { get; set; }
    public DbSet<ServiceBusMessageLock> ServiceBusMessageLocks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AdapterInstance>(entity =>
        {
            entity.HasKey(e => e.AdapterInstanceGuid);
            entity.Property(e => e.AdapterInstanceGuid).ValueGeneratedNever();
            entity.Property(e => e.datetime_created).HasDefaultValueSql("GETUTCDATE()").IsRequired();
            entity.HasIndex(e => e.InterfaceName);
            entity.HasIndex(e => e.AdapterName);
            entity.HasIndex(e => e.SourceAdapterGuid);
            entity.HasIndex(e => new { e.InterfaceName, e.SourceAdapterGuid });
            // Ignore AdapterType property - it's no longer used, determined from SourceAdapterGuid instead
            entity.Ignore(e => e.IsSourceAdapter);
            entity.Ignore(e => e.IsDestinationAdapter);
        });

        modelBuilder.Entity<ProcessingStatistics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.InterfaceName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.AdapterType).HasMaxLength(50); // Keep for statistics - this is different from AdapterInstance.AdapterType
            entity.Property(e => e.AdapterName).HasMaxLength(100);
            entity.Property(e => e.SourceName).HasMaxLength(500);
            entity.Property(e => e.DestinationName).HasMaxLength(500);
            entity.Property(e => e.SourceFile).HasMaxLength(500);
            entity.HasIndex(e => e.InterfaceName);
            entity.HasIndex(e => e.ProcessingEndTime);
            entity.HasIndex(e => new { e.InterfaceName, e.ProcessingEndTime });
            entity.HasIndex(e => e.AdapterType); // Keep for statistics queries
            entity.HasIndex(e => e.AdapterInstanceGuid);
            entity.HasIndex(e => new { e.InterfaceName, e.AdapterType }); // Keep for statistics queries
        });

        modelBuilder.Entity<Feature>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.DetailedDescription).IsRequired().HasMaxLength(10000);
            entity.Property(e => e.TechnicalDetails).HasMaxLength(10000);
            entity.Property(e => e.TestInstructions).HasMaxLength(10000);
            entity.Property(e => e.KnownIssues).HasMaxLength(5000);
            entity.Property(e => e.Dependencies).HasMaxLength(2000);
            entity.Property(e => e.BreakingChanges).HasMaxLength(5000);
            entity.Property(e => e.Screenshots).HasMaxLength(2000);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Priority).HasMaxLength(50);
            entity.Property(e => e.ImplementedDate).IsRequired();
            entity.Property(e => e.TestComment).HasMaxLength(5000);
            entity.Property(e => e.TestCommentBy).HasMaxLength(100);
            entity.HasIndex(e => e.FeatureNumber).IsUnique();
            entity.HasIndex(e => e.ImplementedDate);
            entity.HasIndex(e => e.Category);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<InterfaceConfiguration>(entity =>
        {
            entity.HasKey(e => e.InterfaceName);
            entity.Property(e => e.InterfaceName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<AdapterSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.AdapterInstanceGuid).IsRequired();
            entity.Property(e => e.InterfaceName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.AdapterName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FilterCriteria).HasMaxLength(2000);
            entity.Property(e => e.IsEnabled).IsRequired();
            entity.Property(e => e.datetime_created).IsRequired();
            entity.HasIndex(e => new { e.AdapterInstanceGuid, e.InterfaceName });
        });

        modelBuilder.Entity<ServiceBusMessageLock>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.MessageId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.LockToken).IsRequired().HasMaxLength(500);
            entity.Property(e => e.TopicName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SubscriptionName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.InterfaceName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.AdapterInstanceGuid).IsRequired();
            entity.Property(e => e.LockAcquiredAt).IsRequired();
            entity.Property(e => e.LockExpiresAt).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => e.LockToken);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AdapterInstanceGuid);
            entity.HasIndex(e => e.LockExpiresAt);
            entity.HasIndex(e => new { e.Status, e.LockExpiresAt });
        });
    }
}

