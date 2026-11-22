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
- **The old versions shipped the files they save to**, without a flag to protect them: the macOS and Linux depots
  `savestates/savestate.xml` and `config/config.xml` beside the executable, the Windows ones `project.godot`.
  `packaging/steam/legacy` has the same three, byte for byte (a test compares the checksums). What Steam does on update with a file
  that leaves a depot is not in Valve's docs; the depots therefore keep them, marked `userconfig`, which the
  docs say an update can't override. Only a beta-branch update from the public build can prove it.
- **The update keeps the saves, on macOS** (2026-11-22): from the 2016 build with three levels solved and the music at
  40, switching to the beta branch left both files byte for byte as they were, and the new version started from Steam
  with the levels and the volumes, and left the files alone.
- **Going back to build 2752730 resets the old saves, on macOS** (same day): Steam put its own copies of both files over
  the player's, which the 2018 build doesn't flag. The new version's progress lies elsewhere and is safe, but whoever was
  updated and hasn't started the new version yet would lose everything. **So `default` is never rolled back to 2752730,
  and that build is never offered as a branch.** `packaging/steam/old_454190.vdf` is the same content with the files
  flagged, for both. Tested on macOS: from the new version to this copy, Steam left both files alone. Windows and Linux
  still need theirs.
- **Without a ticked box for 64-bit macOS, Steam doesn't install the game on a current macOS.** The one for Intel is
  ticked since 2026-11-22, which holds for the old version too (it runs under Rosetta).
- **Steam starts the universal app as x86-64** on Apple silicon, under Rosetta, though Steam itself runs natively;
  presumably because only the Intel box is ticked. Rosetta isn't on every such Mac, so this must be right by the release.
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

Then, in `packaging/steam`, with the SDK's `steamcmd`, which needs `chmod +x` once for it and `steamcmd.sh` (it asks for
the password and the Steam Guard code):

```bash
sdk/tools/ContentBuilder/builder_osx/steamcmd.sh +login <account> +run_app_build "$PWD/app_454190.vdf" +quit
```

`Preview` is `1` until the manifests it writes have been read. The macOS one ran on 2026-11-22: the old save files are
in it, where they were, flagged `userconfig`, and unchanged from the live build.
Never commit the SDK or its libraries, and never ship `steam_appid.txt`.

**Before any build goes to `default`:** read in Steamworks which depots the build holds. Whether a build that leaves
a depot out keeps its last content is unverified, and Windows and Linux players must not lose theirs.

## Left to do in Steamworks

Menu names are from Valve's docs (2026-11-22). Settings go live for everyone, on the old build too, as soon as they are
published; builds are separate, and only go live where they are set live.

### Now

1. An account for uploads with "Edit App Metadata" and "Publish App Changes To Steam", and Steam Guard. Valve
   suggests one of its own with only these.
2. Log in with `steamcmd` once; then the preview build can be run and its manifest read.

### For the beta test

3. On the Builds page, "Create new app branch", twice: one for the new build, with a password, and one for the
   protected copy of the 2018 build.
4. Upload (`Preview` `0`), set the build live on the new branch, and check which depots it holds.
5. Done on macOS (2026-11-22), with the protected copy on the way back; the overlay works. On each other system: install the public build, solve some levels, switch to the branch, check the progress and settings,
   play, switch back. Start it from the library; look at the overlay and at an achievement being granted.
6. Steam Cloud, with "Enable cloud support for developers only" first: a quota and a number of files; `progress.txt`
   and `settings.txt` in `Cubicolor` under WinAppDataRoaming, cross-platform, with root overrides to MacAppSupport and
   LinuxXdgDataHome; the old `savestates` rule gone. `log.txt` is not synced.

### For the release

7. Base languages on the Manage Depots page: the ten, and Latin American Spanish, which reads the Spanish table.
   The store page's languages and texts are set apart from them, and the achievements' under Achievement Localization.
8. Launch options: the one on the branch `test` gone. With the Windows executable named `cubicolor.win64.exe` the others
   fit both the old and the new build.
9. The 32-bit depots (454191, 454194): .NET has no 32-bit Linux. Decide what those players get.
10. Controller support, system requirements, store art, trailer, developer name and website on the store page.
11. Supported operating systems, in the application's general settings: the box for Apple silicon binaries on macOS, with
    the release and not before, since the old version has none. Then the log must say "running as Arm64", and the old
    version on its branch must still start.
12. Set the build live on `default`: only by hand, confirmed by the Steam app or a text message. Then the announcement
    and an Update Visibility Round.
