$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
& dotnet run --project (Join-Path $Root 'tests/material-binding/tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Falha nos testes de binding/recurso.' }
& dotnet run --project (Join-Path $Root 'tests/eclipse-poses/tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Falha nos testes de poses.' }
Write-Host 'Testes isolados concluídos. Isso NÃO confirma renderização Unity ou gameplay.'
