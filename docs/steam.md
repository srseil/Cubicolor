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
- **The packaged game talks to Steam** (2026-11-22, started with `SteamAppId=454190` in the environment, on an
  account that owns it): it is recognised, and reads the language and the three achievements. Setting an
  achievement hasn't been tried.
- **Steam reports `english`** there. English is the only language Steamworks lists for the game, and Steam's answer
  comes before the system's, so the ten languages must be listed by the time the update goes out.
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

Menu names are from Valve's docs (2026-11-22). Settings go live for everyone, on the old build too, as soon as they are
published; builds are separate, and only go live where they are set live.

### Now

1. Look at the manifests of the old macOS and Linux depots (Builds page, public build 2752730): are `savestates` and
   `config` in them, and where?
2. An account for uploads with "Edit App Metadata" and "Publish App Changes To Steam", and Steam Guard. Valve
   suggests one of its own with only these.
3. Log in with `steamcmd` once; then the preview build can be run and its manifest read.

### For the beta test

4. On the Builds page, "Create new app branch", twice: one for the new build, with a password, and one for the 2018
   build, set live there (by then the 2018 build may no longer be among the 50 it lists).
5. Upload (`Preview` `0`), set the build live on the new branch, and check which depots it holds.
6. On each system: install the public build, solve some levels, switch to the branch, check the progress and settings,
   play, switch back. Start it from the library; look at the overlay and at an achievement being granted.
7. Steam Cloud, with "Enable cloud support for developers only" first: a quota and a number of files; `progress.txt`
   and `settings.txt` in `Cubicolor` under WinAppDataRoaming, cross-platform, with root overrides to MacAppSupport and
   LinuxXdgDataHome; the old `savestates` rule gone. `log.txt` is not synced.

### For the release

8. Base languages on the Manage Depots page: the ten, and Latin American Spanish, which reads the Spanish table.
   The store page's languages and texts are set apart from them, and the achievements' under Achievement Localization.
9. Launch options: the one on the branch `test` gone. With the Windows executable named `cubicolor.win64.exe` the others
   fit both the old and the new build.
10. The 32-bit depots (454191, 454194): .NET has no 32-bit Linux. Decide what those players get.
11. Controller support, system requirements, store art, trailer, developer name and website on the store page.
12. Set the build live on `default`: only by hand, confirmed by the Steam app or a text message. Then the announcement
    and an Update Visibility Round.
