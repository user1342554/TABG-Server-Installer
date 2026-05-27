#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

cd "$ROOT"
DLL="$ROOT/TabgInstaller.App/bin/Debug/net8.0/TabgInstaller.App.dll"

if [[ ! -f "$DLL" ]]; then
  dotnet build TabgInstaller.App/TabgInstaller.App.csproj
fi

exec dotnet "$DLL"
