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
