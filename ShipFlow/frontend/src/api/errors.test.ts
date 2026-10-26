import { describe, it, expect } from 'vitest';
import { AxiosError, AxiosHeaders, AxiosResponse } from 'axios';
import { mapApiError } from './errorMapper';

describe('mapApiError', () => {
  it('maps RFC 7807 validation problem details with field errors', () => {
    const errorResponse: AxiosResponse = {
      data: {
        type: 'https://tools.ietf.org/html/rfc7231#section-6.5.1',
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: {
          Email: ['The Email field is required.'],
          Password: ['Password must be at least 8 characters long.'],
        },
      },
      status: 400,
      statusText: 'Bad Request',
      headers: {},
      config: { headers: new AxiosHeaders() },
    };

    const axiosError = new AxiosError(
      'Request failed with status code 400',
      'ERR_BAD_REQUEST',
      undefined,
      undefined,
      errorResponse
    );

    const appError = mapApiError(axiosError);

    expect(appError.status).toBe(400);
    expect(appError.message).toBe('Please correct the highlighted errors and try again.');
    expect(appError.fieldErrors).toBeDefined();
    expect(appError.fieldErrors?.email).toBe('The Email field is required.');
    expect(appError.fieldErrors?.password).toBe('Password must be at least 8 characters long.');
  });

  it('maps 409 Conflict with detail message', () => {
    const errorResponse: AxiosResponse = {
      data: {
        title: 'Conflict',
        status: 409,
        detail: 'Vehicle is currently assigned to an active shipment.',
      },
      status: 409,
      statusText: 'Conflict',
      headers: {},
      config: { headers: new AxiosHeaders() },
    };

    const axiosError = new AxiosError(
      'Conflict',
      'ERR_BAD_REQUEST',
      undefined,
      undefined,
      errorResponse
    );

    const appError = mapApiError(axiosError);

    expect(appError.status).toBe(409);
    expect(appError.message).toBe('Vehicle is currently assigned to an active shipment.');
  });

  it('handles 429 Too Many Requests with Retry-After header', () => {
    const errorResponse: AxiosResponse = {
      data: {
        title: 'Too Many Requests',
        status: 429,
        detail: 'GPS telemetry rate limit exceeded (60 requests/minute).',
      },
      status: 429,
      statusText: 'Too Many Requests',
      headers: {
        'retry-after': '30',
      },
      config: { headers: new AxiosHeaders() },
    };

    const axiosError = new AxiosError(
      'Too Many Requests',
      'ERR_BAD_REQUEST',
      undefined,
      undefined,
      errorResponse
    );

    const appError = mapApiError(axiosError);

    expect(appError.status).toBe(429);
    expect(appError.retryAfterSeconds).toBe(30);
    expect(appError.message).toBe('GPS telemetry rate limit exceeded (60 requests/minute).');
  });

  it('handles network failure when no response is received', () => {
    const axiosError = new AxiosError(
      'Network Error',
      'ERR_NETWORK',
      undefined,
      undefined,
      undefined
    );

    const appError = mapApiError(axiosError);

    expect(appError.status).toBe(0);
    expect(appError.message).toContain('Unable to connect to ShipFlow servers');
  });

  it('handles unknown non-Axios generic errors gracefully', () => {
    const genericError = new Error('Unexpected runtime exception');
    const appError = mapApiError(genericError);

    expect(appError.status).toBe(500);
    expect(appError.message).toBe('Unexpected runtime exception');
  });
});
