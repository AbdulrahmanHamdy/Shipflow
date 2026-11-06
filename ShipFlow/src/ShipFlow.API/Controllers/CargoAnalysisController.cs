using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipFlow.Application.Features.CargoAnalysis;

namespace ShipFlow.API.Controllers;

[ApiController]
[Route("api/cargo-analysis")]
[Produces("application/json")]
public sealed class CargoAnalysisController : ControllerBase
{
    /// <summary>
    /// Analyzes uploaded furniture/cargo photos using Google Gemini Vision AI.
    /// Returns detected inventory items, estimated dimensions, weight, disassembly suggestions, and vehicle recommendations.
    /// </summary>
    [HttpPost("analyze")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(CargoAnalysisResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CargoAnalysisResponseDto>> Analyze(
        [FromForm] List<IFormFile> images,
        [FromServices] AnalyzeCargoImagesHandler handler,
        CancellationToken ct)
    {
        if (images == null || images.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = "At least one photo must be uploaded for cargo analysis.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var imageUploads = new List<CargoImageUpload>();
        foreach (var file in images)
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, ct);
            imageUploads.Add(new CargoImageUpload(
                ContentType: file.ContentType,
                Bytes: stream.ToArray(),
                FileName: file.FileName
            ));
        }

        var result = await handler.HandleAsync(imageUploads, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a previously analyzed cargo estimation session.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CargoAnalysisResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CargoAnalysisResponseDto>> GetById(
        Guid id,
        [FromServices] GetCargoAnalysisByIdHandler handler,
        CancellationToken ct) =>
        Ok(await handler.HandleAsync(id, ct));

    /// <summary>
    /// Updates the cargo items in an analysis session (customer edits, manual dimension adjustments, additions, deletions).
    /// </summary>
    [HttpPut("{id:guid}/items")]
    [ProducesResponseType(typeof(CargoAnalysisResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CargoAnalysisResponseDto>> UpdateItems(
        Guid id,
        [FromBody] UpdateCargoAnalysisItemsCommand command,
        [FromServices] UpdateCargoAnalysisItemsHandler handler,
        CancellationToken ct) =>
        Ok(await handler.HandleAsync(id, command, ct));

    /// <summary>
    /// Confirms the cargo inventory after the customer reviews and verifies items.
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(typeof(CargoAnalysisResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CargoAnalysisResponseDto>> Confirm(
        Guid id,
        [FromServices] ConfirmCargoAnalysisHandler handler,
        CancellationToken ct) =>
        Ok(await handler.HandleAsync(id, ct));

    /// <summary>
    /// Calculates smart vehicle recommendations from fleet vehicles based on cargo items.
    /// </summary>
    [HttpPost("recommend-vehicles")]
    [ProducesResponseType(typeof(VehicleRecommendationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VehicleRecommendationResultDto>> RecommendVehicles(
        [FromBody] RecommendVehiclesCommand command,
        [FromServices] RecommendVehiclesHandler handler,
        CancellationToken ct) =>
        Ok(await handler.HandleAsync(command, ct));
}
