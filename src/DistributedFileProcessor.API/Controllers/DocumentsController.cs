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
    private const int MaxFileSizeInBytes = 100 * 1024 * 1024;

    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSizeInBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSizeInBytes)]
    [ProducesResponseType<DocumentAcceptedResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadDocument(IUploadDocumentUseCase useCase, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "File is missing or empty." });
        }

        LogUploadRequestReceived(logger, file.FileName, file.Length);
        await using Stream stream = file.OpenReadStream();

        UploadDocumentRequest request = new(file.FileName, stream);
        UploadDocumentResponse appResponse = await useCase.ExecuteAsync(request, cancellationToken);
        DocumentAcceptedResponse apiResponse = new("Document accepted for processing.", appResponse.JobId, appResponse.S3ObjectKey, appResponse.Status);

        return AcceptedAtAction(nameof(GetDocumentStatus), new { jobId = apiResponse.JobId }, apiResponse);
    }

    [HttpGet("{jobId:guid}")]
    [ProducesResponseType(typeof(DocumentStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentStatus(IGetDocumentStatusUseCase useCase, Guid jobId, CancellationToken cancellationToken)
    {
        DocumentStatusResponse? response = await useCase.ExecuteAsync(jobId, cancellationToken);

        if (response is null)
        {
            return NotFound(new { error = $"O documento com o ID {jobId} não foi encontrado." });
        }

        return Ok(response);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Received upload request for file {FileName} with size {FileSize} bytes.")]
    static partial void LogUploadRequestReceived(ILogger<DocumentsController> logger, string fileName, long fileSize);
}