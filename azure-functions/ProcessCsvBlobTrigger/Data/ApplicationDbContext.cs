using Microsoft.EntityFrameworkCore;
using ProcessCsvBlobTrigger.Models;

namespace ProcessCsvBlobTrigger.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProcessCsvBlobTrigger.Models.TransportData> TransportData { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProcessCsvBlobTrigger.Models.TransportData>(entity =>
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


