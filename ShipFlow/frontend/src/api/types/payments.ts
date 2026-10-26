export interface InitiatePaymentCommand {
  shipmentId: string;
}

export interface PaymentResponseDto {
  shipmentId: string;
  orderId: string;
  paymentKey: string;
  iframeUrl: string;
  amount: number;
  currency: string;
}

export interface PaymentCallbackResponse {
  success: boolean;
  message: string;
}
