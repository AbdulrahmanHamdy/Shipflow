using ShipFlow.Application.Features.CargoAnalysis;
using ShipFlow.Domain.CargoAnalysis;

namespace ShipFlow.Application.Common.Interfaces;

public interface ICargoAnalysisService
{
    Task<IReadOnlyList<CargoItemEstimateDto>> AnalyzeImagesAsync(
        IReadOnlyList<CargoImageUpload> images,
        CancellationToken cancellationToken);
}

public interface ICargoCalculationService
{
    CargoCalculationResult Calculate(IEnumerable<CargoItemEstimateDto> items);
    CargoCalculationResult Calculate(IEnumerable<UpdateCargoItemInputDto> items);
    CargoCalculationResult Calculate(IEnumerable<CargoItem> items);
}

public interface IVehicleRecommendationService
{
    Task<VehicleRecommendationResultDto> RecommendVehiclesAsync(
        CargoCalculationResult calculation,
        IReadOnlyList<UpdateCargoItemInputDto> items,
        CancellationToken cancellationToken);
}
