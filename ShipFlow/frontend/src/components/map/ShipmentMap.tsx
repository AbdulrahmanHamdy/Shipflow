import React, { useState, useEffect, useMemo } from 'react';
import { MapContainer, TileLayer, Marker, Popup, Polyline, useMap } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import { MAP_CONFIG, createMapIcon, isValidLatLng } from './MapConfig';
import { StopDto, TrackingPointDto } from '@/api/types';
import { formatDateTime, formatRelativeTime } from '@/lib/formatters';
import { Button } from '@/components/ui/Button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/Table';
import { MapPin, Navigation, List, Map as MapIcon, Clock, AlertCircle } from 'lucide-react';

interface ShipmentMapProps {
  stops?: StopDto[];
  trackingPoints?: TrackingPointDto[];
  latestPosition?: TrackingPointDto | null;
  className?: string;
  height?: string;
}

// Sub-component to fit map bounds to points
const BoundsAdjuster: React.FC<{ points: [number, number][] }> = ({ points }) => {
  const map = useMap();

  useEffect(() => {
    if (points.length === 1) {
      map.setView(points[0], 13);
    } else if (points.length > 1) {
      map.fitBounds(points, { padding: [50, 50], maxZoom: 15 });
    }
  }, [map, points]);

  return null;
};

export const ShipmentMap: React.FC<ShipmentMapProps> = ({
  stops = [],
  trackingPoints = [],
  latestPosition,
  className = '',
  height = '420px',
}) => {
  const [viewMode, setViewMode] = useState<'map' | 'table'>('map');

  // Filter valid stop points
  const validStops = useMemo(
    () => stops.filter((s) => isValidLatLng(s.latitude, s.longitude)),
    [stops]
  );

  // Filter valid tracking points
  const validHistoryPoints = useMemo(
    () => trackingPoints.filter((p) => isValidLatLng(p.latitude, p.longitude)),
    [trackingPoints]
  );

  const hasDriver = latestPosition && isValidLatLng(latestPosition.latitude, latestPosition.longitude);

  // Polyline coordinates from recorded GPS points
  const polylineCoords: [number, number][] = useMemo(() => {
    return validHistoryPoints.map((p) => [p.latitude, p.longitude] as [number, number]);
  }, [validHistoryPoints]);

  // All coordinates for auto-fit bounds
  const allPoints: [number, number][] = useMemo(() => {
    const pts: [number, number][] = [];
    validStops.forEach((s) => pts.push([s.latitude, s.longitude]));
    validHistoryPoints.forEach((p) => pts.push([p.latitude, p.longitude]));
    if (hasDriver) pts.push([latestPosition!.latitude, latestPosition!.longitude]);
    return pts;
  }, [validStops, validHistoryPoints, hasDriver, latestPosition]);

  const defaultCenter = allPoints.length > 0 ? allPoints[0] : MAP_CONFIG.defaultCenter;

  // Stale check (e.g. if last position is older than 1 hour)
  const isStale = useMemo(() => {
    if (!latestPosition?.recordedAtUtc) return false;
    const diffHours = (Date.now() - new Date(latestPosition.recordedAtUtc).getTime()) / (1000 * 60 * 60);
    return diffHours > 1;
  }, [latestPosition]);

  return (
    <div className={`flex flex-col rounded-2xl border border-neutral-border bg-white shadow-card overflow-hidden ${className}`}>
      {/* Map Control Bar */}
      <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 border-b border-neutral-border/60 bg-neutral-appBg/50 text-xs text-neutral-secondary">
        <div className="flex items-center gap-4 flex-wrap">
          {/* Status info */}
          {hasDriver && (
            <div className="flex items-center gap-1.5 font-medium text-neutral-primary">
              <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-ping inline-block" />
              <span>Live Position Reported: {formatRelativeTime(latestPosition!.recordedAtUtc)}</span>
              {isStale && (
                <span className="inline-flex items-center gap-1 text-amber-700 bg-amber-100 px-2 py-0.5 rounded-md font-semibold text-[11px]">
                  <AlertCircle className="w-3 h-3" /> Stale (over 1h)
                </span>
              )}
            </div>
          )}

          {!hasDriver && validStops.length > 0 && (
            <div className="flex items-center gap-1.5">
              <MapPin className="w-3.5 h-3.5 text-brand" />
              <span>{validStops.length} Waypoints plotted</span>
            </div>
          )}
        </div>

        {/* View toggle (Map vs Accessible Table) */}
        <div className="flex items-center gap-1 bg-white p-0.5 rounded-lg border border-neutral-border">
          <Button
            size="sm"
            variant={viewMode === 'map' ? 'secondary' : 'ghost'}
            className="h-7 text-xs px-2.5"
            onClick={() => setViewMode('map')}
            leftIcon={<MapIcon className="w-3 h-3" />}
          >
            Map View
          </Button>
          <Button
            size="sm"
            variant={viewMode === 'table' ? 'secondary' : 'ghost'}
            className="h-7 text-xs px-2.5"
            onClick={() => setViewMode('table')}
            leftIcon={<List className="w-3 h-3" />}
          >
            Accessible Table
          </Button>
        </div>
      </div>

      {/* Main View Area */}
      {viewMode === 'table' ? (
        <div className="p-4 overflow-y-auto" style={{ maxHeight: height }}>
          <h4 className="text-sm font-bold text-neutral-primary mb-3">Position &amp; Waypoint Coordinates</h4>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Type</TableHead>
                <TableHead>Label / Address</TableHead>
                <TableHead>Latitude</TableHead>
                <TableHead>Longitude</TableHead>
                <TableHead>Timestamp</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {validStops.map((stop, idx) => (
                <TableRow key={stop.id || idx}>
                  <TableCell className="font-semibold text-xs">
                    {idx === 0 ? 'Pickup' : idx === validStops.length - 1 ? 'Delivery' : 'Intermediate Stop'}
                  </TableCell>
                  <TableCell>{stop.address}</TableCell>
                  <TableCell className="font-mono text-xs">{stop.latitude.toFixed(5)}</TableCell>
                  <TableCell className="font-mono text-xs">{stop.longitude.toFixed(5)}</TableCell>
                  <TableCell className="text-xs text-neutral-secondary">
                    {formatDateTime(stop.windowStartUtc)}
                  </TableCell>
                </TableRow>
              ))}

              {validHistoryPoints.map((point) => (
                <TableRow key={point.id}>
                  <TableCell className="text-xs text-brand font-medium">Recorded GPS</TableCell>
                  <TableCell className="text-xs text-neutral-secondary">
                    Speed: {point.speedKph != null ? `${point.speedKph} km/h` : '—'}
                  </TableCell>
                  <TableCell className="font-mono text-xs">{point.latitude.toFixed(5)}</TableCell>
                  <TableCell className="font-mono text-xs">{point.longitude.toFixed(5)}</TableCell>
                  <TableCell className="text-xs text-neutral-secondary">
                    {formatDateTime(point.recordedAtUtc)}
                  </TableCell>
                </TableRow>
              ))}

              {validStops.length === 0 && validHistoryPoints.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} className="text-center py-6 text-neutral-secondary">
                    No location coordinates have been reported for this shipment yet.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </div>
      ) : (
        <div className="relative w-full" style={{ height }}>
          {allPoints.length === 0 ? (
            <div className="h-full flex flex-col items-center justify-center p-8 text-center bg-neutral-appBg">
              <Navigation className="w-10 h-10 text-neutral-muted mb-2 animate-bounce" />
              <p className="text-sm font-semibold text-neutral-primary">No Coordinate Data Available</p>
              <p className="text-xs text-neutral-secondary max-w-sm mt-1">
                Geographic coordinates have not yet been assigned to this shipment.
              </p>
            </div>
          ) : (
            <>
              <MapContainer
                center={defaultCenter}
                zoom={MAP_CONFIG.defaultZoom}
                scrollWheelZoom={false}
                style={{ height: '100%', width: '100%' }}
              >
                <TileLayer
                  attribution={MAP_CONFIG.attribution}
                  url={MAP_CONFIG.tileUrl}
                  maxZoom={MAP_CONFIG.maxZoom}
                />

                <BoundsAdjuster points={allPoints} />

                {/* Stops Markers */}
                {validStops.map((stop, idx) => {
                  const isPickup = idx === 0;
                  const isDelivery = idx === validStops.length - 1;
                  const iconType = isPickup ? 'pickup' : isDelivery ? 'delivery' : 'waypoint';

                  return (
                    <Marker
                      key={stop.id || idx}
                      position={[stop.latitude, stop.longitude]}
                      icon={createMapIcon(iconType)}
                    >
                      <Popup>
                        <div className="p-1 space-y-1">
                          <span className="font-bold text-xs uppercase text-brand">
                            {isPickup ? 'Pickup Location' : isDelivery ? 'Delivery Destination' : 'Waypoint'}
                          </span>
                          <p className="text-xs font-medium text-neutral-primary">{stop.address}</p>
                          {stop.windowStartUtc && (
                            <p className="text-[11px] text-neutral-secondary flex items-center gap-1">
                              <Clock className="w-3 h-3" />
                              {formatDateTime(stop.windowStartUtc)}
                            </p>
                          )}
                        </div>
                      </Popup>
                    </Marker>
                  );
                })}

                {/* Recorded Polyline */}
                {polylineCoords.length > 1 && (
                  <Polyline
                    positions={polylineCoords}
                    pathOptions={{
                      color: '#10B981',
                      weight: 4,
                      opacity: 0.85,
                      dashArray: '6, 8',
                    }}
                  />
                )}

                {/* Driver Live Marker */}
                {hasDriver && (
                  <Marker
                    position={[latestPosition!.latitude, latestPosition!.longitude]}
                    icon={createMapIcon('driver')}
                  >
                    <Popup>
                      <div className="p-1 space-y-1">
                        <span className="font-bold text-xs uppercase text-emerald-600">Current Position</span>
                        <p className="text-xs text-neutral-secondary">
                          Reported: {formatDateTime(latestPosition!.recordedAtUtc)}
                        </p>
                        {latestPosition!.speedKph != null && (
                          <p className="text-xs font-semibold text-neutral-primary">
                            Speed: {latestPosition!.speedKph} km/h
                          </p>
                        )}
                      </div>
                    </Popup>
                  </Marker>
                )}
              </MapContainer>

              {/* Map Legend Overlay */}
              <div className="absolute bottom-3 left-3 z-[1000] bg-white/95 backdrop-blur-xs px-3 py-2 rounded-xl shadow-card border border-neutral-border text-[11px] text-neutral-secondary flex items-center gap-3">
                <div className="flex items-center gap-1.5">
                  <span className="w-3 h-3 rounded-full bg-brand border border-white shadow-2xs inline-block" />
                  <span>Pickup</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <span className="w-3 h-3 rounded-full bg-sky-600 border border-white shadow-2xs inline-block" />
                  <span>Destination</span>
                </div>
                {hasDriver && (
                  <div className="flex items-center gap-1.5">
                    <span className="w-3 h-3 rounded-full bg-forest-night border border-emerald-400 shadow-2xs inline-block" />
                    <span>Driver</span>
                  </div>
                )}
                {polylineCoords.length > 1 && (
                  <div className="flex items-center gap-1.5">
                    <span className="w-4 h-0.5 bg-emerald-500 border-dashed inline-block" />
                    <span>Recorded Trail</span>
                  </div>
                )}
              </div>
            </>
          )}
        </div>
      )}
    </div>
  );
};
