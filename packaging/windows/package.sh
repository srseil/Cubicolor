#!/bin/bash
# Builds the Windows folder: Cubicolor.exe (NativeAOT, x64) with the assets beside it, and steam_api64.dll when
# packaging/steam has it. NativeAOT links with MSVC, so this needs Visual Studio's Build Tools with the C++
# workload (winget install Microsoft.VisualStudio.2022.BuildTools, adding Microsoft.VisualStudio.Workload.VCTools).
# Runs in Git Bash.
#
#   packaging/windows/package.sh [output-dir]      (default: artifacts/windows)
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
out="${1:-$root/artifacts/windows}"
work="$out/build"
game="$out/Cubicolor"

rm -rf "$work" "$game"
dotnet publish "$root/src/Cubicolor" -c Release -r win-x64 -p:PublishAot=true -o "$work"

mkdir -p "$game"
cp "$work/Cubicolor.exe" "$game/"
cp -R "$work/assets" "$game/assets"
if [ -f "$root/packaging/steam/steam_api64.dll" ]; then cp "$root/packaging/steam/steam_api64.dll" "$game/"; fi

ls -la "$game"
echo "Built $game"
