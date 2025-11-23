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
    public DbSet<MessageSubscription> MessageSubscriptions { get; set; } // Legacy - kept for backward compatibility
    public DbSet<AdapterSubscription> AdapterSubscriptions { get; set; } // New: BizTalk-style subscriptions (filter criteria)
    public DbSet<MessageProcessing> MessageProcessing { get; set; } // New: Tracks message processing status
    public DbSet<AdapterInstance> AdapterInstances { get; set; }
    public DbSet<Models.ProcessLog> ProcessLogs { get; set; }
    public DbSet<ProcessingStatistics> ProcessingStatistics { get; set; }
    
    // Feature Management (moved from ApplicationDbContext)
    public DbSet<Models.Feature> Features { get; set; }
    public DbSet<Models.User> Users { get; set; }
    
    // Interface Configuration (moved from Blob Storage to database)
    public DbSet<InterfaceConfiguration> InterfaceConfigurations { get; set; }
    public DbSet<SourceAdapterInstance> SourceAdapterInstances { get; set; }
    public DbSet<DestinationAdapterInstance> DestinationAdapterInstances { get; set; }

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

        // Configure AdapterSubscription (New: BizTalk-style subscriptions - filter criteria)
        modelBuilder.Entity<AdapterSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.AdapterInstanceGuid)
                .IsRequired();
            entity.Property(e => e.InterfaceName)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.AdapterName)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValue(true);
            entity.Property(e => e.datetime_created)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();
            entity.HasIndex(e => e.AdapterInstanceGuid).HasDatabaseName("IX_AdapterSubscriptions_AdapterInstanceGuid");
            entity.HasIndex(e => e.InterfaceName).HasDatabaseName("IX_AdapterSubscriptions_InterfaceName");
            entity.HasIndex(e => new { e.AdapterInstanceGuid, e.InterfaceName }).HasDatabaseName("IX_AdapterSubscriptions_AdapterInstanceGuid_InterfaceName");
            entity.HasIndex(e => e.IsEnabled).HasDatabaseName("IX_AdapterSubscriptions_IsEnabled");
        });

        // Configure MessageProcessing (New: Tracks message processing status)
        modelBuilder.Entity<MessageProcessing>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.MessageId)
                .IsRequired();
            entity.Property(e => e.AdapterInstanceGuid)
                .IsRequired();
            entity.Property(e => e.InterfaceName)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.AdapterName)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.datetime_created)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();
            entity.HasIndex(e => e.MessageId).HasDatabaseName("IX_MessageProcessing_MessageId");
            entity.HasIndex(e => e.AdapterInstanceGuid).HasDatabaseName("IX_MessageProcessing_AdapterInstanceGuid");
            entity.HasIndex(e => new { e.MessageId, e.AdapterInstanceGuid }).HasDatabaseName("IX_MessageProcessing_MessageId_AdapterInstanceGuid");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_MessageProcessing_Status");
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

        // Configure Feature (moved from ApplicationDbContext)
        modelBuilder.Entity<Models.Feature>(entity =>
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
            entity.HasIndex(e => e.ImplementedDate).HasDatabaseName("IX_Features_ImplementedDate");
            entity.HasIndex(e => e.Category).HasDatabaseName("IX_Features_Category");
        });

        // Configure User (moved from ApplicationDbContext)
        modelBuilder.Entity<Models.User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Username).IsUnique();
        });

        // Configure InterfaceConfiguration (moved from Blob Storage)
        modelBuilder.Entity<InterfaceConfiguration>(entity =>
        {
            entity.HasKey(e => e.InterfaceName);
            entity.Property(e => e.InterfaceName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_InterfaceConfigurations_CreatedAt");
        });

        // Configure SourceAdapterInstance (moved from Blob Storage)
        // Note: In a full implementation, this would have a foreign key to InterfaceConfiguration
        // For now, we store InterfaceName as a property (would need to add this to the model)
        modelBuilder.Entity<SourceAdapterInstance>(entity =>
        {
            entity.HasKey(e => e.AdapterInstanceGuid);
            entity.Property(e => e.AdapterInstanceGuid).ValueGeneratedNever();
            entity.Property(e => e.InstanceName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.AdapterName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Configuration).HasMaxLength(1000);
            entity.Property(e => e.SourceReceiveFolder).HasMaxLength(500);
            entity.Property(e => e.SourceFileMask).HasMaxLength(100);
            entity.Property(e => e.SourceFieldSeparator).HasMaxLength(10);
            entity.Property(e => e.CsvData).HasMaxLength(10000000); // 10MB
            entity.Property(e => e.CsvAdapterType).HasMaxLength(20);
            entity.Property(e => e.SftpHost).HasMaxLength(500);
            entity.Property(e => e.SftpUsername).HasMaxLength(200);
            entity.Property(e => e.SftpPassword).HasMaxLength(500);
            entity.Property(e => e.SftpSshKey).HasMaxLength(5000);
            entity.Property(e => e.SftpFolder).HasMaxLength(500);
            entity.Property(e => e.SftpFileMask).HasMaxLength(100);
            entity.Property(e => e.SqlServerName).HasMaxLength(500);
            entity.Property(e => e.SqlDatabaseName).HasMaxLength(200);
            entity.Property(e => e.SqlUserName).HasMaxLength(200);
            entity.Property(e => e.SqlPassword).HasMaxLength(500);
            entity.Property(e => e.SqlResourceGroup).HasMaxLength(200);
            entity.Property(e => e.SqlPollingStatement).HasMaxLength(2000);
            entity.Property(e => e.SqlTableName).HasMaxLength(200);
            
            // SAP Adapter Properties
            entity.Property(e => e.SapApplicationServer).HasMaxLength(500);
            entity.Property(e => e.SapSystemNumber).HasMaxLength(200);
            entity.Property(e => e.SapClient).HasMaxLength(200);
            entity.Property(e => e.SapUsername).HasMaxLength(200);
            entity.Property(e => e.SapPassword).HasMaxLength(500);
            entity.Property(e => e.SapLanguage).HasMaxLength(200);
            entity.Property(e => e.SapIdocType).HasMaxLength(500);
            entity.Property(e => e.SapIdocMessageType).HasMaxLength(500);
            entity.Property(e => e.SapIdocFilter).HasMaxLength(2000);
            entity.Property(e => e.SapRfcDestination).HasMaxLength(500);
            
            // Dynamics 365 Adapter Properties
            entity.Property(e => e.Dynamics365TenantId).HasMaxLength(500);
            entity.Property(e => e.Dynamics365ClientId).HasMaxLength(500);
            entity.Property(e => e.Dynamics365ClientSecret).HasMaxLength(500);
            entity.Property(e => e.Dynamics365InstanceUrl).HasMaxLength(500);
            entity.Property(e => e.Dynamics365EntityName).HasMaxLength(200);
            entity.Property(e => e.Dynamics365ODataFilter).HasMaxLength(2000);
            
            // CRM Adapter Properties
            entity.Property(e => e.CrmOrganizationUrl).HasMaxLength(500);
            entity.Property(e => e.CrmUsername).HasMaxLength(200);
            entity.Property(e => e.CrmPassword).HasMaxLength(500);
            entity.Property(e => e.CrmEntityName).HasMaxLength(200);
            entity.Property(e => e.CrmFetchXml).HasMaxLength(2000);
            
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt);
            entity.HasIndex(e => e.AdapterInstanceGuid).IsUnique();
            entity.HasIndex(e => e.InstanceName).HasDatabaseName("IX_SourceAdapterInstances_InstanceName");
            entity.HasIndex(e => e.AdapterName).HasDatabaseName("IX_SourceAdapterInstances_AdapterName");
        });

        // Configure DestinationAdapterInstance (moved from Blob Storage)
        // Note: In a full implementation, this would have a foreign key to InterfaceConfiguration
        modelBuilder.Entity<DestinationAdapterInstance>(entity =>
        {
            entity.HasKey(e => e.AdapterInstanceGuid);
            entity.Property(e => e.AdapterInstanceGuid).ValueGeneratedNever();
            entity.Property(e => e.InstanceName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.AdapterName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Configuration).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.DestinationReceiveFolder).HasMaxLength(500);
            entity.Property(e => e.DestinationFileMask).HasMaxLength(100);
            entity.Property(e => e.SqlServerName).HasMaxLength(500);
            entity.Property(e => e.SqlDatabaseName).HasMaxLength(200);
            entity.Property(e => e.SqlUserName).HasMaxLength(200);
            entity.Property(e => e.SqlPassword).HasMaxLength(500);
            entity.Property(e => e.SqlResourceGroup).HasMaxLength(200);
            entity.Property(e => e.SqlTableName).HasMaxLength(200);
            
            // SAP Adapter Properties
            entity.Property(e => e.SapApplicationServer).HasMaxLength(500);
            entity.Property(e => e.SapSystemNumber).HasMaxLength(200);
            entity.Property(e => e.SapClient).HasMaxLength(200);
            entity.Property(e => e.SapUsername).HasMaxLength(200);
            entity.Property(e => e.SapPassword).HasMaxLength(500);
            entity.Property(e => e.SapLanguage).HasMaxLength(200);
            entity.Property(e => e.SapIdocType).HasMaxLength(500);
            entity.Property(e => e.SapIdocMessageType).HasMaxLength(500);
            entity.Property(e => e.SapReceiverPort).HasMaxLength(500);
            entity.Property(e => e.SapReceiverPartner).HasMaxLength(500);
            entity.Property(e => e.SapRfcDestination).HasMaxLength(500);
            
            // Dynamics 365 Adapter Properties
            entity.Property(e => e.Dynamics365TenantId).HasMaxLength(500);
            entity.Property(e => e.Dynamics365ClientId).HasMaxLength(500);
            entity.Property(e => e.Dynamics365ClientSecret).HasMaxLength(500);
            entity.Property(e => e.Dynamics365InstanceUrl).HasMaxLength(500);
            entity.Property(e => e.Dynamics365EntityName).HasMaxLength(200);
            
            // CRM Adapter Properties
            entity.Property(e => e.CrmOrganizationUrl).HasMaxLength(500);
            entity.Property(e => e.CrmUsername).HasMaxLength(200);
            entity.Property(e => e.CrmPassword).HasMaxLength(500);
            entity.Property(e => e.CrmEntityName).HasMaxLength(200);
            
            // JQ Transformation Properties
            entity.Property(e => e.JQScriptFile).HasMaxLength(1000);
            entity.Property(e => e.SourceAdapterSubscription);
            
            // SQL Server Custom Statement Properties
            entity.Property(e => e.InsertStatement).HasMaxLength(5000);
            entity.Property(e => e.UpdateStatement).HasMaxLength(5000);
            entity.Property(e => e.DeleteStatement).HasMaxLength(5000);
            
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt);
            entity.HasIndex(e => e.AdapterInstanceGuid).IsUnique();
            entity.HasIndex(e => e.InstanceName).HasDatabaseName("IX_DestinationAdapterInstances_InstanceName");
            entity.HasIndex(e => e.AdapterName).HasDatabaseName("IX_DestinationAdapterInstances_AdapterName");
        });
    }
}

