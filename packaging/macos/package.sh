#!/bin/bash
# Builds Cubicolor.app: NativeAOT for arm64 and x86-64, joined into one universal executable, with the assets
# in Contents/Resources.
#
#   [SIGN_IDENTITY=<identity>] packaging/macos/package.sh [output-dir]   (default: artifacts/macos)
#
# The Steam library comes from the Steamworks SDK in packaging/steam/sdk (docs/steam.md), or from where STEAM_SDK says;
# without one the game runs without Steam. Signed ad hoc unless SIGN_IDENTITY names a Developer ID, which
# then gets the hardened runtime notarizing wants and the entitlements Valve names for the Steam overlay and
# library. Ad hoc goes without the hardened runtime: under it, macOS refuses the library (no team to match).
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
out="${1:-$root/artifacts/macos}"
work="$out/build"
app="$out/Cubicolor.app"

sdk="${STEAM_SDK:-$root/packaging/steam/sdk}"
library="$sdk/redistributable_bin/osx/libsteam_api.dylib"
if [ ! -f "$library" ]; then
    if [ -n "${STEAM_SDK:-}" ]; then echo "No $library" >&2; exit 1; fi
    echo "No Steamworks SDK in packaging/steam/sdk: packaging without Steam" >&2
    library=""
fi

rm -rf "$work" "$app"
for rid in osx-arm64 osx-x64; do
    dotnet publish "$root/src/Cubicolor" -c Release -r "$rid" -p:PublishAot=true -o "$work/$rid"
done

mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
lipo -create "$work/osx-arm64/Cubicolor" "$work/osx-x64/Cubicolor" -output "$app/Contents/MacOS/Cubicolor"
cp -R "$work/osx-arm64/assets" "$app/Contents/Resources/assets"
cp "$root/packaging/macos/Info.plist" "$app/Contents/Info.plist"
if [ -f "$root/packaging/macos/Cubicolor.icns" ]; then cp "$root/packaging/macos/Cubicolor.icns" "$app/Contents/Resources/"; fi
if [ -n "$library" ]; then cp "$library" "$app/Contents/MacOS/"; fi

sign() { codesign --force --sign "${SIGN_IDENTITY:--}" ${SIGN_IDENTITY:+--options runtime --timestamp} "$@"; }
if [ -n "$library" ]; then sign "$app/Contents/MacOS/libsteam_api.dylib"; fi
sign ${SIGN_IDENTITY:+--entitlements "$root/packaging/macos/Cubicolor.entitlements"} "$app"
codesign --verify --deep --strict "$app"
if [ -n "$library" ]; then "$app/Contents/MacOS/Cubicolor" --check-steam; fi
lipo -info "$app/Contents/MacOS/Cubicolor"
echo "Built $app"
