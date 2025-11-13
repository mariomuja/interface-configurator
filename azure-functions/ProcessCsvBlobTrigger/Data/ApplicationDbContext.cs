using Microsoft.EntityFrameworkCore;
using ProcessCsvBlobTrigger.Models;

namespace ProcessCsvBlobTrigger.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<TransportData> TransportData { get; set; }
    public DbSet<ProcessLog> ProcessLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TransportData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_TransportData_CreatedAt");
        });

        modelBuilder.Entity<ProcessLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp).HasDatabaseName("IX_ProcessLogs_Timestamp");
            entity.HasIndex(e => e.Level).HasDatabaseName("IX_ProcessLogs_Level");
        });
    }
}


