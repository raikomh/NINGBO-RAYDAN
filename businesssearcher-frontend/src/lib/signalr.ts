import * as signalR from '@microsoft/signalr';

const BASE_URL =
  import.meta.env.VITE_API_URL ||
  (import.meta.env.PROD ? 'https://businesssearcher-api.onrender.com' : 'http://localhost:62560');

let connection: signalR.HubConnection | null = null;

export function getStoreHub(): signalR.HubConnection {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(`${BASE_URL}/hubs/store`, {
        accessTokenFactory: () => localStorage.getItem('token') ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();
  }
  return connection;
}

export async function startStoreHub(): Promise<void> {
  const hub = getStoreHub();
  if (hub.state === signalR.HubConnectionState.Disconnected) {
    await hub.start();
  }
}

export async function stopStoreHub(): Promise<void> {
  if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
    await connection.stop();
  }
}
