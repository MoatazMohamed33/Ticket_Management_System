export const environment = {
  production: false,
  // API dev URL depends on how you run the backend:
  //   - `dotnet run` (Kestrel launchSettings.json)     → http://localhost:5097
  //   - `docker compose up` (docker-compose.yml ports) → http://localhost:5080
  // Set this to whichever you use. Both allow CORS from http://localhost:4200.
  apiBaseUrl: 'http://localhost:5097/api',
  hubBaseUrl: 'http://localhost:5097/hubs',
};
