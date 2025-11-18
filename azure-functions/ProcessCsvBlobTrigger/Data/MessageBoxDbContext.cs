using Microsoft.EntityFrameworkCore;
using ProcessCsvBlobTrigger.Core.Models;
using ProcessCsvBlobTrigger.Models;

namespace ProcessCsvBlobTrigger.Data;

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
    public DbSet<ProcessCsvBlobTrigger.Models.ProcessLog> ProcessLogs { get; set; }

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
        modelBuilder.Entity<ProcessCsvBlobTrigger.Models.ProcessLog>(entity =>
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
    }
}

