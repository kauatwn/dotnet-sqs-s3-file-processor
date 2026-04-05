using DistributedFileProcessor.API.Contracts.Responses;
using DistributedFileProcessor.Application.DTOs.Requests;
using DistributedFileProcessor.Application.DTOs.Responses;
using DistributedFileProcessor.Application.UseCases.Documents.GetStatus;
using DistributedFileProcessor.Application.UseCases.Documents.Upload;
using Microsoft.AspNetCore.Mvc;

namespace DistributedFileProcessor.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed partial class DocumentsController(ILogger<DocumentsController> logger) : ControllerBase
{
    [HttpPost("upload-url")]
    [ProducesResponseType<DocumentUploadUrlResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestUploadUrl(IUploadDocumentUseCase useCase, UploadDocumentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return BadRequest(new { error = "FileName is required." });
        }

        LogUploadUrlRequestReceived(logger, request.FileName);
        UploadDocumentResponse appResponse = await useCase.ExecuteAsync(request, cancellationToken);

        DocumentUploadUrlResponse apiResponse = new(
            "Upload URL generated successfully. Please PUT your file to the provided URL.",
            appResponse.JobId,
            appResponse.S3ObjectKey,
            appResponse.PreSignedUrl,
            appResponse.Status.ToString());

        return CreatedAtAction(nameof(GetDocumentStatus), new { jobId = apiResponse.JobId }, apiResponse);
    }

    [HttpGet("{jobId:guid}")]
    [ProducesResponseType<DocumentStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentStatus(IGetDocumentStatusUseCase useCase, Guid jobId, CancellationToken cancellationToken)
    {
        DocumentStatusResponse? response = await useCase.ExecuteAsync(jobId, cancellationToken);

        if (response is null)
        {
            LogDocumentStatusNotFound(logger, jobId);
            return NotFound(new { error = $"Document with ID {jobId} was not found." });
        }

        return Ok(response);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Received request to generate upload URL for file: {FileName}.")]
    static partial void LogUploadUrlRequestReceived(ILogger logger, string fileName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Document status requested but not found for JobId: {JobId}.")]
    static partial void LogDocumentStatusNotFound(ILogger logger, Guid jobId);
}