using DistributedFileProcessor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DistributedFileProcessor.Infrastructure.Persistence.Configurations;

public sealed class TransactionRecordConfiguration : IEntityTypeConfiguration<TransactionRecord>
{
    public void Configure(EntityTypeBuilder<TransactionRecord> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(j => j.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.JobId)
            .IsRequired();

        builder.HasIndex(t => t.JobId);

        builder.Property(t => t.TransactionDate)
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.AccountId)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasOne<DocumentProcessJob>()
            .WithMany()
            .HasForeignKey(t => t.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}