#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

cd "$ROOT"
DLL="$ROOT/TabgInstaller.LinuxGui/bin/Debug/net8.0/TabgInstaller.LinuxGui.dll"

if [[ ! -f "$DLL" ]]; then
  dotnet build TabgInstaller.LinuxGui/TabgInstaller.LinuxGui.csproj
fi

exec dotnet "$DLL"
