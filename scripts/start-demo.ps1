$ErrorActionPreference = 'Stop'

$dockerBin = 'C:\Program Files\Docker\Docker\resources\bin'
if (Test-Path -LiteralPath $dockerBin) {
    $env:Path = "$dockerBin;$env:Path"
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker was not found. Install and start Docker Desktop first.'
}

cmd.exe /c "docker info >nul 2>nul"
if ($LASTEXITCODE -ne 0) {
    $desktop = 'C:\Program Files\Docker\Docker\Docker Desktop.exe'
    if (-not (Test-Path -LiteralPath $desktop)) { throw 'Docker engine is offline and Docker Desktop was not found.' }
    Write-Host 'Starting Docker Desktop...' -ForegroundColor Yellow
    Start-Process -FilePath $desktop -WindowStyle Hidden
    $ready = $false
    foreach ($attempt in 1..60) {
        Start-Sleep -Seconds 2
        cmd.exe /c "docker info >nul 2>nul"
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    }
    if (-not $ready) { throw 'Docker Desktop was not ready within 120 seconds. Open Docker Desktop to inspect its status.' }
}

docker compose up --build -d
if ($LASTEXITCODE -ne 0) { throw 'Docker Compose failed to start.' }

Write-Host 'QoS demo environment is running:' -ForegroundColor Green
Write-Host '  Web:  http://localhost:5200'
Write-Host '  WSDL: http://localhost:5201/soap/legacy-weather?wsdl'
Write-Host 'Stop with: docker compose down'
