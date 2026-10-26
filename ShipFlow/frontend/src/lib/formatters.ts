/**
 * Formats a UTC ISO date string into a user-friendly local date and time string.
 */
export function formatDateTime(utcDateString: string | null | undefined): string {
  if (!utcDateString) return '—';
  try {
    const date = new Date(utcDateString);
    if (isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(date);
  } catch {
    return '—';
  }
}

/**
 * Formats a UTC date string into short date (e.g. Oct 24, 2026).
 */
export function formatDate(utcDateString: string | null | undefined): string {
  if (!utcDateString) return '—';
  try {
    const date = new Date(utcDateString);
    if (isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    }).format(date);
  } catch {
    return '—';
  }
}

/**
 * Formats time only (e.g. 14:30).
 */
export function formatTime(utcDateString: string | null | undefined): string {
  if (!utcDateString) return '—';
  try {
    const date = new Date(utcDateString);
    if (isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat(undefined, {
      hour: '2-digit',
      minute: '2-digit',
    }).format(date);
  } catch {
    return '—';
  }
}

/**
 * Formats relative time (e.g., "5 minutes ago", "2 hours ago").
 */
export function formatRelativeTime(utcDateString: string | null | undefined): string {
  if (!utcDateString) return '—';
  try {
    const date = new Date(utcDateString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffSec = Math.floor(diffMs / 1000);
    const diffMin = Math.floor(diffSec / 60);
    const diffHour = Math.floor(diffMin / 60);
    const diffDay = Math.floor(diffHour / 24);

    if (diffSec < 60) return 'Just now';
    if (diffMin < 60) return `${diffMin} min ago`;
    if (diffHour < 24) return `${diffHour}h ago`;
    if (diffDay < 7) return `${diffDay}d ago`;
    return formatDate(utcDateString);
  } catch {
    return '—';
  }
}

/**
 * Formats currency amount.
 */
export function formatCurrency(amount: number | null | undefined, currency = 'USD'): string {
  if (amount == null) return '—';
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: currency.toUpperCase(),
      maximumFractionDigits: 2,
    }).format(amount);
  } catch {
    return `${currency.toUpperCase()} ${amount.toFixed(2)}`;
  }
}

/**
 * Formats cargo weight (kg / metric tons).
 */
export function formatWeight(weightKg: number | null | undefined): string {
  if (weightKg == null) return '—';
  if (weightKg >= 1000) {
    const tons = weightKg / 1000;
    return `${tons.toLocaleString(undefined, { maximumFractionDigits: 2 })} t`;
  }
  return `${weightKg.toLocaleString(undefined, { maximumFractionDigits: 1 })} kg`;
}

/**
 * Formats cargo volume (m³).
 */
export function formatVolume(volumeM3: number | null | undefined): string {
  if (volumeM3 == null) return '—';
  return `${volumeM3.toLocaleString(undefined, { maximumFractionDigits: 2 })} m³`;
}
