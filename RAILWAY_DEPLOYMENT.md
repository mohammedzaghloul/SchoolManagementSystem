# Railway Deployment

## Services

### 1. Frontend
- Root Directory: `/`
- Build Command: `npm ci && npm run build`
- Start Command: `npm start`
- Required variables:
  - `API_URL=https://<your-backend-domain>`
  - `SIGNALR_URL=https://<your-backend-domain>/chathub`

### 2. Backend API
- Root Directory: `/backend`
- Dockerfile: `backend/Dockerfile`
- Required variables:
  - `ASPNETCORE_ENVIRONMENT=Production`
  - `ASPNETCORE_URLS=http://0.0.0.0:$PORT`
  - `ConnectionStrings__DefaultConnection=<your-sql-server-connection-string>`
  - `ConnectionStrings__Redis=${{Redis.REDISHOST}}:${{Redis.REDISPORT}},user=${{Redis.REDISUSER}},password=${{Redis.REDISPASSWORD}}`
  - `Jwt__Secret=<long-random-secret>`
  - `Jwt__Key=<same-value-as-jwt-secret>`
  - `Cors__AllowedOrigins=https://<your-frontend-domain>`
- Optional variables:
  - `FaceRecognition__BaseUrl=http://face-recognition.railway.internal`
  - `Cloudinary__Enabled=true`
  - `Cloudinary__CloudName=<cloud-name>`
  - `Cloudinary__ApiKey=<api-key>`
  - `Cloudinary__ApiSecret=<api-secret>`

Attach a volume to `/app/wwwroot/uploads` if you want uploaded chat/profile files to persist.

### 3. Redis
- Add a managed Redis service from Railway.

### 4. Face Recognition (optional)
- Root Directory: `/backend/FaceRecognitionService`
- Dockerfile: `backend/FaceRecognitionService/Dockerfile`
- Recommended variable:
  - `FACE_DB_PATH=/app/data/face_data.db`

Attach a volume to `/app/data` if you want registered face data to persist.

## Notes
- The API uses SQL Server, so you need an external SQL Server database or your own SQL Server container. Railway managed databases currently cover PostgreSQL, MySQL, Redis, and MongoDB, not SQL Server.
- Generate public domains for the frontend and backend services from Railway Networking settings.
- The frontend now reads runtime config from `/env.js`, so you can change `API_URL` and `SIGNALR_URL` from Railway variables without rebuilding the Angular app.
