export type VehicleType = 'Van' | 'Pickup' | 'Truck' | 'Trailer';

export interface DriverDto {
  id: string;
  userId: string;
  companyId?: string;
  fullName: string;
  licenseNumber: string;
  isActive: boolean;
  createdAtUtc?: string;
}

export interface VehicleDto {
  id: string;
  companyId?: string;
  plateNumber: string;
  type: VehicleType | string;
  maxWeightKg: number;
  maxVolumeM3: number;
  isActive: boolean;
  createdAtUtc?: string;
}

export interface RegisterDriverCommand {
  userId: string;
  fullName: string;
  licenseNumber: string;
}

export interface UpdateDriverCommand {
  fullName: string;
  licenseNumber: string;
}

export interface RegisterVehicleCommand {
  plateNumber: string;
  type: VehicleType;
  maxWeightKg: number;
  maxVolumeM3: number;
}

export interface UpdateVehicleCapacityCommand {
  maxWeightKg: number;
  maxVolumeM3: number;
}

export interface ListFleetQuery {
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}
