using DistributedFileProcessor.Application.UseCases.Documents.GetStatus;
using DistributedFileProcessor.Application.UseCases.Documents.Process;
using DistributedFileProcessor.Application.UseCases.Documents.Upload;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace DistributedFileProcessor.Application.Extensions;

[ExcludeFromCodeCoverage(Justification = "Pure dependency injection configuration")]
public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUploadDocumentUseCase, UploadDocumentUseCase>();
        services.AddScoped<IProcessDocumentUseCase, ProcessDocumentUseCase>();
        services.AddScoped<IGetDocumentStatusUseCase, GetDocumentStatusUseCase>();

        return services;
    }
}