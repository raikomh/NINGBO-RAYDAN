import * as signalR from '@microsoft/signalr';

// VITE_API_URL='/' significa "mismo origen" (build Docker: nginx sirve el front y
// hace de proxy del API). Al quitar la barra final queda '' y las URLs de los hubs
// salen relativas (/hubs/...), que SignalR resuelve contra el origen de la página.
const BASE_URL = (
  import.meta.env.VITE_API_URL ||
  (import.meta.env.PROD ? 'https://businesssearcher-api.onrender.com' : 'http://localhost:62560')
).replace(/\/+$/, '');

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
