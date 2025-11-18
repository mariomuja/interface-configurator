using Microsoft.EntityFrameworkCore;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Models;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main.Data;

/// <summary>
/// DbContext for MessageBox database
/// </summary>
public class MessageBoxDbContext : DbContext
{
    public MessageBoxDbContext(DbContextOptions<MessageBoxDbContext> options)
        : base(options)
    {
    }

    public DbSet<MessageBoxMessage> Messages { get; set; }
    public DbSet<MessageSubscription> MessageSubscriptions { get; set; }
    public DbSet<AdapterInstance> AdapterInstances { get; set; }
    public DbSet<Models.ProcessLog> ProcessLogs { get; set; }
    public DbSet<ProcessingStatistics> ProcessingStatistics { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure MessageBoxMessage
        modelBuilder.Entity<MessageBoxMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId);
            entity.Property(e => e.MessageId)
                .HasColumnName("MessageId")
                .HasDefaultValueSql("NEWID()")
                .ValueGeneratedNever();
            entity.Property(e => e.datetime_created)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();
            entity.HasIndex(e => e.InterfaceName).HasDatabaseName("IX_Messages_InterfaceName");
            entity.HasIndex(e => e.AdapterName).HasDatabaseName("IX_Messages_AdapterName");
            entity.HasIndex(e => e.AdapterType).HasDatabaseName("IX_Messages_AdapterType");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_Messages_Status");
            entity.HasIndex(e => e.datetime_created).HasDatabaseName("IX_Messages_datetime_created");
            entity.HasIndex(e => new { e.Status, e.InterfaceName }).HasDatabaseName("IX_Messages_Status_InterfaceName");
            entity.HasIndex(e => e.AdapterInstanceGuid).HasDatabaseName("IX_Messages_AdapterInstanceGuid");
            // Unique index on MessageHash for idempotency (prevent duplicate messages)
            // Note: This is a non-unique index to allow multiple messages with same hash if needed
            // The idempotency check in code handles duplicate detection
            entity.HasIndex(e => e.MessageHash).HasDatabaseName("IX_Messages_MessageHash");
        });

        // Configure MessageSubscription
        modelBuilder.Entity<MessageSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.datetime_created)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();
            entity.HasIndex(e => e.MessageId).HasDatabaseName("IX_MessageSubscriptions_MessageId");
            entity.HasIndex(e => new { e.MessageId, e.SubscriberAdapterName }).HasDatabaseName("IX_MessageSubscriptions_MessageId_Subscriber");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_MessageSubscriptions_Status");
        });

        // Configure AdapterInstance
        modelBuilder.Entity<AdapterInstance>(entity =>
        {
            entity.HasKey(e => e.AdapterInstanceGuid);
            entity.Property(e => e.AdapterInstanceGuid)
                .HasColumnName("AdapterInstanceGuid")
                .ValueGeneratedNever();
            entity.Property(e => e.datetime_created)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();
            entity.HasIndex(e => e.InterfaceName).HasDatabaseName("IX_AdapterInstances_InterfaceName");
            entity.HasIndex(e => e.AdapterName).HasDatabaseName("IX_AdapterInstances_AdapterName");
            entity.HasIndex(e => e.AdapterType).HasDatabaseName("IX_AdapterInstances_AdapterType");
            entity.HasIndex(e => new { e.InterfaceName, e.AdapterType }).HasDatabaseName("IX_AdapterInstances_InterfaceName_AdapterType");
        });

        // Configure ProcessLog
        modelBuilder.Entity<Models.ProcessLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.datetime_created)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();
            entity.HasIndex(e => e.datetime_created).HasDatabaseName("IX_ProcessLogs_datetime_created");
            entity.HasIndex(e => e.Level).HasDatabaseName("IX_ProcessLogs_Level");
            entity.HasIndex(e => e.InterfaceName).HasDatabaseName("IX_ProcessLogs_InterfaceName");
            entity.HasIndex(e => e.MessageId).HasDatabaseName("IX_ProcessLogs_MessageId");
        });

        // Configure ProcessingStatistics
        modelBuilder.Entity<ProcessingStatistics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.ProcessingStartTime)
                .HasColumnName("ProcessingStartTime")
                .IsRequired();
            entity.Property(e => e.ProcessingEndTime)
                .HasColumnName("ProcessingEndTime")
                .IsRequired();
            entity.HasIndex(e => e.InterfaceName).HasDatabaseName("IX_ProcessingStatistics_InterfaceName");
            entity.HasIndex(e => e.ProcessingEndTime).HasDatabaseName("IX_ProcessingStatistics_ProcessingEndTime");
            entity.HasIndex(e => new { e.InterfaceName, e.ProcessingEndTime }).HasDatabaseName("IX_ProcessingStatistics_InterfaceName_ProcessingEndTime");
        });
    }
}

