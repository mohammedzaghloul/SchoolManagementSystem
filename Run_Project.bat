@echo off
echo ==========================================
echo   School Management System - Setup & Run
echo ==========================================
echo.

echo [1/3] Checking Dependencies...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo Error: .NET SDK is not installed.
    pause
    exit /b
)

node -v >nul 2>&1
if %errorlevel% neq 0 (
    echo Error: Node.js is not installed.
    pause
    exit /b
)

echo.
echo [2/3] Cleaning up Port 5033 and Starting Backend...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr :5033 ^| findstr LISTENING') do taskkill /F /PID %%a 2>nul
start "Backend Server" cmd /c "cd backend\School.API && dotnet run"

echo.
echo [3/3] Cleaning up Port 4200 and Starting Frontend...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr :4200 ^| findstr LISTENING') do taskkill /F /PID %%a 2>nul
start "Frontend Server" cmd /c "npm install && npm start"

echo.
echo ==========================================
echo   Backend will be at: http://localhost:5033
echo   Frontend will be at: http://localhost:4200
echo ==========================================
echo.
pause
