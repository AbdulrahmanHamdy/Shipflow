import { apiClient } from './client';
import { QuoteDto, ShipmentDetailsDto, SubmitQuoteCommand } from './types';

export const quotesApi = {
  submit: async (shipmentId: string, command: SubmitQuoteCommand, signal?: AbortSignal): Promise<QuoteDto> => {
    const res = await apiClient.post<QuoteDto>(`/api/shipments/${shipmentId}/quotes`, command, { signal });
    return res.data;
  },

  list: async (shipmentId: string, signal?: AbortSignal): Promise<QuoteDto[]> => {
    const res = await apiClient.get<QuoteDto[]>(`/api/shipments/${shipmentId}/quotes`, { signal });
    return res.data;
  },

  accept: async (shipmentId: string, quoteId: string, signal?: AbortSignal): Promise<ShipmentDetailsDto> => {
    const res = await apiClient.post<ShipmentDetailsDto>(
      `/api/shipments/${shipmentId}/quotes/${quoteId}/accept`,
      {},
      { signal }
    );
    return res.data;
  },

  withdraw: async (shipmentId: string, quoteId: string, signal?: AbortSignal): Promise<QuoteDto> => {
    const res = await apiClient.post<QuoteDto>(
      `/api/shipments/${shipmentId}/quotes/${quoteId}/withdraw`,
      {},
      { signal }
    );
    return res.data;
  },
};
