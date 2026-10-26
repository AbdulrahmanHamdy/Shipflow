import { apiClient } from './client';
import { InitiatePaymentCommand, PaymentCallbackResponse, PaymentResponseDto } from './types';

export const paymentsApi = {
  initiate: async (command: InitiatePaymentCommand, signal?: AbortSignal): Promise<PaymentResponseDto> => {
    const res = await apiClient.post<PaymentResponseDto>('/api/payments/initiate', command, { signal });
    return res.data;
  },

  handleRedirect: async (queryString: string, signal?: AbortSignal): Promise<PaymentCallbackResponse> => {
    const res = await apiClient.get<PaymentCallbackResponse>(`/api/payments/callback?${queryString}`, { signal });
    return res.data;
  },
};
