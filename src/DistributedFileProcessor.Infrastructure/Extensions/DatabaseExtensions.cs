using System.Diagnostics.CodeAnalysis;
using DistributedFileProcessor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DistributedFileProcessor.Infrastructure.Extensions;

[ExcludeFromCodeCoverage(Justification = "Infrastructure wrapper for database initialization")]
public static class DatabaseExtensions
{
    public static async Task ApplyMigrationsAsync(this IHost host)
    {
        using IServiceScope scope = host.Services.CreateScope();
        FileProcessorDbContext context = scope.ServiceProvider.GetRequiredService<FileProcessorDbContext>();

        await context.Database.MigrateAsync();
    }
}