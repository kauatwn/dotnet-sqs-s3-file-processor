using DistributedFileProcessor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DistributedFileProcessor.Infrastructure.Persistence.Configurations;

public sealed class DocumentProcessJobConfiguration : IEntityTypeConfiguration<DocumentProcessJob>
{
    public void Configure(EntityTypeBuilder<DocumentProcessJob> builder)
    {
        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id)
            .ValueGeneratedNever();

        builder.Property(j => j.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(j => j.S3ObjectKey)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(j => j.CreatedAt)
            .IsRequired();

        builder.Property(j => j.ProcessedAt)
            .IsRequired(false);

        builder.Property(j => j.FailureReason)
            .HasMaxLength(2000)
            .IsRequired(false);
    }
}