using DistributedFileProcessor.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DistributedFileProcessor.Infrastructure.Persistence;

public sealed class FileProcessorDbContext(DbContextOptions<FileProcessorDbContext> options) : DbContext(options)
{
    public DbSet<DocumentProcessJob> DocumentProcessJobs => Set<DocumentProcessJob>();
    public DbSet<TransactionRecord> TransactionRecords => Set<TransactionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FileProcessorDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}