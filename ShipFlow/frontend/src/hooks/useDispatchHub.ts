import { useEffect, useRef, useState } from 'react';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { getAccessToken } from '@/api/client';
import { queryKeys } from '@/lib/queryClient';
import { AvailableShipmentDto } from '@/api/types';

export interface DispatchHubEvents {
  onNewShipmentAvailable?: (shipment: AvailableShipmentDto) => void;
  onShipmentTaken?: (data: { shipmentId: string; driverName: string }) => void;
  onShipmentCancelled?: (data: { shipmentId: string }) => void;
  onShipmentAssigned?: (data: { shipmentId: string; driverId: string; driverName: string }) => void;
  onDriverAvailabilityChanged?: (data: { driverId: string; isAvailable: boolean }) => void;
}

export function useDispatchHub(callbacks?: DispatchHubEvents) {
  const queryClient = useQueryClient();
  const [isConnected, setIsConnected] = useState(false);
  const connectionRef = useRef<HubConnection | null>(null);
  const callbacksRef = useRef<DispatchHubEvents | undefined>(callbacks);

  useEffect(() => {
    callbacksRef.current = callbacks;
  }, [callbacks]);

  useEffect(() => {
    const token = getAccessToken();
    if (!token) {
      return;
    }

    const rawBaseUrl = import.meta.env.VITE_API_BASE_URL || '';
    const hubUrl = `${rawBaseUrl.replace(/\/$/, '')}/hubs/dispatch`;

    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => getAccessToken() || '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on('NewEligibleShipmentAvailable', (shipment: AvailableShipmentDto) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.driver.availableShipments });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
      callbacksRef.current?.onNewShipmentAvailable?.(shipment);
    });

    connection.on('ShipmentTaken', (data: { shipmentId: string; driverName: string }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.driver.availableShipments });
      queryClient.invalidateQueries({ queryKey: queryKeys.dispatch.matchingStatus(data.shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(data.shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
      callbacksRef.current?.onShipmentTaken?.(data);
    });

    connection.on('ShipmentCancelled', (data: { shipmentId: string }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.driver.availableShipments });
      queryClient.invalidateQueries({ queryKey: queryKeys.dispatch.matchingStatus(data.shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(data.shipmentId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
      callbacksRef.current?.onShipmentCancelled?.(data);
    });

    connection.on(
      'ShipmentAssigned',
      (data: { shipmentId: string; driverId: string; driverName: string }) => {
        queryClient.invalidateQueries({ queryKey: queryKeys.driver.availableShipments });
        queryClient.invalidateQueries({ queryKey: queryKeys.driver.currentAssignment });
        queryClient.invalidateQueries({ queryKey: queryKeys.driver.profile });
        queryClient.invalidateQueries({ queryKey: queryKeys.dispatch.matchingStatus(data.shipmentId) });
        queryClient.invalidateQueries({ queryKey: queryKeys.shipments.detail(data.shipmentId) });
        queryClient.invalidateQueries({ queryKey: queryKeys.shipments.all });
        callbacksRef.current?.onShipmentAssigned?.(data);
      }
    );

    connection.on(
      'DriverAvailabilityChanged',
      (data: { driverId: string; isAvailable: boolean }) => {
        queryClient.invalidateQueries({ queryKey: queryKeys.driver.profile });
        queryClient.invalidateQueries({ queryKey: ['fleet'] });
        callbacksRef.current?.onDriverAvailabilityChanged?.(data);
      }
    );

    let isCancelled = false;

    connection
      .start()
      .then(() => {
        if (!isCancelled) {
          setIsConnected(true);
        }
      })
      .catch((err) => {
        if (!isCancelled) {
          console.warn('SignalR dispatch hub connection error:', err);
          setIsConnected(false);
        }
      });

    connection.onreconnecting(() => setIsConnected(false));
    connection.onreconnected(() => setIsConnected(true));
    connection.onclose(() => setIsConnected(false));

    return () => {
      isCancelled = true;
      if (
        connection.state === HubConnectionState.Connected ||
        connection.state === HubConnectionState.Connecting
      ) {
        connection.stop().catch(() => {});
      }
    };
  }, [queryClient]);

  return { isConnected, connection: connectionRef.current };
}
