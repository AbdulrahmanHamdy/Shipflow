export interface ProblemDetails {
  type?: string;
  title?: string;
  status: number;
  detail?: string;
  instance?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export interface AppError {
  status: number;
  code?: string;
  message: string;
  detail?: string;
  fieldErrors?: Record<string, string>;
  traceId?: string;
  retryAfterSeconds?: number;
}
