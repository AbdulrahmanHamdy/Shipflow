using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Security;
using ShipFlow.Domain.CargoAnalysis;

namespace ShipFlow.Application.Features.CargoAnalysis;

public sealed class UpdateCargoAnalysisItemsHandler
{
    private readonly ICargoAnalysisStore _cargoStore;
    private readonly ICargoCalculationService _calculationService;
    private readonly IVehicleRecommendationService _vehicleService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public UpdateCargoAnalysisItemsHandler(
        ICargoAnalysisStore cargoStore,
        ICargoCalculationService calculationService,
        IVehicleRecommendationService vehicleService,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _cargoStore = cargoStore;
        _calculationService = calculationService;
        _vehicleService = vehicleService;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<CargoAnalysisResponseDto> HandleAsync(
        Guid id,
        UpdateCargoAnalysisItemsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Items == null || command.Items.Count == 0)
        {
            throw new ValidationFailedException("At least one cargo item is required in the inventory.");
        }

        var session = await _cargoStore.FindForUpdateAsync(id, cancellationToken);
        if (session == null)
        {
            throw new NotFoundException("Cargo analysis session not found.");
        }

        if (_currentUser.IsAuthenticated &&
            !_currentUser.IsInRole(Roles.PlatformAdmin) &&
            session.CustomerId != _currentUser.UserId)
        {
            throw new ForbiddenException("You are not authorized to modify this cargo analysis.");
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var domainItems = command.Items.Select(item => new CargoItem(
            id: item.Id.HasValue && item.Id.Value != Guid.Empty ? item.Id.Value : Guid.NewGuid(),
            sessionId: session.Id,
            name: item.Name,
            category: item.Category,
            quantity: item.Quantity,
            lengthCm: item.LengthCm,
            widthCm: item.WidthCm,
            heightCm: item.HeightCm,
            weightKg: item.WeightKg,
            originalVolumeM3: Math.Round((item.LengthCm * item.WidthCm * item.HeightCm) / 1_000_000m, 4),
            canBeDisassembled: item.CanBeDisassembled,
            disassemblyNotes: item.DisassemblyNotes,
            packedLengthCm: item.PackedLengthCm,
            packedWidthCm: item.PackedWidthCm,
            packedHeightCm: item.PackedHeightCm,
            packedWeightKg: item.PackedWeightKg,
            packedVolumeM3: Math.Round((item.PackedLengthCm * item.PackedWidthCm * item.PackedHeightCm) / 1_000_000m, 4),
            packingComplexity: item.PackingComplexity,
            fragility: item.Fragility,
            specialHandling: item.SpecialHandling,
            confidenceScore: item.ConfidenceScore ?? 1.0m, // User-confirmed items have max confidence
            estimationNotes: item.EstimationNotes
        )).ToList();

        session.ReplaceItems(domainItems, nowUtc);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var calculation = _calculationService.Calculate(session.Items);

        var vehicleRecommendations = await _vehicleService.RecommendVehiclesAsync(
            calculation,
            command.Items,
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
