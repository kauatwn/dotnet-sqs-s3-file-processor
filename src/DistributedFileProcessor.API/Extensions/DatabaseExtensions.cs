using System.Diagnostics.CodeAnalysis;
using DistributedFileProcessor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DistributedFileProcessor.API.Extensions;

[ExcludeFromCodeCoverage(Justification = "Infrastructure wrapper for database initialization")]
public static class DatabaseExtensions
{
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        FileProcessorDbContext context = scope.ServiceProvider.GetRequiredService<FileProcessorDbContext>();

        await context.Database.MigrateAsync();
    }
}