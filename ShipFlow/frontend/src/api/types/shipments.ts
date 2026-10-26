import { QuoteDto } from './quotes';

export type ShipmentStatus =
  | 'Pending'
  | 'Quoting'
  | 'Accepted'
  | 'Assigned'
  | 'PickedUp'
  | 'InTransit'
  | 'DeliveryFailed'
  | 'Delivered'
  | 'Cancelled';

export type ShipmentScope = 'Mine' | 'OpenForQuotes';

export interface CargoDto {
  description: string;
  weightKg: number;
  volumeM3: number;
}

export interface StopDto {
  id: string;
  sequence: number;
  type: string;
  address: string;
  latitude: number;
  longitude: number;
  windowStartUtc: string | null;
  windowEndUtc: string | null;
}

export interface StopInput {
  address: string;
  latitude: number;
  longitude: number;
  windowStartUtc?: string | null;
  windowEndUtc?: string | null;
}

export interface CreateShipmentCommand {
  reference?: string | null;
  cargo: CargoDto;
  stops: StopInput[];
}

export interface ShipmentSummaryDto {
  id: string;
  reference: string | null;
  status: ShipmentStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
  cargoDescription: string;
  cargoWeightKg: number;
  cargoVolumeM3: number;
  pickupAddress: string;
  deliveryAddress: string;
  agreedAmount: number | null;
  agreedCurrency: string | null;
  carrierCompanyId: string | null;
}

export interface ShipmentDetailsDto {
  id: string;
  reference: string | null;
  status: ShipmentStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
  cargo: CargoDto;
  stops: StopDto[];
  agreedAmount: number | null;
  agreedCurrency: string | null;
  carrierCompanyId: string | null;
  quotes: QuoteDto[];
}

export interface GetShipmentsQuery {
  scope?: ShipmentScope;
  statuses?: ShipmentStatus[];
  createdFromUtc?: string;
  createdToUtc?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface CancelShipmentCommand {
  reason: string;
}
