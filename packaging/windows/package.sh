#!/bin/bash
# Builds the Windows folder: cubicolor.win64.exe (NativeAOT, x64) with the assets beside it. The name is the Godot
# version's, so the launch option in Steamworks fits both (docs/steam.md). NativeAOT links with MSVC, so
# this needs Visual Studio's Build Tools with the C++ workload (winget install Microsoft.VisualStudio.2022.BuildTools,
# adding Microsoft.VisualStudio.Workload.VCTools). Runs in Git Bash.
#
#   packaging/windows/package.sh [output-dir]      (default: artifacts/windows)
#
# The Steam library comes from the Steamworks SDK in packaging/steam/sdk (docs/steam.md), or from where STEAM_SDK says;
# without one the game runs without Steam.
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
out="${1:-$root/artifacts/windows}"
work="$out/build"
game="$out/Cubicolor"

sdk="${STEAM_SDK:-$root/packaging/steam/sdk}"
library="$sdk/redistributable_bin/win64/steam_api64.dll"
if [ ! -f "$library" ]; then
    if [ -n "${STEAM_SDK:-}" ]; then echo "No $library" >&2; exit 1; fi
    echo "No Steamworks SDK in packaging/steam/sdk: packaging without Steam" >&2
    library=""
fi

rm -rf "$work" "$game"
dotnet publish "$root/src/Cubicolor" -c Release -r win-x64 -p:PublishAot=true -o "$work"

mkdir -p "$game"
cp "$work/Cubicolor.exe" "$game/cubicolor.win64.exe"
cp -R "$work/assets" "$game/assets"
if [ -n "$library" ]; then
    cp "$library" "$game/"
    "$game/cubicolor.win64.exe" --check-steam
fi

ls -la "$game"
echo "Built $game"
