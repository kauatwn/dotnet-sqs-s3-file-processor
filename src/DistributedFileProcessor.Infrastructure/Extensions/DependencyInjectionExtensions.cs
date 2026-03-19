using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using DistributedFileProcessor.Application.Interfaces;
using DistributedFileProcessor.Domain.Interfaces;
using DistributedFileProcessor.Infrastructure.Configuration;
using DistributedFileProcessor.Infrastructure.Messaging;
using DistributedFileProcessor.Infrastructure.Parsing;
using DistributedFileProcessor.Infrastructure.Persistence;
using DistributedFileProcessor.Infrastructure.Persistence.Repositories;
using DistributedFileProcessor.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using System.Diagnostics.CodeAnalysis;

namespace DistributedFileProcessor.Infrastructure.Extensions;

[ExcludeFromCodeCoverage(Justification = "Pure dependency injection configuration")]
public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddConfiguration(services, configuration);
        AddAwsServices(services);
        AddMessagingServices(services);
        AddPersistence(services, configuration);
        AddParsingServices(services);
        AddResiliencePolicies(services);

        return services;
    }

    private static void AddConfiguration(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SqsOptions>()
            .Bind(configuration.GetSection(SqsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<S3Options>()
            .Bind(configuration.GetSection(S3Options.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    private static void AddAwsServices(IServiceCollection services)
    {
        BasicAWSCredentials credentials = new(accessKey: "test", secretKey: "test");

        services.AddSingleton<IAmazonSQS>(sp =>
        {
            string localStackUrl = sp.GetRequiredService<IConfiguration>().GetValue<string>("LocalStack:ServiceUrl")
                ?? throw new InvalidOperationException("The configuration 'LocalStack:ServiceUrl' is required but was not found.");

            AmazonSQSConfig config = new()
            {
                ServiceURL = localStackUrl,
                AuthenticationRegion = "us-east-1"
            };

            return new AmazonSQSClient(credentials, config);
        });

        services.AddSingleton<IAmazonS3>(sp =>
        {
            string localStackUrl = sp.GetRequiredService<IConfiguration>().GetValue<string>("LocalStack:ServiceUrl")
                ?? throw new InvalidOperationException("The configuration 'LocalStack:ServiceUrl' is required but was not found.");

            AmazonS3Config config = new()
            {
                ServiceURL = localStackUrl,
                AuthenticationRegion = "us-east-1",
                ForcePathStyle = true
            };

            return new AmazonS3Client(credentials, config);
        });

        services.AddScoped<IFileStorageService, S3FileStorageService>();
    }

    private static void AddMessagingServices(IServiceCollection services)
    {
        services.AddSingleton<IMessagePublisher, SqsMessagePublisher>();
        services.AddSingleton<IMessageConsumer, SqsMessageConsumer>();
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FileProcessorDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"), npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            })
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IDocumentProcessJobRepository, DocumentProcessJobRepository>();
        services.AddScoped<ITransactionRecordRepository, TransactionRecordRepository>();
    }

    private static void AddParsingServices(IServiceCollection services)
    {
        services.AddScoped<ITransactionFileParser, CsvTransactionFileParser>();
    }

    private static void AddResiliencePolicies(IServiceCollection services)
    {
        services.AddResiliencePipeline("S3Pipeline", builder =>
        {
            builder.AddTimeout(TimeSpan.FromSeconds(15));
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<AmazonS3Exception>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            });

            builder.AddTimeout(TimeSpan.FromSeconds(5));
        });

        services.AddResiliencePipeline("SqsPipeline", builder =>
        {
            builder.AddTimeout(TimeSpan.FromSeconds(5));

            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<AmazonSQSException>()
                    .Handle<TimeoutRejectedException>(),
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            });

            builder.AddTimeout(TimeSpan.FromSeconds(2));
        });
    }
}