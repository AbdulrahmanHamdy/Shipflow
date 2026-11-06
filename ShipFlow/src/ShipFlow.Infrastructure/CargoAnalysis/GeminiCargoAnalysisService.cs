using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShipFlow.Application.Common.Exceptions;
using ShipFlow.Application.Common.Interfaces;
using ShipFlow.Application.Features.CargoAnalysis;
using ShipFlow.Domain.CargoAnalysis;

namespace ShipFlow.Infrastructure.CargoAnalysis;

public sealed class GeminiCargoAnalysisService : ICargoAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IOptions<GeminiOptions> _options;
    private readonly ILogger<GeminiCargoAnalysisService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public GeminiCargoAnalysisService(
        HttpClient httpClient,
        IConfiguration configuration,
        IOptions<GeminiOptions> options,
        ILogger<GeminiCargoAnalysisService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CargoItemEstimateDto>> AnalyzeImagesAsync(
        IReadOnlyList<CargoImageUpload> images,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(images);

        var apiKey = _configuration["Gemini:ApiKey"] ?? _options.Value.ApiKey;
        var model = _configuration["Gemini:Model"] ?? _options.Value.Model;
        if (string.IsNullOrWhiteSpace(model))
        {
            model = "gemini-2.5-flash";
        }

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Gemini API key is not configured or contains placeholder value.");
            throw new ValidationFailedException(
                "Gemini AI Cargo Estimation is not configured on the server. Please ensure Gemini:ApiKey is set in appsettings.json.");
        }

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var systemPrompt = @"
You are ShipFlow AI, an expert computer vision logistics and cargo dimension estimator.
Analyze the provided photograph(s) containing household or commercial furniture, appliances, and cargo items.

Identify each distinct piece of furniture or cargo item visible in the scene.
For each item, provide realistic estimates in metric units:
- Dimensions in centimeters (cm): Length, Width, Height
- Weight in kilograms (kg)
- Original Volume in cubic meters (m3) = (Length * Width * Height) / 1,000,000

DISASSEMBLY INTELLIGENCE:
Determine if the item can realistically and safely be disassembled before moving:
- Beds: Bed frames usually have detachable headboards, side rails, and legs. Mattresses do not disassemble.
- Wardrobes: Many modular wardrobes can have doors, shelves, and panels detached. Solid antique wardrobes cannot.
- Sofas: Cushions and modular L-sections often detach. Mention if feet/legs can unscrew.
- Dining / Office Tables: Legs are frequently detachable from the tabletop.
- Desks / Cabinets: Check if drawers or partitions remove.
- Appliances & Boxes: Cannot be disassembled.

If disassembled:
- Provide packed dimensions in centimeters (PackedLengthCm, PackedWidthCm, PackedHeightCm) representing the compact transport state.
- Provide packed volume in cubic meters (PackedVolumeM3).
- Provide packing complexity: Low, Medium, or High.
- Provide fragility: Low, Medium, or High.
- Provide special handling instructions (e.g. 'Use moving blankets', 'Keep upright', 'Requires 2 persons to lift').
- Provide confidence score (0.0 to 1.0) based on visual clarity.
- Provide concise estimation notes explaining visual cues used.

Categorize items into one of the following exact categories:
Bed, Mattress, Wardrobe, Sofa, Chair, Table, Desk, Refrigerator, WashingMachine, Television, Cabinet, Box, Appliance, Electronics, Other.

Return a JSON array of objects with the following schema:
[
  {
    ""name"": ""3-Seater Fabric Sofa"",
    ""category"": ""Sofa"",
    ""quantity"": 1,
    ""lengthCm"": 210,
    ""widthCm"": 90,
    ""heightCm"": 85,
    ""weightKg"": 65,
    ""originalVolumeM3"": 1.606,
    ""canBeDisassembled"": true,
    ""disassemblyNotes"": ""Removable back and seat cushions; wooden feet unscrew."",
    ""packedLengthCm"": 210,
    ""packedWidthCm"": 85,
    ""packedHeightCm"": 70,
    ""packedWeightKg"": 65,
    ""packedVolumeM3"": 1.25,
    ""packingComplexity"": ""Low"",
    ""fragility"": ""Medium"",
    ""specialHandling"": ""Wrap with protective furniture shrink wrap and moving pads."",
    ""confidenceScore"": 0.90,
    ""estimationNotes"": ""Standard 3-cushion contemporary fabric sofa with removable cushions.""
  }
]
";

        var parts = new List<object>
        {
            new { text = systemPrompt }
        };

        foreach (var img in images)
        {
            parts.Add(new
            {
                inlineData = new
                {
                    mimeType = img.ContentType,
                    data = Convert.ToBase64String(img.Bytes)
                }
            });
        }

        var requestBody = new
        {
            contents = new[]
            {
                new { parts }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.2
            }
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to connect to Gemini API endpoint.");
            throw new ValidationFailedException(
                "Unable to connect to Google Gemini Vision AI service. Please check your internet connection or server network.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Gemini API call failed with status code {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);

            if ((int)response.StatusCode == 429)
            {
                throw new ValidationFailedException(
                    "Gemini AI rate limit reached. Please wait a moment before trying again.");
            }

            if ((int)response.StatusCode == 400 || (int)response.StatusCode == 403)
            {
                throw new ValidationFailedException(
                    $"Gemini Vision API rejected the request ({response.StatusCode}). Please verify your Gemini API key and model permissions.");
            }

            throw new ValidationFailedException(
                $"Gemini AI service error ({response.StatusCode}). Please try again.");
        }

        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseGeminiResponse(jsonResponse);
    }

    private List<CargoItemEstimateDto> ParseGeminiResponse(string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("Gemini returned no candidates in response: {Json}", jsonResponse);
                return [];
            }

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.GetArrayLength() == 0)
            {
                return [];
            }

            var textPart = parts[0].GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(textPart))
            {
                return [];
            }

            // Clean markdown code blocks if Gemini enclosed the JSON in ```json ... ```
            var cleanedJson = textPart.Trim();
            if (cleanedJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                cleanedJson = cleanedJson.Substring(7);
            }
            else if (cleanedJson.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                cleanedJson = cleanedJson.Substring(3);
            }

            if (cleanedJson.EndsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                cleanedJson = cleanedJson.Substring(0, cleanedJson.Length - 3);
            }
            cleanedJson = cleanedJson.Trim();

            var rawItems = JsonSerializer.Deserialize<List<GeminiRawItem>>(cleanedJson, JsonOpts);
            if (rawItems == null || rawItems.Count == 0)
            {
                return [];
            }

            var result = new List<CargoItemEstimateDto>();
            foreach (var r in rawItems)
            {
                if (string.IsNullOrWhiteSpace(r.Name))
                {
                    continue;
                }

                var category = ParseCategory(r.Category);
                var length = Math.Max(10m, Math.Round(r.LengthCm, 1));
                var width = Math.Max(10m, Math.Round(r.WidthCm, 1));
                var height = Math.Max(10m, Math.Round(r.HeightCm, 1));
                var weight = Math.Max(1m, Math.Round(r.WeightKg, 1));
                var origVol = r.OriginalVolumeM3 > 0
                    ? Math.Round(r.OriginalVolumeM3, 4)
                    : Math.Round((length * width * height) / 1_000_000m, 4);

                var packedL = r.PackedLengthCm > 0 ? Math.Round(r.PackedLengthCm, 1) : length;
                var packedW = r.PackedWidthCm > 0 ? Math.Round(r.PackedWidthCm, 1) : width;
                var packedH = r.PackedHeightCm > 0 ? Math.Round(r.PackedHeightCm, 1) : height;
                var packedWt = r.PackedWeightKg > 0 ? Math.Round(r.PackedWeightKg, 1) : weight;
                var packedVol = r.PackedVolumeM3 > 0
                    ? Math.Round(r.PackedVolumeM3, 4)
                    : Math.Round((packedL * packedW * packedH) / 1_000_000m, 4);

                var complexity = ParseComplexity(r.PackingComplexity);
                var fragility = ParseFragility(r.Fragility);
                var confidence = r.ConfidenceScore > 0 && r.ConfidenceScore <= 1.0m
                    ? Math.Round(r.ConfidenceScore, 2)
                    : 0.85m;

                result.Add(new CargoItemEstimateDto(
                    Name: r.Name.Trim(),
                    Category: category,
                    Quantity: Math.Max(1, r.Quantity),
                    LengthCm: length,
                    WidthCm: width,
                    HeightCm: height,
                    WeightKg: weight,
                    OriginalVolumeM3: origVol,
                    CanBeDisassembled: r.CanBeDisassembled,
                    DisassemblyNotes: r.DisassemblyNotes,
                    PackedLengthCm: packedL,
                    PackedWidthCm: packedW,
                    PackedHeightCm: packedH,
                    PackedWeightKg: packedWt,
                    PackedVolumeM3: packedVol,
                    PackingComplexity: complexity,
                    Fragility: fragility,
                    SpecialHandling: r.SpecialHandling,
                    ConfidenceScore: confidence,
                    EstimationNotes: r.EstimationNotes
                ));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Gemini response JSON: {Json}", jsonResponse);
            throw new ValidationFailedException(
                "Gemini AI generated an unparseable response. Please try with clearer photos of your items.");
        }
    }

    private static CargoCategory ParseCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return CargoCategory.Other;
        if (Enum.TryParse<CargoCategory>(raw.Trim(), true, out var cat)) return cat;

        var lower = raw.ToLowerInvariant();
        if (lower.Contains("bed")) return CargoCategory.Bed;
        if (lower.Contains("mattress")) return CargoCategory.Mattress;
        if (lower.Contains("wardrobe") || lower.Contains("closet") || lower.Contains("armoire")) return CargoCategory.Wardrobe;
        if (lower.Contains("sofa") || lower.Contains("couch")) return CargoCategory.Sofa;
        if (lower.Contains("chair") || lower.Contains("stool")) return CargoCategory.Chair;
        if (lower.Contains("table")) return CargoCategory.Table;
        if (lower.Contains("desk")) return CargoCategory.Desk;
        if (lower.Contains("refrigerator") || lower.Contains("fridge")) return CargoCategory.Refrigerator;
        if (lower.Contains("washer") || lower.Contains("washing")) return CargoCategory.WashingMachine;
        if (lower.Contains("tv") || lower.Contains("television")) return CargoCategory.Television;
        if (lower.Contains("cabinet") || lower.Contains("shelf") || lower.Contains("bookcase")) return CargoCategory.Cabinet;
        if (lower.Contains("box") || lower.Contains("crate")) return CargoCategory.Box;
        if (lower.Contains("appliance")) return CargoCategory.Appliance;
        if (lower.Contains("electronic")) return CargoCategory.Electronics;

        return CargoCategory.Other;
    }

    private static PackingComplexity ParseComplexity(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return PackingComplexity.Medium;
        if (Enum.TryParse<PackingComplexity>(raw.Trim(), true, out var c)) return c;
        return PackingComplexity.Medium;
    }

    private static FragilityLevel ParseFragility(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return FragilityLevel.Low;
        if (Enum.TryParse<FragilityLevel>(raw.Trim(), true, out var f)) return f;
        return FragilityLevel.Low;
    }

    private sealed class GeminiRawItem
    {
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal LengthCm { get; set; }
        public decimal WidthCm { get; set; }
        public decimal HeightCm { get; set; }
        public decimal WeightKg { get; set; }
        public decimal OriginalVolumeM3 { get; set; }
        public bool CanBeDisassembled { get; set; }
        public string? DisassemblyNotes { get; set; }
        public decimal PackedLengthCm { get; set; }
        public decimal PackedWidthCm { get; set; }
        public decimal PackedHeightCm { get; set; }
        public decimal PackedWeightKg { get; set; }
        public decimal PackedVolumeM3 { get; set; }
        public string? PackingComplexity { get; set; }
        public string? Fragility { get; set; }
        public string? SpecialHandling { get; set; }
        public decimal ConfidenceScore { get; set; }
        public string? EstimationNotes { get; set; }
    }
}
