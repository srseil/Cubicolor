#!/bin/bash
# Lays out what the depot scripts upload, in artifacts/steam/content: each packaged platform's folder and the earlier
# versions' save files. Uploads nothing; docs/steam.md has the steamcmd command.
#
#   packaging/steam/content.sh <package>...
#
# Each package is one platform's, as its packaging script left it: artifacts/macos/Cubicolor.app, or the Windows
# folder artifacts/windows/Cubicolor. The platforms are built on different machines, so a Windows folder can be
# copied to the Mac (or the other way round) and both given here. A Steam library is checked by running the game only
# on its own system.
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
content="$root/artifacts/steam/content"
[ $# -gt 0 ] || { echo "usage: $0 <Cubicolor.app | Windows folder>..." >&2; exit 2; }

case "$(uname -s)" in
    Darwin) system=macos ;;
    MINGW* | MSYS* | CYGWIN*) system=windows ;;
    *) system=linux ;;
esac

rm -rf "$content"
mkdir -p "$content" "$root/artifacts/steam/output"
for package in "$@"; do
    if [ -f "$package/Contents/MacOS/Cubicolor" ]; then
        platform=macos library="$package/Contents/MacOS/libsteam_api.dylib" game="$package/Contents/MacOS/Cubicolor"
        target="$content/macos/Cubicolor.app"
    elif [ -f "$package/cubicolor.win64.exe" ]; then
        platform=windows library="$package/steam_api64.dll" game="$package/cubicolor.win64.exe"
        target="$content/windows"
    else
        echo "$package is neither a Cubicolor.app nor a Windows folder with cubicolor.win64.exe" >&2
        exit 1
    fi
    if [ ! -f "$library" ]; then
        echo "$package has no Steam library: package it with the SDK in packaging/steam/sdk" >&2
        exit 1
    fi
    if [ "$platform" = "$system" ]; then "$game" --check-steam; else echo "The $platform Steam library is checked on $platform only"; fi
    if find "$package" -name steam_appid.txt | grep -q .; then
        echo "steam_appid.txt must not ship" >&2
        exit 1
    fi
    mkdir -p "$(dirname "$target")"
    cp -R "$package" "$target"
done
cp -R "$root/packaging/steam/legacy" "$content/legacy"
find "$content" -name .DS_Store -delete
du -sh "$content"/*
