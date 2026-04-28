# Railway Deployment

This project is deployed as two Railway services in the same project:

- Backend API: ASP.NET Core Web API from `backend/`
- Frontend: Angular static app served by `server.js` from the repository root

Railway project:

```text
https://railway.com/project/d126814d-2e01-423b-b707-e3ebf16c28d9
```

## Backend Service

Deploy root:

```text
backend/
```

Build:

```text
Dockerfile
```

Runtime:

```text
dotnet School.API.dll
```

Health check:

```text
/health
```

Required Railway variables:

```text
ASPNETCORE_ENVIRONMENT=Production
DB_PASSWORD=<MonsterASP password>
CONNECTION_STRING=Server=db49846.databaseasp.net;Database=db49846;User Id=db49846;Password=${DB_PASSWORD};Encrypt=False;MultipleActiveResultSets=True;
JWT_SECRET=<long random secret>
FRONTEND_URL=https://<frontend-service>.up.railway.app
```

Optional variables:

```text
SCHOOL_RUN_SEED=0
FACE_RECOGNITION_API_KEY=<only if face service is deployed>
CentralAuth__Enabled=false
```

The API reads Railway's `PORT` variable and binds to `http://0.0.0.0:{PORT}`.

## Frontend Service

Deploy root:

```text
/
```

Build:

```text
npm install --legacy-peer-deps
npm run build
```

Start:

```text
npm start
```

Required Railway variables:

```text
API_URL=https://<backend-service>.up.railway.app
SIGNALR_URL=https://<backend-service>.up.railway.app/chathub
```

The frontend serves `/env.js` dynamically from `server.js`, so API URLs can change from Railway variables without rebuilding the Angular bundle.

## CLI Deployment

Install or update the Railway CLI:

```powershell
npm i -g @railway/cli
```

Log in:

```powershell
railway login --browserless
```

Link the local checkout to the existing backend service:

```powershell
railway link --project d126814d-2e01-423b-b707-e3ebf16c28d9 --service ee8dda16-dcea-4401-aa6a-3ab41ae91273 --environment production
```

Deploy backend to the existing backend service:

```powershell
railway up backend --path-as-root --project d126814d-2e01-423b-b707-e3ebf16c28d9 --service ee8dda16-dcea-4401-aa6a-3ab41ae91273 --environment production --message "Deploy backend API"
```

Set backend variables from PowerShell. Keep the connection string in single quotes so `${DB_PASSWORD}` is not expanded by PowerShell:

```powershell
railway variable set --service ee8dda16-dcea-4401-aa6a-3ab41ae91273 --environment production ASPNETCORE_ENVIRONMENT=Production DB_PASSWORD=<MonsterASP password> 'CONNECTION_STRING=Server=db49846.databaseasp.net;Database=db49846;User Id=db49846;Password=${DB_PASSWORD};Encrypt=False;MultipleActiveResultSets=True;' JWT_SECRET=<long-random-secret> FRONTEND_URL=https://<frontend-service>.up.railway.app
```

Create a separate frontend service if one does not already exist:

```powershell
railway add --service school-frontend
```

Set frontend variables after the backend domain exists:

```powershell
railway variable set --service school-frontend --environment production API_URL=https://<backend-service>.up.railway.app SIGNALR_URL=https://<backend-service>.up.railway.app/chathub
```

Deploy frontend:

```powershell
railway up --project d126814d-2e01-423b-b707-e3ebf16c28d9 --service school-frontend --environment production --message "Deploy frontend"
```

Generate public domains:

```powershell
railway domain --service ee8dda16-dcea-4401-aa6a-3ab41ae91273
railway domain --service school-frontend
```

After the frontend domain exists, update backend `FRONTEND_URL`. After the backend domain exists, update frontend `API_URL` and `SIGNALR_URL`.

## Database

The app uses the external MonsterASP SQL Server:

```text
Server=db49846.databaseasp.net
Database=db49846
User Id=db49846
Port=1433
```

Do not commit the password. Set `DB_PASSWORD` in Railway only.

## Verification

After deployment:

```powershell
railway service status --service ee8dda16-dcea-4401-aa6a-3ab41ae91273 --environment production
railway logs --service ee8dda16-dcea-4401-aa6a-3ab41ae91273 --environment production
```

Then check:

```text
https://<backend-service>.up.railway.app/health
https://<frontend-service>.up.railway.app
```
