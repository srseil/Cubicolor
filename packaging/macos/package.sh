#!/bin/bash
# Builds Cubicolor.app: NativeAOT for arm64 and x86-64, joined into one universal executable, with the assets
# in Contents/Resources. Signed ad hoc unless SIGN_IDENTITY names a Developer ID; notarizing is a separate step.
#
#   packaging/macos/package.sh [output-dir]      (default: artifacts/macos)
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
out="${1:-$root/artifacts/macos}"
work="$out/build"
app="$out/Cubicolor.app"

rm -rf "$work" "$app"
for rid in osx-arm64 osx-x64; do
    dotnet publish "$root/src/Cubicolor" -c Release -r "$rid" -p:PublishAot=true -o "$work/$rid"
done

mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
lipo -create "$work/osx-arm64/Cubicolor" "$work/osx-x64/Cubicolor" -output "$app/Contents/MacOS/Cubicolor"
cp -R "$work/osx-arm64/assets" "$app/Contents/Resources/assets"
cp "$root/packaging/macos/Info.plist" "$app/Contents/Info.plist"
if [ -f "$root/packaging/macos/Cubicolor.icns" ]; then cp "$root/packaging/macos/Cubicolor.icns" "$app/Contents/Resources/"; fi
if [ -f "$root/packaging/steam/libsteam_api.dylib" ]; then cp "$root/packaging/steam/libsteam_api.dylib" "$app/Contents/MacOS/"; fi

codesign --force --deep --options runtime --sign "${SIGN_IDENTITY:--}" "$app"
lipo -info "$app/Contents/MacOS/Cubicolor"
echo "Built $app"
