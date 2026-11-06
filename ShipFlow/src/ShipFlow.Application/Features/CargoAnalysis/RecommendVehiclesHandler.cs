using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;

namespace ShipFlow.Application.Features.CargoAnalysis;

public sealed class RecommendVehiclesHandler
{
    private readonly ICargoCalculationService _calculationService;
    private readonly IVehicleRecommendationService _vehicleService;

    public RecommendVehiclesHandler(
        ICargoCalculationService calculationService,
        IVehicleRecommendationService vehicleService)
    {
        _calculationService = calculationService;
        _vehicleService = vehicleService;
    }

    public async Task<VehicleRecommendationResultDto> HandleAsync(
        RecommendVehiclesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Items == null || command.Items.Count == 0)
        {
            throw new ValidationFailedException("At least one cargo item is required for vehicle recommendation.");
        }

        var calculation = _calculationService.Calculate(command.Items);

        return await _vehicleService.RecommendVehiclesAsync(
            calculation,
            command.Items,
            cancellationToken);
    }
}
