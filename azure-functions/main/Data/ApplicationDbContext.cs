using Microsoft.EntityFrameworkCore;
using InterfaceConfigurator.Main.Models;

namespace InterfaceConfigurator.Main.Data;

/// <summary>
/// DbContext for the main application database (app-database)
/// This context is used by SqlServerAdapter to create and write to TransportData table
/// TransportData table is created in the main application database, NOT in MessageBox database
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// TransportData table - stores CSV data in the main application database
    /// This table is created in app-database (not MessageBox database)
    /// </summary>
    public DbSet<InterfaceConfigurator.Main.Models.TransportData> TransportData { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<InterfaceConfigurator.Main.Models.TransportData>(entity =>
        {
            entity.HasKey(e => e.PrimaryKey);
            // GUID primary key with DEFAULT NEWID() - NEVER use IDENTITY for primary keys
            // Primary key column name is PrimaryKey to avoid conflicts with CSV 'id' columns
            entity.Property(e => e.PrimaryKey)
                .HasColumnName("PrimaryKey")
                .HasDefaultValueSql("NEWID()")
                .ValueGeneratedNever(); // GUID is generated in code, not by database
            // Every SQL table MUST have a datetime_created column with DEFAULT GETUTCDATE()
            entity.Property(e => e.datetime_created)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();
            entity.HasIndex(e => e.datetime_created).HasDatabaseName("IX_TransportData_datetime_created");
            // CSV columns are stored as individual columns, not as JSON
        });

    }
}


