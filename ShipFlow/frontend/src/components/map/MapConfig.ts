import L from 'leaflet';

export const MAP_CONFIG = {
  tileUrl: import.meta.env.VITE_MAP_TILE_URL || 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
  attribution:
    import.meta.env.VITE_MAP_ATTRIBUTION ||
    '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap</a> contributors',
  defaultCenter: [30.0444, 31.2357] as [number, number], // Cairo / Middle East regional hub default
  defaultZoom: 10,
  maxZoom: 19,
};

/**
 * Validates latitude and longitude ranges.
 */
export function isValidLatLng(lat: number | null | undefined, lng: number | null | undefined): boolean {
  if (lat == null || lng == null) return false;
  if (isNaN(lat) || isNaN(lng)) return false;
  return lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180;
}

/**
 * Creates custom brand SVG divIcon markers for Leaflet.
 */
export function createMapIcon(type: 'pickup' | 'delivery' | 'driver' | 'waypoint'): L.DivIcon {
  let svgContent = '';
  const className = 'custom-map-marker';

  switch (type) {
    case 'pickup':
      svgContent = `
        <div style="background-color:#15803D;color:#ffffff;border:2.5px solid #ffffff;border-radius:50%;width:30px;height:30px;display:flex;align-items:center;justify-content:center;box-shadow:0 3px 6px rgba(0,0,0,0.3);">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><path d="M12 2v20M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
        </div>`;
      break;

    case 'delivery':
      svgContent = `
        <div style="background-color:#0284C7;color:#ffffff;border:2.5px solid #ffffff;border-radius:50%;width:30px;height:30px;display:flex;align-items:center;justify-content:center;box-shadow:0 3px 6px rgba(0,0,0,0.3);">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><path d="m9 11 3 3L22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/></svg>
        </div>`;
      break;

    case 'driver':
      svgContent = `
        <div style="background-color:#0B2A1F;color:#10B981;border:2.5px solid #10B981;border-radius:50%;width:34px;height:34px;display:flex;align-items:center;justify-content:center;box-shadow:0 0 12px rgba(16,185,129,0.5);animation:pulse 2s infinite;">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><rect width="16" height="16" x="4" y="4" rx="2"/><circle cx="12" cy="12" r="3"/></svg>
        </div>`;
      break;

    case 'waypoint':
      svgContent = `
        <div style="background-color:#10B981;border:2px solid #ffffff;border-radius:50%;width:14px;height:14px;box-shadow:0 1px 3px rgba(0,0,0,0.3);"></div>`;
      break;
  }

  return L.divIcon({
    html: svgContent,
    className,
    iconSize: type === 'driver' ? [34, 34] : type === 'waypoint' ? [14, 14] : [30, 30],
    iconAnchor: type === 'driver' ? [17, 17] : type === 'waypoint' ? [7, 7] : [15, 15],
    popupAnchor: [0, -15],
  });
}
