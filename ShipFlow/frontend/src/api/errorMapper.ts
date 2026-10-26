import axios, { AxiosError } from 'axios';
import { AppError, ProblemDetails } from './types/problemDetails';

/**
 * Maps any API error (AxiosError, ProblemDetails, network failure) into a clean, normalized AppError.
 */
export function mapApiError(error: unknown): AppError {
  if (!error) {
    return {
      status: 0,
      message: 'An unknown error occurred.',
    };
  }

  // Generic JS Error that is not an Axios error
  if (!axios.isAxiosError(error) && error instanceof Error) {
    return {
      status: 500,
      message: error.message || 'An unexpected application error occurred.',
    };
  }

  const axiosError = error as AxiosError<ProblemDetails>;

  // Network / Connection Error
  if (!axiosError.response) {
    if (axiosError.code === 'ECONNABORTED' || axiosError.message?.includes('timeout')) {
      return {
        status: 408,
        message: 'The request timed out. Please check your connection and try again.',
      };
    }
    return {
      status: 0,
      message: 'Unable to connect to ShipFlow servers. Please check your network or server status.',
    };
  }

  const { status, headers, data } = axiosError.response;
  const traceId = data?.traceId;
  const code = data?.code;

  // Extract Retry-After header for 429
  let retryAfterSeconds: number | undefined;
  if (status === 429 && headers) {
    const retryHeader =
      typeof (headers as any).get === 'function'
        ? (headers as any).get('retry-after')
        : (headers as any)['retry-after'] || (headers as any)['Retry-After'];
    if (retryHeader) {
      const parsed = parseInt(String(retryHeader), 10);
      if (!isNaN(parsed)) retryAfterSeconds = parsed;
    }
  }

  // Extract field validation errors (e.g. from FluentValidation)
  let fieldErrors: Record<string, string> | undefined;
  if (data?.errors && typeof data.errors === 'object') {
    fieldErrors = {};
    for (const [key, messages] of Object.entries(data.errors)) {
      if (Array.isArray(messages) && messages.length > 0) {
        // Normalize field keys like Cargo.WeightKg or email to lowercase/camelCase for form matching
        const normalizedKey = key.includes('.') ? key.split('.').pop()! : key;
        const formKey = normalizedKey.charAt(0).toLowerCase() + normalizedKey.slice(1);
        fieldErrors[formKey] = messages[0];
        // Keep original key as well
        fieldErrors[key] = messages[0];
      }
    }
  }

  // Derive human-readable message based on status and domain code
  let message = data?.detail || data?.title || 'An error occurred while processing your request.';

  switch (status) {
    case 400:
      if (code === 'invalid_state') {
        message = data.detail || 'This action cannot be performed in the current shipment state.';
      } else if (fieldErrors && Object.keys(fieldErrors).length > 0) {
        message = 'Please correct the highlighted errors and try again.';
      } else {
        message = data.detail || 'The request was invalid. Please review your input.';
      }
      break;

    case 401:
      message = 'Your session has expired or credentials were invalid. Please sign in again.';
      break;

    case 403:
      if (code === 'not_owner' || code === 'cross_company') {
        message = 'You do not have permission to access resources belonging to another company.';
      } else {
        message = 'You do not have sufficient permissions to perform this action.';
      }
      break;

    case 404:
      message = data.detail || 'The requested resource was not found or is no longer available.';
      break;

    case 409:
      if (code === 'assignment_conflict') {
        message = 'The selected driver or vehicle is already assigned during this delivery window.';
      } else if (code === 'concurrency_conflict') {
        message = 'Another user updated this record simultaneously. Please refresh the page.';
      } else if (code === 'quote_rule') {
        message = 'Quotes cannot be modified or submitted in the current shipment state.';
      } else {
        message = data.detail || 'A conflict occurred. The resource may already exist or have changed.';
      }
      break;

    case 422:
      message = data?.detail || 'The operation could not be processed due to business rules.';
      break;

    case 429:
      message =
        data?.detail ||
        (retryAfterSeconds
          ? `Rate limit exceeded. Please wait ${retryAfterSeconds} seconds before trying again.`
          : 'Rate limit exceeded. Please slow down and try again shortly.');
      break;

    case 500:
    case 502:
    case 503:
    case 504:
      message = 'A server error occurred. Our operations team has been notified.';
      break;
  }

  return {
    status,
    code,
    message,
    detail: data?.detail,
    fieldErrors,
    traceId,
    retryAfterSeconds,
  };
}
