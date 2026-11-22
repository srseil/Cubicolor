#!/bin/bash
# Lays out what the depot scripts upload, in artifacts/steam/content: each platform's folder and the Java version's
# save files. Uploads nothing; docs/steam.md has the steamcmd command.
#
#   packaging/steam/content.sh <Cubicolor.app>
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
content="$root/artifacts/steam/content"
app="${1:?the packaged Cubicolor.app}"

if [ ! -f "$app/Contents/MacOS/libsteam_api.dylib" ]; then
    echo "$app has no Steam library: package it with the SDK in packaging/steam/sdk" >&2
    exit 1
fi
"$app/Contents/MacOS/Cubicolor" --check-steam
if find "$app" -name steam_appid.txt | grep -q .; then
    echo "steam_appid.txt must not ship" >&2
    exit 1
fi

rm -rf "$content"
mkdir -p "$content/macos" "$root/artifacts/steam/output"
cp -R "$app" "$content/macos/Cubicolor.app"
cp -R "$root/packaging/steam/legacy" "$content/legacy"
find "$content" -name .DS_Store -delete
du -sh "$content"/*
