#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
dotnet run --project "$ROOT/tests/material-binding/tests.csproj" -c Release
dotnet run --project "$ROOT/tests/eclipse-poses/tests.csproj" -c Release
printf '\nTestes isolados concluídos. Isso NÃO confirma renderização Unity ou gameplay.\n'
