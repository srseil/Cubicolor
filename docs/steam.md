# Releasing on Steam

App 454190. What is known, how a build is uploaded, and what is left to do in Steamworks.

## What is known

Checked 2026-11-22 against Valve's public docs, SDK 1.65's headers, the app's public configuration
(api.steamcmd.net) and this Mac's Steam client.

- **Depots today** (public build 2752730, 2018): 454191 Windows 32-bit, 454192 Windows 64-bit, 454193 macOS,
  454194 Linux 32-bit, 454195 Linux 64-bit.
- **Launch options today:** macOS `Cubicolor.app/Contents/MacOS/Cubicolor`, Linux `Cubicolor`, Windows
  `cubicolor.win64.exe` and `cubicolor.win32.exe`, plus a leftover Linux one on the branch `test`. Launch options
  belong to the app, not to a build, so players on an old build on a beta branch start through the same ones. The
  new macOS and Linux builds fit them as they are.
- **Achievements** COMPLETE_NORMAL, COMPLETE_SMART and COMPLETE_GENIUS exist under exactly these names.
- **Cloud:** quota 0, one rule (`savestates/*.xml`) with a macOS override.
- **The Java version shipped its save files.** A packaged 2016 build (Dropbox, `data0/Cubicolor/Cubicolor`) has
  `savestates/savestate.xml` and `config/config.xml` beside the executable. What Steam does on update with a file
  that leaves a depot is not in Valve's docs; the depots therefore keep both files, marked `userconfig`, which the
  docs say an update can't override. Only a beta-branch update from the public build can prove it.
- **macOS signing.** Steam-installed files carry no quarantine attribute (checked on an unsigned game Steam
  installed on this Mac), so Gatekeeper doesn't look at them, and the ad hoc signature Apple silicon needs is
  enough to run. Valve's docs require notarizing for applications new since October 2019; Cubicolor is from 2016.
  Not notarized, the "App Bundles Are Notarized" box in Steamworks stays off. Distributing outside Steam would
  need a Developer ID and notarizing.
- **Old saves inside the bundle** break its seal, but not the game: started directly, as Steam's launch option
  does, and through macOS's launcher, it runs and imports them.
- **Ad hoc and hardened runtime don't mix:** macOS then refuses to load `libsteam_api.dylib`. `package.sh` uses the
  hardened runtime only with a real identity, with the two entitlements Valve names for the overlay.

## Building and uploading

The Steamworks SDK goes into `packaging/steam/sdk`, which git ignores: unpack Valve's zip there, so that
`redistributable_bin` is directly inside (1.65 now, from HoleInNone's `steamworks_sdk_165.zip`). Its macOS, Windows
and Linux libraries have every function the game calls; a test checks the one of the system it runs on.

```bash
packaging/macos/package.sh
packaging/steam/content.sh artifacts/macos/Cubicolor.app
```

Then, in `packaging/steam`, with the `steamcmd` in the SDK (`sdk/tools/ContentBuilder/builder_osx`; it asks for the password and the Steam Guard code):

```bash
steamcmd +login <account> +run_app_build "$PWD/app_454190.vdf" +quit
```

`Preview` is `1` until the manifests it writes have been read. The scripts have not been run through `steamcmd` yet.
Never commit the SDK or its libraries, and never ship `steam_appid.txt`.

**Before any build goes to `default`:** read in Steamworks which depots the build holds. Whether a build that leaves
a depot out keeps its last content is unverified, and Windows and Linux players must not lose theirs.

## Left to do in Steamworks

1. One start of the packaged game through Steam: that it is recognised, and the language and achievements work.
2. Look at the file list of the old macOS and Linux depots: are `savestates` and `config` in them, and where?
3. A private beta branch for the new build. On each system: install the public build, solve some levels, switch to
   the branch, check the progress is there, switch back.
4. Keep a branch with the 2018 build for players who want it.
5. Windows launch options, once that build's name is settled.
6. Auto-Cloud: a quota, `progress.txt` and `settings.txt` in `Cubicolor` under WinAppDataRoaming, with root
   overrides to MacAppSupport and LinuxXdgDataHome, and the old rule gone.
7. Supported languages (the ten, and Latin American Spanish, which reads the Spanish table) and controller support.
8. Decide on the 32-bit depots (454191, 454194): .NET has no 32-bit Linux.
9. Steam languages' achievement names and descriptions.
