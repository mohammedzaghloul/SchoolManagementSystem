@echo off
echo ==========================================
echo   School Management System - Setup & Run
echo ==========================================
echo.

REM Check if dotnet is already running
tasklist /FI "IMAGENAME eq dotnet.exe" 2>nul | find /I "dotnet.exe" >nul
if %errorlevel% equ 0 (
    echo.
    echo WARNING: Backend is already running!
    echo.
    choice /C YN /M "Kill existing backend and restart"
    if %errorlevel% equ 1 (
        echo Killing existing dotnet processes...
        taskkill /F /IM dotnet.exe >nul 2>&1
        timeout /t 2 /nobreak >nul
    ) else (
        echo.
        echo Existing backend will continue running.
        echo Note: Only one backend instance can run at a time.
    )
)

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
start "Backend Server" cmd /c "cd backend\School.API && set PORT=5033 && dotnet run --launch-profile http"

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
