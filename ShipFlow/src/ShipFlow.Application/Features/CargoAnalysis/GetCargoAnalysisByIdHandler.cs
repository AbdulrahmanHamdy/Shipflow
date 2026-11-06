using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Common.Security;

namespace ShipFlow.Application.Features.CargoAnalysis;

public sealed class GetCargoAnalysisByIdHandler
{
    private readonly ICargoAnalysisStore _cargoStore;
    private readonly ICargoCalculationService _calculationService;
    private readonly IVehicleRecommendationService _vehicleService;
    private readonly ICurrentUser _currentUser;

    public GetCargoAnalysisByIdHandler(
        ICargoAnalysisStore cargoStore,
        ICargoCalculationService calculationService,
        IVehicleRecommendationService vehicleService,
        ICurrentUser currentUser)
    {
        _cargoStore = cargoStore;
        _calculationService = calculationService;
        _vehicleService = vehicleService;
        _currentUser = currentUser;
    }

    public async Task<CargoAnalysisResponseDto> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await _cargoStore.FindByIdAsync(id, cancellationToken);
        if (session == null)
        {
            throw new NotFoundException("Cargo analysis session not found.");
        }

        if (_currentUser.IsAuthenticated &&
            !_currentUser.IsInRole(Roles.PlatformAdmin) &&
            session.CustomerId != _currentUser.UserId)
        {
            throw new ForbiddenException("You are not authorized to view this cargo analysis.");
        }

        var calculation = _calculationService.Calculate(session.Items);

        var itemInputs = session.Items.Select(i => new UpdateCargoItemInputDto(
            Id: i.Id,
            Name: i.Name,
            Category: i.Category,
            Quantity: i.Quantity,
            LengthCm: i.LengthCm,
            WidthCm: i.WidthCm,
            HeightCm: i.HeightCm,
            WeightKg: i.WeightKg,
            CanBeDisassembled: i.CanBeDisassembled,
            DisassemblyNotes: i.DisassemblyNotes,
            PackedLengthCm: i.PackedLengthCm,
            PackedWidthCm: i.PackedWidthCm,
            PackedHeightCm: i.PackedHeightCm,
            PackedWeightKg: i.PackedWeightKg,
            PackingComplexity: i.PackingComplexity,
            Fragility: i.Fragility,
            SpecialHandling: i.SpecialHandling,
            ConfidenceScore: i.ConfidenceScore,
            EstimationNotes: i.EstimationNotes
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
