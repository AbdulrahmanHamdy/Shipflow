using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Security;
using ShipFlow.Domain.CargoAnalysis;

namespace ShipFlow.Application.Features.CargoAnalysis;

public sealed class AnalyzeCargoImagesHandler
{
    private const int MaxImageCount = 10;
    private const int MaxImageSizeBytes = 10 * 1024 * 1024; // 10MB
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp"
    };

    private readonly ICargoAnalysisService _analysisService;
    private readonly ICargoCalculationService _calculationService;
    private readonly IVehicleRecommendationService _vehicleService;
    private readonly ICargoAnalysisStore _cargoStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public AnalyzeCargoImagesHandler(
        ICargoAnalysisService analysisService,
        ICargoCalculationService calculationService,
        IVehicleRecommendationService vehicleService,
        ICargoAnalysisStore cargoStore,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _analysisService = analysisService;
        _calculationService = calculationService;
        _vehicleService = vehicleService;
        _cargoStore = cargoStore;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<CargoAnalysisResponseDto> HandleAsync(
        IReadOnlyList<CargoImageUpload> images,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(images);

        if (images.Count == 0)
        {
            throw new ValidationFailedException("At least one cargo image is required for analysis.");
        }

        if (images.Count > MaxImageCount)
        {
            throw new ValidationFailedException($"Maximum of {MaxImageCount} images can be analyzed at once.");
        }

        foreach (var image in images)
        {
            if (image.Bytes == null || image.Bytes.Length == 0)
            {
                throw new ValidationFailedException("An uploaded image is empty.");
            }

            if (image.Bytes.Length > MaxImageSizeBytes)
            {
                throw new ValidationFailedException($"Image '{image.FileName}' exceeds the maximum allowed size of 10MB.");
            }

            if (!AllowedContentTypes.Contains(image.ContentType))
            {
                throw new ValidationFailedException($"Image '{image.FileName}' has unsupported format '{image.ContentType}'. Supported formats: JPEG, PNG, WebP.");
            }
        }

        // 1. Analyze via AI service
        var estimatedItems = await _analysisService.AnalyzeImagesAsync(images, cancellationToken);
        if (estimatedItems.Count == 0)
        {
            throw new ValidationFailedException("No furniture or cargo items could be identified in the uploaded photos. Please try taking photos with better lighting or clearer angles.");
        }

        // 2. Calculate totals
        var calculation = _calculationService.Calculate(estimatedItems);

        // 3. Map to domain items and create session
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var customerId = _currentUser.IsAuthenticated && _currentUser.UserId.HasValue && _currentUser.UserId.Value != Guid.Empty
            ? _currentUser.UserId.Value
            : Guid.NewGuid(); // Fallback for unauthenticated preview if applicable

        var sessionId = Guid.NewGuid();
        var domainItems = estimatedItems.Select(e => new CargoItem(
            id: Guid.NewGuid(),
            sessionId: sessionId,
            name: e.Name,
            category: e.Category,
            quantity: e.Quantity,
            lengthCm: e.LengthCm,
            widthCm: e.WidthCm,
            heightCm: e.HeightCm,
            weightKg: e.WeightKg,
            originalVolumeM3: e.OriginalVolumeM3,
            canBeDisassembled: e.CanBeDisassembled,
            disassemblyNotes: e.DisassemblyNotes,
            packedLengthCm: e.PackedLengthCm,
            packedWidthCm: e.PackedWidthCm,
            packedHeightCm: e.PackedHeightCm,
            packedWeightKg: e.PackedWeightKg,
            packedVolumeM3: e.PackedVolumeM3,
            packingComplexity: e.PackingComplexity,
            fragility: e.Fragility,
            specialHandling: e.SpecialHandling,
            confidenceScore: e.ConfidenceScore,
            estimationNotes: e.EstimationNotes
        )).ToList();

        var session = CargoAnalysisSession.Create(customerId, domainItems, nowUtc);
        await _cargoStore.AddAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 4. Recommend vehicles based on estimated items
        var itemInputs = estimatedItems.Select(e => new UpdateCargoItemInputDto(
            Id: null,
            Name: e.Name,
            Category: e.Category,
            Quantity: e.Quantity,
            LengthCm: e.LengthCm,
            WidthCm: e.WidthCm,
            HeightCm: e.HeightCm,
            WeightKg: e.WeightKg,
            CanBeDisassembled: e.CanBeDisassembled,
            DisassemblyNotes: e.DisassemblyNotes,
            PackedLengthCm: e.PackedLengthCm,
            PackedWidthCm: e.PackedWidthCm,
            PackedHeightCm: e.PackedHeightCm,
            PackedWeightKg: e.PackedWeightKg,
            PackingComplexity: e.PackingComplexity,
            Fragility: e.Fragility,
            SpecialHandling: e.SpecialHandling,
            ConfidenceScore: e.ConfidenceScore,
            EstimationNotes: e.EstimationNotes
        )).ToList();

        var vehicleRecommendations = await _vehicleService.RecommendVehiclesAsync(
            calculation,
            itemInputs,
            cancellationToken);

        var responseItems = session.Items.Select(i => new CargoItemDto(
            i.Id,
            i.Name,
            i.Category,
            i.Quantity,
            i.LengthCm,
            i.WidthCm,
            i.HeightCm,
            i.WeightKg,
            i.OriginalVolumeM3,
            i.CanBeDisassembled,
            i.DisassemblyNotes,
            i.PackedLengthCm,
            i.PackedWidthCm,
            i.PackedHeightCm,
            i.PackedWeightKg,
            i.PackedVolumeM3,
            i.PackingComplexity,
            i.Fragility,
            i.SpecialHandling,
            i.ConfidenceScore,
            i.EstimationNotes
        )).ToList();

        return new CargoAnalysisResponseDto(
            session.Id,
            session.Status,
            session.CreatedAtUtc,
            calculation,
            responseItems,
            vehicleRecommendations);
    }
}
