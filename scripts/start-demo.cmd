@echo off
setlocal
set "PATH=C:\Program Files\Docker\Docker\resources\bin;%PATH%"

docker info >nul 2>nul
if errorlevel 1 (
  echo Starting Docker Desktop...
  start "" "C:\Program Files\Docker\Docker\Docker Desktop.exe"
  for /L %%G in (1,1,60) do (
    timeout /t 2 /nobreak >nul
    docker info >nul 2>nul && goto :docker_ready
  )
  echo Docker Desktop was not ready within 120 seconds.
  exit /b 1
)

:docker_ready
docker compose up --build -d
if errorlevel 1 exit /b 1

echo.
echo QoS demo environment is running:
echo   Web:  http://localhost:5200
echo   WSDL: http://localhost:5201/soap/legacy-weather?wsdl
echo   Stop: scripts\stop-demo.cmd

