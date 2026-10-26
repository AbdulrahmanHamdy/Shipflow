export interface QuoteDto {
  id: string;
  shipmentId: string;
  companyId: string;
  amount: number;
  currency: string;
  expiresAtUtc: string;
  terms: string;
  status: string;
  createdAtUtc: string;
  resolvedAtUtc: string | null;
}

export interface SubmitQuoteCommand {
  amount: number;
  currency: string;
  expiresAtUtc: string;
  terms: string;
}
