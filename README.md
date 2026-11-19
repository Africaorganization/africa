# Africa

A landing website for African travel experiences. Visitors browse the page and leave
an application (name, email, phone number), which is saved to a SQL Server database.

## Tech stack

| Part | Technology |
|---|---|
| Client | Vue 3, Vite, Vue Router, Vuex, Axios, SCSS |
| Server | ASP.NET Core (.NET 10) Web API |
| Database | SQL Server / Azure SQL Database via Entity Framework Core |
| Hosting | Azure App Service (Linux) |

## Project structure

```
Africa/
├── Africa.sln
├── africa.client/            # Vue 3 front end
│   ├── src/components/       # Header, Footer, Form, Section1Html … Section12Html
│   ├── src/pages/            # HomePage
│   ├── src/router/           # Vue Router
│   ├── src/store/            # Vuex store
│   └── vite.config.js        # dev server proxies /api to the .NET server
└── Africa.Server/            # ASP.NET Core API
    ├── Controllers/          # ApplicationsController (POST /api/Applications)
    ├── Data/                 # Application entity + ApplicationsContext (EF Core)
    ├── Migrations/           # EF Core migrations
    ├── Program.cs
    └── Startup.cs            # services, DB, CORS, static files, SPA fallback
```

In production the server also serves the built Vue app: `dotnet publish` runs
`npm ci` + `npm run build` and copies `africa.client/dist` into `wwwroot`.

## API

| Method | Route | Body | Description |
|---|---|---|---|
| POST | `/api/Applications` | `{ "name": "", "email": "", "phoneNumber": "" }` | Saves a new application |

All three fields are required; `email` and `phoneNumber` are validated.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or newer
- [Node.js](https://nodejs.org/) (LTS) and npm
- A SQL Server database (local SQL Server, Docker, or Azure SQL Database)

## Configuration

The server reads one connection string, **`ApplicationTable`**. Set it in any of these places:

- `Africa.Server/appsettings.Development.json` (the default points to Windows LocalDB)
- User secrets (recommended on macOS/Linux, keeps passwords out of git):
  ```bash
  cd Africa/Africa.Server
  dotnet user-secrets set "ConnectionStrings:ApplicationTable" "Server=...;Database=...;User ID=...;Password=...;Encrypt=True;"
  ```
- Environment variable `ConnectionStrings__ApplicationTable`

Other settings (`appsettings.json`):

| Setting | Default | Meaning |
|---|---|---|
| `Database:MigrateOnStartup` | `true` | Apply EF Core migrations automatically when the app starts |
| `Cors:AllowedOrigins` | `[]` | Extra origins allowed to call the API (only needed if the client is hosted elsewhere) |
| `RateLimiting:Applications:PermitLimit` | `5` | Applications one visitor (IP) can submit per window; extra requests get `429` |
| `RateLimiting:Applications:WindowMinutes` | `10` | Length of the rate-limit window |
| `Cleanup:Enabled` | `true` | Automatically delete old applications |
| `Cleanup:MaxAgeHours` | `12` | Applications older than this are deleted |
| `Cleanup:IntervalHours` | `12` | How often the cleanup runs while the app is awake (it also runs on every start) |

Database connections retry transient errors automatically, so the app survives a
serverless Azure SQL database waking up from auto-pause.

## Running locally

```bash
cd Africa/Africa.Server
dotnet run --launch-profile https
```

This starts the API on `https://localhost:7272` and, through the SPA proxy, the Vite dev
server on `https://localhost:5173` (open that one). The first run creates an HTTPS dev
certificate; trust it with `dotnet dev-certs https --trust` if the browser complains.

Swagger UI is available in Development at `https://localhost:7272/swagger`.

To run the client on its own:

```bash
cd Africa/africa.client
npm install
npm run dev
```

## Database migrations

Migrations are applied on startup. To create a new one after changing the model:

```bash
dotnet tool install --global dotnet-ef   # once
cd Africa/Africa.Server
dotnet ef migrations add <MigrationName>
```

## Building for production

```bash
cd Africa
dotnet publish Africa.Server/Africa.Server.csproj -c Release -o ~/africa-publish
cd ~/africa-publish && zip -r ~/africa-app.zip .
```

Check that `~/africa-publish/wwwroot/index.html` exists. Zip the **contents** of the
publish folder, not the folder itself.

## Deploying to Azure App Service

One-time setup in the Azure Portal:

1. **Create a Web App**: Publish **Code**, Runtime **.NET 10 (LTS)**, OS **Linux**, same
   resource group and region as the database.
2. **SQL server → Networking**: enable *Allow Azure services and resources to access this server*.
3. **Web App → Settings → Environment variables → Connection strings**: add
   `ApplicationTable` with the database's ADO.NET connection string, type **SQLAzure**.
4. **Web App → Configuration → General settings**: make sure **HTTPS Only** is on.

Each deployment:

```bash
az login
az webapp deploy --resource-group <resource-group> --name <app-name> --src-path ~/africa-app.zip --type zip
```

The `Applications` table is created automatically on first start.

### Troubleshooting

Enable **App Service logs → Application logging (Filesystem)** and open **Log stream**, or run
`az webapp log download -g <resource-group> -n <app-name>`.

| Symptom | Likely cause |
|---|---|
| `Connection string 'ApplicationTable' is not configured` | Connection string name misspelled or not applied |
| `Cannot open server … not allowed to access` | Azure-services access not enabled on the SQL server |
| `Login failed for user` | Wrong password in the connection string |
| SQL error `40613` / slow first start | Serverless database is resuming from auto-pause; the app retries |
| 404 on the home page | Zip has no `wwwroot`, or the folder was zipped instead of its contents |
