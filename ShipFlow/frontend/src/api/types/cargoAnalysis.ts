export type CargoCategory =
  | 'Bed'
  | 'Mattress'
  | 'Wardrobe'
  | 'Sofa'
  | 'Chair'
  | 'Table'
  | 'Desk'
  | 'Refrigerator'
  | 'WashingMachine'
  | 'Television'
  | 'Cabinet'
  | 'Box'
  | 'Appliance'
  | 'Electronics'
  | 'Other';

export type PackingComplexity = 'Low' | 'Medium' | 'High';
export type FragilityLevel = 'Low' | 'Medium' | 'High';
export type CargoAnalysisStatus = 'Analyzed' | 'Confirmed';

export interface CargoItemDto {
  id: string;
  name: string;
  category: CargoCategory;
  quantity: number;
  lengthCm: number;
  widthCm: number;
  heightCm: number;
  weightKg: number;
  originalVolumeM3: number;
  canBeDisassembled: boolean;
  disassemblyNotes?: string | null;
  packedLengthCm: number;
  packedWidthCm: number;
  packedHeightCm: number;
  packedWeightKg: number;
  packedVolumeM3: number;
  packingComplexity: PackingComplexity;
  fragility: FragilityLevel;
  specialHandling?: string | null;
  confidenceScore: number;
  estimationNotes?: string | null;
}

export interface CargoCalculationResult {
  totalWeightKg: number;
  totalOriginalVolumeM3: number;
  totalPackedVolumeM3: number;
  maxLengthCm: number;
  maxWidthCm: number;
  maxHeightCm: number;
  volumeSavingsPercentage: number;
  packingAllowanceM3: number;
  totalItemsCount: number;
  hasFragileItems: boolean;
  specialHandlingRequirements: string[];
}

export interface VehicleRecommendationDto {
  vehicleId: string;
  plateNumber: string;
  type: 'Van' | 'Pickup' | 'Truck' | 'Trailer';
  maxWeightKg: number;
  maxVolumeM3: number;
  weightUtilizationPercentage: number;
  volumeUtilizationPercentage: number;
  isSuitable: boolean;
  suitabilityRank: 'BestMatch' | 'Suitable' | 'Alternative' | 'TooSmall' | 'Overweight' | 'DimensionMismatch';
  reason: string;
  warnings: string[];
}

export interface VehicleRecommendationResultDto {
  cargoSummary: CargoCalculationResult;
  bestMatch?: VehicleRecommendationDto | null;
  recommendations: VehicleRecommendationDto[];
}

export interface CargoAnalysisResponseDto {
  sessionId: string;
  status: CargoAnalysisStatus;
  createdAtUtc: string;
  calculation: CargoCalculationResult;
  items: CargoItemDto[];
  vehicleRecommendations?: VehicleRecommendationResultDto | null;
}

export interface UpdateCargoItemInputDto {
  id?: string;
  name: string;
  category: CargoCategory;
  quantity: number;
  lengthCm: number;
  widthCm: number;
  heightCm: number;
  weightKg: number;
  canBeDisassembled: boolean;
  disassemblyNotes?: string | null;
  packedLengthCm: number;
  packedWidthCm: number;
  packedHeightCm: number;
  packedWeightKg: number;
  packingComplexity: PackingComplexity;
  fragility: FragilityLevel;
  specialHandling?: string | null;
  confidenceScore?: number;
  estimationNotes?: string | null;
}
