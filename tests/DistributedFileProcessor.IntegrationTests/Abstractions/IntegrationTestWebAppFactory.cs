using DistributedFileProcessor.Worker;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.LocalStack;
using Testcontainers.PostgreSql;

namespace DistributedFileProcessor.IntegrationTests.Abstractions;

public class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("fileprocessor_db_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly LocalStackContainer _localStackContainer = new LocalStackBuilder("localstack/localstack:2026.3.0")
        .WithEnvironment("LOCALSTACK_ACKNOWLEDGE_ACCOUNT_REQUIREMENT", "1")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            string localStackUrl = _localStackContainer.GetConnectionString();

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _dbContainer.GetConnectionString(),
                ["LocalStack:ServiceUrl"] = localStackUrl,

                ["AWS:SQS:QueueUrl"] = $"{localStackUrl}/000000000000/integration-test-queue",
                ["AWS:SQS:QueueName"] = "integration-test-queue",
                ["AWS:SQS:DlqName"] = "integration-test-dlq",

                ["AWS:S3:BucketName"] = "integration-test-bucket"
            });
        });

        builder.ConfigureServices(services => { services.AddHostedService<DocumentProcessingWorker>(); });
    }

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_dbContainer.StartAsync(), _localStackContainer.StartAsync());
    }
}