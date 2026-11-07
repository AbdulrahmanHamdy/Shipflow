export interface AssignmentDto {
  id: string;
  shipmentId: string;
  companyId: string;
  driverId: string;
  vehicleId: string;
  windowStartUtc: string;
  windowEndUtc: string;
  status: string;
  shipmentStatus: string;
  assignedByUserId: string;
  createdAtUtc: string;
  endedAtUtc: string | null;
  releaseReason: string | null;
}

export interface AvailabilityQuery {
  windowStartUtc: string;
  windowEndUtc: string;
}

export interface AssignShipmentCommand {
  driverId: string;
  vehicleId: string;
  windowStartUtc: string;
  windowEndUtc: string;
}

export interface UnassignShipmentCommand {
  reason: string;
}

export interface EligibleDriverDto {
  driverId: string;
  userId: string;
  fullName: string;
  licenseNumber: string;
  isAvailable: boolean;
  vehicleId: string | null;
  vehiclePlate: string | null;
  vehicleType: string | null;
  vehicleMaxWeightKg: number | null;
  vehicleMaxVolumeM3: number | null;
  canCarryDimensions: boolean;
  eligibilityStatus: string;
}

export interface MatchingStatusDto {
  shipmentId: string;
  reference: string | null;
  status: string;
  pickupAddress: string;
  pickupWindowStartUtc: string | null;
  pickupWindowEndUtc: string | null;
  deliveryAddress: string;
  deliveryWindowStartUtc: string | null;
  deliveryWindowEndUtc: string | null;
  cargoDescription: string;
  cargoWeightKg: number;
  cargoVolumeM3: number;
  requiredVehicleType: string;
  eligibleDriversCount: number;
  notifiedDriversCount: number;
  eligibleDrivers: EligibleDriverDto[];
  assignedDriver: EligibleDriverDto | null;
  dispatchedAtUtc: string | null;
}

export interface AvailableShipmentDto {
  shipmentId: string;
  reference: string | null;
  status: string;
  pickupAddress: string;
  pickupWindowStartUtc: string | null;
  pickupWindowEndUtc: string | null;
  deliveryAddress: string;
  deliveryWindowStartUtc: string | null;
  deliveryWindowEndUtc: string | null;
  cargoDescription: string;
  cargoWeightKg: number;
  cargoVolumeM3: number;
  requiredVehicleType: string;
  estimatedDistanceKm: number | null;
  priceAmount: number | null;
  priceCurrency: string | null;
  createdAtUtc: string;
  carrierCompanyName: string;
}

export interface DriverProfileDto {
  driverId: string;
  userId: string;
  fullName: string;
  licenseNumber: string;
  companyId: string;
  companyName: string;
  assignedVehicleId: string | null;
  assignedVehiclePlate: string | null;
  assignedVehicleType: string | null;
  isAvailable: boolean;
  hasActiveAssignment: boolean;
}

export interface UpdateDriverAvailabilityCommand {
  isAvailable: boolean;
}

export interface AssignDriverVehicleCommand {
  vehicleId: string | null;
}
