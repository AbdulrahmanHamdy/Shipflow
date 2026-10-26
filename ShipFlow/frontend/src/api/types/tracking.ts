export interface TrackingPointDto {
  id: string;
  shipmentId: string;
  latitude: number;
  longitude: number;
  recordedAtUtc: string;
  speedKph: number | null;
}

export interface SubmitTrackingPointCommand {
  shipmentId: string;
  latitude: number;
  longitude: number;
  recordedAtUtc: string;
  speedKph?: number | null;
}

export interface TrackingHistoryQuery {
  fromUtc?: string;
  toUtc?: string;
  page?: number;
  pageSize?: number;
}

export interface DeliveryProofDto {
  id: string;
  shipmentId: string;
  recipientName: string;
  deliveredAtUtc: string;
  notes: string | null;
  signatureReference: string | null;
  photoReference: string | null;
}

export interface DeliverShipmentCommand {
  deliveredAtUtc: string;
  recipientName: string;
  notes?: string | null;
  signatureReference?: string | null;
  photoReference?: string | null;
}

export interface FailDeliveryCommand {
  reason: string;
}
