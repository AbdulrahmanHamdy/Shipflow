import { useMutation, useQuery } from '@tanstack/react-query';
import { paymentsApi } from '@/api';
import { InitiatePaymentCommand, PaymentCallbackResponse, PaymentResponseDto } from '@/api/types';

export function useInitiatePaymentMutation() {
  return useMutation<PaymentResponseDto, unknown, InitiatePaymentCommand>({
    mutationFn: (command) => paymentsApi.initiate(command),
  });
}

export function usePaymentRedirectCallbackQuery(queryString: string, enabled = true) {
  return useQuery<PaymentCallbackResponse>({
    queryKey: ['payments', 'callback', queryString],
    queryFn: ({ signal }) => paymentsApi.handleRedirect(queryString, signal),
    enabled: !!queryString && enabled,
    retry: false,
  });
}
