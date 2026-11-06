using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Features.CargoAnalysis;
using ShipFlow.Domain.CargoAnalysis;
using ShipFlow.Domain.Fleet;
using ShipFlow.Infrastructure.CargoAnalysis;
using Xunit;

namespace ShipFlow.Application.Tests.CargoAnalysis;

public sealed class CargoCalculationServiceTests
{
    private readonly CargoCalculationService _calculator = new();

    [Fact]
    public void Calculate_ComputesAccurateTotals_AndDisassemblySavings()
    {
        var items = new List<CargoItemEstimateDto>
        {
            new(
                Name: "King Bed",
                Category: CargoCategory.Bed,
                Quantity: 1,
                LengthCm: 200m,
                WidthCm: 180m,
                HeightCm: 100m,
                WeightKg: 75m,
                OriginalVolumeM3: 3.6m,
                CanBeDisassembled: true,
                DisassemblyNotes: "Frame detaches into flat rails and headboard",
                PackedLengthCm: 200m,
                PackedWidthCm: 40m,
                PackedHeightCm: 30m,
                PackedWeightKg: 75m,
                PackedVolumeM3: 0.24m,
                PackingComplexity: PackingComplexity.Medium,
                Fragility: FragilityLevel.Low,
                SpecialHandling: "Wrap headboard",
                ConfidenceScore: 0.95m,
                EstimationNotes: "Standard wooden king bed"
            ),
            new(
                Name: "Glass Coffee Table",
                Category: CargoCategory.Table,
                Quantity: 1,
                LengthCm: 120m,
                WidthCm: 60m,
                HeightCm: 45m,
                WeightKg: 20m,
                OriginalVolumeM3: 0.324m,
                CanBeDisassembled: false,
                DisassemblyNotes: null,
                PackedLengthCm: 120m,
                PackedWidthCm: 60m,
                PackedHeightCm: 45m,
                PackedWeightKg: 20m,
                PackedVolumeM3: 0.324m,
                PackingComplexity: PackingComplexity.High,
                Fragility: FragilityLevel.High,
                SpecialHandling: "Fragile tempered glass, use crate",
                ConfidenceScore: 0.90m,
                EstimationNotes: "Glass top"
            )
        };

        var result = _calculator.Calculate(items);

        Assert.Equal(95.0m, result.TotalWeightKg);
        Assert.Equal(3.924m, result.TotalOriginalVolumeM3);
        Assert.Equal(0.564m, result.TotalPackedVolumeM3);
        Assert.True(result.VolumeSavingsPercentage > 80m);
        Assert.True(result.HasFragileItems);
        Assert.Equal(2, result.SpecialHandlingRequirements.Count);
    }
}

public sealed class GeminiResponseParsingTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_response);
    }

    [Fact]
    public async Task AnalyzeImagesAsync_ParsesStructuredGeminiJson_Successfully()
    {
        var geminiRawJson = @"{
  ""candidates"": [
    {
      ""content"": {
        ""parts"": [
          {
            ""text"": ""```json\n[\n  {\n    \""name\"": \""Large 3-Door Wardrobe\"",\n    \""category\"": \""Wardrobe\"",\n    \""quantity\"": 1,\n    \""lengthCm\"": 180,\n    \""widthCm\"": 60,\n    \""heightCm\"": 200,\n    \""weightKg\"": 110,\n    \""originalVolumeM3\"": 2.16,\n    \""canBeDisassembled\"": true,\n    \""disassemblyNotes\"": \""Modular panels, doors and shelves detachable.\"",\n    \""packedLengthCm\"": 200,\n    \""packedWidthCm\"": 60,\n    \""packedHeightCm\"": 35,\n    \""packedWeightKg\"": 110,\n    \""packedVolumeM3\"": 0.42,\n    \""packingComplexity\"": \""High\"",\n    \""fragility\"": \""Medium\"",\n    \""specialHandling\"": \""Keep mirrors cushioned.\"",\n    \""confidenceScore\"": 0.92,\n    \""estimationNotes\"": \""Wood finish modular wardrobe.\""\n  }\n]\n```""
          }
        ]
      }
    }
  ]
}";

        var httpResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(geminiRawJson, System.Text.Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new MockHttpMessageHandler(httpResponse));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "fake-api-key-for-test",
                ["Gemini:Model"] = "gemini-2.5-flash"
            })
            .Build();

        var options = Options.Create(new GeminiOptions { ApiKey = "fake-api-key-for-test" });
        var service = new GeminiCargoAnalysisService(httpClient, config, options, NullLogger<GeminiCargoAnalysisService>.Instance);

        var testImages = new List<CargoImageUpload>
        {
            new("image/jpeg", [1, 2, 3, 4], "wardrobe.jpg")
        };

        var items = await service.AnalyzeImagesAsync(testImages, CancellationToken.None);

        Assert.Single(items);
        var wardrobe = items[0];
        Assert.Equal("Large 3-Door Wardrobe", wardrobe.Name);
        Assert.Equal(CargoCategory.Wardrobe, wardrobe.Category);
        Assert.True(wardrobe.CanBeDisassembled);
        Assert.Equal(180m, wardrobe.LengthCm);
        Assert.Equal(200m, wardrobe.PackedLengthCm);
        Assert.Equal(0.42m, wardrobe.PackedVolumeM3);
        Assert.Equal(110m, wardrobe.WeightKg);
    }

    [Fact]
    public async Task AnalyzeImagesAsync_ThrowsWhenApiKeyMissing()
    {
        var httpClient = new HttpClient(new MockHttpMessageHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));
        var config = new ConfigurationBuilder().Build();
        var options = Options.Create(new GeminiOptions { ApiKey = "" });
        var service = new GeminiCargoAnalysisService(httpClient, config, options, NullLogger<GeminiCargoAnalysisService>.Instance);

        var testImages = new List<CargoImageUpload>
        {
            new("image/jpeg", [1, 2, 3], "test.jpg")
        };

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            service.AnalyzeImagesAsync(testImages, CancellationToken.None));
    }
}
