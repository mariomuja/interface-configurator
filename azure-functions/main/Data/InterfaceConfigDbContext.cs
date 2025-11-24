using Microsoft.EntityFrameworkCore;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Models;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main.Data;

/// <summary>
/// DbContext for InterfaceConfigDb database (formerly MessageBox)
/// The database stores:
/// - Interface configurations
/// - Adapter instances
/// - Process logs
/// - Processing statistics
/// - Features and Users
/// 
/// Note: Messaging is now handled via Azure Service Bus, not this database
/// </summary>
public class InterfaceConfigDbContext : DbContext
{
    public InterfaceConfigDbContext(DbContextOptions<InterfaceConfigDbContext> options)
        : base(options)
    {
    }

    // Adapter Instances
    public DbSet<AdapterInstance> AdapterInstances { get; set; }
    
    // Process Logs
    public DbSet<Models.ProcessLog> ProcessLogs { get; set; }
    public DbSet<ProcessingStatistics> ProcessingStatistics { get; set; }
    
    // Feature Management (moved from ApplicationDbContext)
    public DbSet<Models.Feature> Features { get; set; }
    public DbSet<Models.User> Users { get; set; }
    
    // Interface Configuration (moved from Blob Storage to database)
    public DbSet<InterfaceConfiguration> InterfaceConfigurations { get; set; }
    public DbSet<SourceAdapterInstance> SourceAdapterInstances { get; set; }
    public DbSet<DestinationAdapterInstance> DestinationAdapterInstances { get; set; }
    
    // Adapter Subscriptions (deprecated - kept for backward compatibility, now using Service Bus)
    public DbSet<AdapterSubscription> AdapterSubscriptions { get; set; }
    
    // Service Bus Message Lock Tracking
    public DbSet<ServiceBusMessageLock> ServiceBusMessageLocks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
            
            // Ignore Dictionary properties - they are not stored directly in the database
            // Instead, SourceAdapterInstances and DestinationAdapterInstances are stored in separate tables
            entity.Ignore(e => e.Sources);
            entity.Ignore(e => e.Destinations);
            
            // Ignore all obsolete properties - they are not stored in the database
            // These properties are only used for backward compatibility during migration
#pragma warning disable CS0618 // Type or member is obsolete
            entity.Ignore(e => e.SourceAdapterName);
            entity.Ignore(e => e.SourceConfiguration);
            entity.Ignore(e => e.DestinationAdapterName);
            entity.Ignore(e => e.DestinationConfiguration);
            entity.Ignore(e => e.SourceIsEnabled);
            entity.Ignore(e => e.DestinationIsEnabled);
            entity.Ignore(e => e.SourceInstanceName);
            entity.Ignore(e => e.DestinationInstanceName);
            entity.Ignore(e => e.SourceAdapterInstanceGuid);
            entity.Ignore(e => e.DestinationAdapterInstanceGuid);
            entity.Ignore(e => e.SourceReceiveFolder);
            entity.Ignore(e => e.SourceFileMask);
            entity.Ignore(e => e.SourceBatchSize);
            entity.Ignore(e => e.SourceFieldSeparator);
            entity.Ignore(e => e.CsvData);
            entity.Ignore(e => e.CsvAdapterType);
            entity.Ignore(e => e.CsvPollingInterval);
            entity.Ignore(e => e.SftpHost);
            entity.Ignore(e => e.SftpPort);
            entity.Ignore(e => e.SftpUsername);
            entity.Ignore(e => e.SftpPassword);
            entity.Ignore(e => e.SftpSshKey);
            entity.Ignore(e => e.SftpFolder);
            entity.Ignore(e => e.SftpFileMask);
            entity.Ignore(e => e.SftpMaxConnectionPoolSize);
            entity.Ignore(e => e.SftpFileBufferSize);
            entity.Ignore(e => e.SqlServerName);
            entity.Ignore(e => e.SqlDatabaseName);
            entity.Ignore(e => e.SqlUserName);
            entity.Ignore(e => e.SqlPassword);
            entity.Ignore(e => e.SqlIntegratedSecurity);
            entity.Ignore(e => e.SqlResourceGroup);
            entity.Ignore(e => e.SqlTableName);
            entity.Ignore(e => e.SqlPollingStatement);
            entity.Ignore(e => e.SqlPollingInterval);
            entity.Ignore(e => e.SqlUseTransaction);
            entity.Ignore(e => e.SqlBatchSize);
            entity.Ignore(e => e.SqlCommandTimeout);
            entity.Ignore(e => e.SqlFailOnBadStatement);
            entity.Ignore(e => e.DestinationReceiveFolder);
            entity.Ignore(e => e.DestinationFileMask);
            entity.Ignore(e => e.SapApplicationServer);
            entity.Ignore(e => e.SapSystemNumber);
            entity.Ignore(e => e.SapClient);
            entity.Ignore(e => e.SapUsername);
            entity.Ignore(e => e.SapPassword);
            entity.Ignore(e => e.SapLanguage);
            entity.Ignore(e => e.SapIdocType);
            entity.Ignore(e => e.SapIdocMessageType);
            entity.Ignore(e => e.SapIdocFilter);
            entity.Ignore(e => e.SapReceiverPort);
            entity.Ignore(e => e.SapReceiverPartner);
            entity.Ignore(e => e.SapPollingInterval);
            entity.Ignore(e => e.SapBatchSize);
            entity.Ignore(e => e.SapConnectionTimeout);
            entity.Ignore(e => e.SapUseRfc);
            entity.Ignore(e => e.SapRfcDestination);
            entity.Ignore(e => e.SapRfcFunctionModule);
            entity.Ignore(e => e.SapRfcParameters);
            entity.Ignore(e => e.SapODataServiceUrl);
            entity.Ignore(e => e.SapRestApiEndpoint);
            entity.Ignore(e => e.SapUseOData);
            entity.Ignore(e => e.SapUseRestApi);
            entity.Ignore(e => e.Dynamics365TenantId);
            entity.Ignore(e => e.Dynamics365ClientId);
            entity.Ignore(e => e.Dynamics365ClientSecret);
            entity.Ignore(e => e.Dynamics365InstanceUrl);
            entity.Ignore(e => e.Dynamics365EntityName);
            entity.Ignore(e => e.Dynamics365ODataFilter);
            entity.Ignore(e => e.Dynamics365PollingInterval);
            entity.Ignore(e => e.Dynamics365BatchSize);
            entity.Ignore(e => e.Dynamics365PageSize);
            entity.Ignore(e => e.Dynamics365UseBatch);
            entity.Ignore(e => e.CrmOrganizationUrl);
            entity.Ignore(e => e.CrmUsername);
            entity.Ignore(e => e.CrmPassword);
            entity.Ignore(e => e.CrmEntityName);
            entity.Ignore(e => e.CrmFetchXml);
            entity.Ignore(e => e.CrmPollingInterval);
            entity.Ignore(e => e.CrmBatchSize);
            entity.Ignore(e => e.CrmUseBatch);
#pragma warning restore CS0618 // Type or member is obsolete
        });

        // Configure SourceAdapterInstance
        modelBuilder.Entity<SourceAdapterInstance>(entity =>
        {
            entity.HasKey(e => e.AdapterInstanceGuid);
            entity.Property(e => e.AdapterInstanceGuid)
                .HasColumnName("AdapterInstanceGuid")
                .ValueGeneratedNever();
            entity.Property(e => e.InstanceName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.AdapterName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Configuration).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.AdapterInstanceGuid).IsUnique();
        });

        // Configure DestinationAdapterInstance
        modelBuilder.Entity<DestinationAdapterInstance>(entity =>
        {
            entity.HasKey(e => e.AdapterInstanceGuid);
            entity.Property(e => e.AdapterInstanceGuid)
                .HasColumnName("AdapterInstanceGuid")
                .ValueGeneratedNever();
            entity.Property(e => e.InstanceName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.AdapterName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Configuration).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.AdapterInstanceGuid).IsUnique();
            entity.Property(e => e.SourceAdapterSubscription);
        });
        
        // Configure AdapterSubscription (deprecated - kept for backward compatibility)
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
            entity.HasIndex(e => new { e.AdapterInstanceGuid, e.InterfaceName }).HasDatabaseName("IX_AdapterSubscriptions_AdapterInstanceGuid_InterfaceName");
        });
        
        // Configure ServiceBusMessageLock
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
            entity.HasIndex(e => e.MessageId).HasDatabaseName("IX_ServiceBusMessageLocks_MessageId");
            entity.HasIndex(e => e.LockToken).HasDatabaseName("IX_ServiceBusMessageLocks_LockToken");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_ServiceBusMessageLocks_Status");
            entity.HasIndex(e => e.AdapterInstanceGuid).HasDatabaseName("IX_ServiceBusMessageLocks_AdapterInstanceGuid");
            entity.HasIndex(e => e.LockExpiresAt).HasDatabaseName("IX_ServiceBusMessageLocks_LockExpiresAt");
            entity.HasIndex(e => new { e.Status, e.LockExpiresAt }).HasDatabaseName("IX_ServiceBusMessageLocks_Status_LockExpiresAt");
        });
    }
}

