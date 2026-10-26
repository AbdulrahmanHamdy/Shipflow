import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}

/**
 * Validates that an internal return URL is a relative same-origin path to prevent open redirects.
 */
export function sanitizeReturnUrl(url: string | null | undefined, fallback = '/'): string {
  if (!url) return fallback;
  if (url.startsWith('/') && !url.startsWith('//') && !url.includes('\\')) {
    return url;
  }
  return fallback;
}
