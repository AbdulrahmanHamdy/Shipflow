import { useEffect } from 'react';

/**
 * Sets the document title with consistent ShipFlow branding.
 */
export function usePageTitle(title: string): void {
  useEffect(() => {
    const prevTitle = document.title;
    document.title = title ? `${title} | ShipFlow` : 'ShipFlow | Smart Logistics & Shipment Management';

    return () => {
      document.title = prevTitle;
    };
  }, [title]);
}
