<#
.SYNOPSIS
    Confere que a API publicada esta de pe: espera /health responder 200 e mostra o corpo.
    Usado pelo workflow de CD depois do docker compose up.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5080',
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$lastError = $null

# A primeira requisicao depois do deploy e lenta: o SQL Server em container leva
# alguns segundos para aceitar login e a API aplica as migrations pendentes antes
# de escutar. Por isso o loop, e nao uma chamada so.
while ((Get-Date) -lt $deadline) {
    try {
        $response = Invoke-WebRequest -Uri "$BaseUrl/health" -UseBasicParsing -TimeoutSec 15
        if ($response.StatusCode -eq 200) {
            Write-Host "OK: $BaseUrl/health respondeu 200"
            Write-Host $response.Content
            exit 0
        }
        $lastError = "status $($response.StatusCode)"
    }
    catch {
        $lastError = $_.Exception.Message
    }
    Start-Sleep -Seconds 3
}

Write-Host '--- logs do container portfolio-api ---'
docker logs --tail 50 portfolio-api 2>&1
throw "A API nao respondeu 200 em /health dentro de $TimeoutSeconds s. Ultimo erro: $lastError"
