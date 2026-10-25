# Cubicolor in C#: the port plan

**Status: accepted in outline, 2026-10-23.** §9 records the decisions and what is still open; §10 says
where the work stands.

## Scope

Cubicolor moves to C# / .NET with no third-party dependencies. It runs natively on:
- macOS: AppKit and Metal
- Windows: Win32 and Direct3D 11
- Linux: X11 and Vulkan

Steamworks is the one native library it ships, called through a thin P/Invoke wrapper.

**What must stay:**
- the game mechanics
- the 48 level layouts
- the general art style
- every player's progress

Everything else may change wherever the clean version is better than what exists today. The reference
is the Godot version (`godot` branch). Where it is buggy or hacky, we fix it rather than copy it.

**Branches:**
- `master`: the C# port
- `libgdx`: the Java version (v1.1)
- `godot`: the Godot version (v1.2)

---

## 1. What exists today

| | Java / libGDX v1.1 (`libgdx`) | Godot 3.0.2, v1.2 (`godot`) |
|---|---|---|
| Released | 2016-03-18 | 2018-05-07 (Steam build 2752730) |
| Shipped on Steam for | **macOS and Linux, still today** | **Windows only** (32 and 64 bit) |
| Progress | `savestates/savestate.xml` in the install dir | `progress.bin` in Godot's user dir |
| Settings | `config/config.xml` in the install dir | written to `project.godot` in the working dir, **never read back** |

**How the platforms were established.** `export_presets.cfg` has Linux and Windows presets but no macOS
one. The public Steam depots (steamcmd.net, 2026-10-23) are:
- Windows (454191, 454192): about 225 MB each. That is the Godot export: a 20–25 MB exe plus a 204 MB
  pck.
- macOS (454193): 82 MB, launched as `Cubicolor.app`.
- Linux (454194, 454195): 93–96 MB, launched as `Cubicolor`.

The Mac and Linux depots are the Java build with a bundled JRE. Stefan confirmed that the Godot version
went out for Windows only.

**Steam Cloud today.** It is configured for `savestates/*.xml` in the game install, with a macOS override
to `Cubicolor.app/Contents/MacOS/savestates`. The quota is 0 and the cloud UI is hidden, so it has
probably never stored anything (unverified).

## 2. Save compatibility (the one hard requirement)

### 2.1 What we read

The facts in the Godot rows come from reading Godot 3.0.2-stable's source. The shipped exe reports
`3.0.2.stable.custom_build`.

| Source | Where | Format |
|---|---|---|
| Godot progress | Windows: `%APPDATA%\Godot\app_userdata\Cubicolor\progress.bin`. Linux: `$XDG_DATA_HOME` (or `~/.local/share`) `/godot/app_userdata/Cubicolor/progress.bin` | 3 bytes: normal, smart, genius. Each is the number of levels solved; 255 means locked (-1) |
| Godot settings | `project.godot` in the working directory, which is the install dir under Steam | Godot `ConfigFile` text. Keys: `application/config/{dark_background,hide_ui,key_*}`, `audio/{music,sound}_{volume,muted}`, `display/window/size/{width,height,fullscreen}`, `display/window/vsync/use_vsync`. Keys are Godot 3 scancodes (`16777232` = Up) |
| Java progress | `<install>/savestates/savestate.xml`. On macOS: `Cubicolor.app/Contents/MacOS/savestates/` (per the Steam Cloud override; to verify on a real install) | XML, `<normal><level1>true</level1>…`. Fields may be missing |
| Java settings | `<install>/config/config.xml`. macOS location to verify | XML. Keys are libGDX `Input.Keys` codes (19–22 arrows, 67 Backspace, 131 Escape). Volumes 0–100 |

Two quirks in the old saves:
- **The Godot settings were never persisted.** In pck mode, `ProjectSettings.save()` writes
  `project.godot` relative to the working directory. Startup only reads the copy inside the pck plus
  `override.cfg`. So every Windows player's settings reset on each launch, but their last choices still
  sit in the install dir. We read them once, as the best guess of what the player wanted.
- **The Godot version never migrated Java saves.** A Windows player from 2016 may have both a
  `savestate.xml` and a `progress.bin`.

### 2.2 The rules

- Progress only ever grows, so merging is safe. **On every start, each difficulty's progress becomes
  the maximum over the new save and every legacy source found.** Java's per-level booleans count as the
  length of the solved run from level 1, because levels unlock in order.
- **Unlocking:** finishing level 16 of one difficulty unlocks level 1 of the next, as in both old
  versions.
- **Settings are imported once,** when no new settings file exists yet. The newest legacy source by
  mtime wins. Keys go through mapping tables for libGDX and Godot codes. A value we can't map falls back
  to its default.
- **Legacy files are never modified or deleted,** so a player can switch to an old build on a Steam beta
  branch and back without losing anything.
- **The new save goes to the OS's own place:**
  - macOS: `~/Library/Application Support/Cubicolor/`
  - Windows: `%APPDATA%\Cubicolor\`
  - Linux: `$XDG_DATA_HOME/Cubicolor/`

  It is a small versioned text file, written atomically (temp file plus rename).
- **Tested with fixtures:** real files from the Dropbox copies (`data0/…/savestate.xml`,
  `config.xml`, `data9/godot/cubicolor/progress.bin`) and hand-made edge cases (missing fields, 255
  bytes, a truncated file, both formats present).
- **Tested for real** on each OS before release: install the current public build, play a few levels,
  switch to the new build on a beta branch, check that progress is there, then switch back.
- **Risk: Steam deleting the Java saves on update.** The Java game could only save if `savestates/` and
  `config/` already existed in its working directory. Its default `savestate.xml` (all levels unsolved)
  and `config.xml` were in the repository's assets, so they almost certainly shipped in the Mac and Linux
  depots. When a file from the old manifest is missing in the new one, Steam removes it on update (from
  memory, to verify). That would delete the player's progress before the new game could read it.
  - **Mitigation:** the new Mac and Linux depots keep both files at their old paths, marked `userconfig`
    in the depot build script, so Steam installs them only where missing and never overwrites or
    removes them (from memory, to verify against the Steamworks depot docs).
  - **Inside `Cubicolor.app` on macOS,** extra files after signing break the bundle's seal. So the
    files must either survive beside a re-signed bundle, or the new `.app` keeps them outside the sealed
    part.
  - **To check in Steamworks:** the old depots' file lists.
  - **To prove:** the beta-branch test.
- **Steam Cloud is turned on properly** for the new save file:
  - Steam Auto-Cloud with one root per OS, matching the paths above.
  - The old `savestates` rule is removed.
  - Set in Steamworks by Stefan. The game itself needs no Cloud code with Auto-Cloud.

## 3. Architecture

This follows Solitude's `docs/ARCHITECTURE_PRINCIPLES.md`: strict downward layering, explicit state,
manual wiring, fail fast for bugs, and validate at boundaries.

```
src/Cubicolor.Simulation   engine-free: level format, rules, puzzle state, progress, settings, legacy import
src/Cubicolor              the game: platform layers, renderers, audio, text, Steam, screens, presentation
tests/Cubicolor.Tests
tools/                     development tools (sound lab, store-art renders)
```

- **The rules are pure and instant.** A move is a function from state to state: position, carried
  colour, tiles alive, requirements left. That makes the puzzle unit-testable. A solver test also proves
  all 48 levels are solvable and records each one's path length.
- **Presentation is separate.** The animations (tile die and revive waves, the requirement stack
  falling, the exit swirl, the cube colouring) are timelines driven from the state transitions. Game
  logic never waits on an animation-finished callback. This removes the class of bug in Godot's
  `todo.txt` (a requirement sometimes not fading back in on reset) and allows input buffering
  ("allow the next move shortly before the animation ends", also from `todo.txt`).
- **Fixed-step clock and headless mode,** as in Solitude: scripted runs, `get`/`expect` on state, and
  screenshots. Platform code stays out of headless runs.
- **Platform layer** behind narrow interfaces (`IWindow`, `IInput`, `IRenderer`, `IAudioOut`,
  `IGlyphRasterizer`):

| | macOS | Windows | Linux |
|---|---|---|---|
| Window and events | AppKit through the ObjC bridge (from Solitude) | Win32 | X11 (`libX11`; also works under XWayland and Gamescope) |
| Graphics | Metal, MSL | Direct3D 11, HLSL | Vulkan, GLSL compiled to SPIR-V at build time (Vulkan SDK) |
| Audio | Core Audio (AudioUnit) | WASAPI | ALSA (`libasound.so.2`) |
| Gamepad | GameController | XInput | evdev |
| Glyphs | Core Text and Core Graphics (from Solitude) | DirectWrite | FreeType (`libfreetype.so.6`) |

- **Rendering is deliberately small:**
  - an orthographic camera and one directional light
  - procedural meshes and surface patterns (§4)
  - alpha fades and 4× MSAA
  - a 2D overlay for the UI and text

  Three hand-written shader sets.
- **Text:** each OS's own rasterizer draws grayscale glyph coverage into an atlas at the exact pixel
  size and display scale. Layout and drawing are ours and shared. ClearType-style subpixel rendering is
  deliberately left out (§9.2).
- **Steam:** P/Invoke over the Steamworks flat API (`SteamAPI_Init`, user stats and achievements
  `COMPLETE_NORMAL`, `COMPLETE_SMART`, `COMPLETE_GENIUS`, `RunCallbacks`). The game runs without Steam
  too.
- **Builds:** .NET 10 NativeAOT, one self-contained binary per OS.
  - macOS ships as a universal arm64 and x64 `.app`, signed and notarized. It is built on this Mac.
  - Windows and Linux ship x64; 32-bit is dropped.
  - NativeAOT cannot cross-compile between operating systems, so Windows and Linux are built on
    Stefan's dual-boot laptop. GitHub Actions can be added for CI later.

## 4. Assets: procedural where the shape is simple

The art is simple enough that most of it can be generated in code, at whatever resolution the screen
needs. That leaves no asset pipeline for meshes, textures or sounds.

| Asset | Today | Plan |
|---|---|---|
| Levels | 48 × `.bin` | Kept byte for byte. The format is documented in `Cubicolor.Simulation` |
| Player cube | `cube_bevel.obj`: unit cube, about 0.1 edge rounding, 576 vertices | **Generated:** a rounded box with the radius and segment count as parameters. Smooth normals end the triangle seams noted in `todo.txt` |
| Tiles | `ground_bevel.obj`: 1 × 0.5 × 1, about 0.03–0.04 rounding | **Generated,** same generator |
| Key tile "+" | `tile.png`, 1536² | **Shader:** a rounded-plus signed distance field. Sharp at any size |
| Exit top | `exit_tile.png`, 1536² | **Shader:** a rounded-square signed distance field |
| Colouring effect | 30 mask frames at 2048² on a cube-net UV layout, stepped at 33 fps over 0.87 s. Paint rises up the sides in rounded "fingers", then closes over the top from the edges inward | **Shader, in object space:** one value per fragment, its distance along the surface from the bottom edge, offset by a smooth per-side finger profile and thresholded by time. Smooth instead of stepped, and needs no UV net. Matched against the old frames side by side |
| Fonts | Vollkorn (OFL) | Kept |
| Sounds | 6 OGG, edited from Freesound recordings | **Synthesized at startup** (§4.1). The old ones are the reference |
| Music | "Relaxing Piano Music" by Kevin MacLeod (CC BY 3.0), 24.8 min | **Ogg Vorbis** at quality 5, 25.6 MB, decoded by our own reader. Attribution stays in the credits |
| GUI images | arrows, checkboxes | Drawn as vector shapes in the UI |
| Logo | `logo.png` 900×390, sources in `data9/BlenderNLE/*.xcf` | Set in Vollkorn at runtime if that is all it is; otherwise re-exported from the source |

The material colours and lighting come from the Godot materials (`materials/*.tres`, the palette in
`global.gd`). Their parameters move into code with the shaders.

The animation timings (41 Godot clips in `player.tscn`, `tile.tscn` and `level.tscn`) are read off once
and written as timing and easing tables in C#.

### 4.1 Sound effects

Synthesizing the sounds also fixes a licensing gap. The shipped effects were edited from these Freesound
recordings, in `data6/MyGame/sounds/*.aup`. Licences are as listed on freesound.org on 2026-10-23:

| Freesound recording | Licence |
|---|---|
| 128918 click | CC0 |
| 319217 sticks | CC0 |
| 32510 stick | CC0 |
| 235555 water | CC0 |
| 54405 button click | CC0 |
| **328046 whooshing stick** | **CC BY 4.0** |
| **706 pop** | **CC BY-NC 4.0** |

The credits name neither of the last two. A non-commercial licence doesn't fit a paid game. The licence
an old upload carried in 2016 may have differed (unverified), and which effect uses which recording has
not been traced yet.

| Sound | Length | Synthesis idea |
|---|---|---|
| button click | 0.24 s | short filtered noise burst plus a fast-decaying partial |
| player step | 0.50 s | modal "wood knock": a few decaying inharmonic partials |
| coloring and decoloring | 0.76 s | rising (or falling) band-passed noise sweep with a soft body |
| reviving | 0.43 s | soft pop: a pitched sine drop with a quick envelope |
| level solve | 1.60 s | a small bell or piano-like chord, additive partials with long decays |

- **How they are made:** a small `tools/SoundLab` renders each preset to WAV next to the original, with
  spectrograms side by side, for tuning.
- **Who judges:** only listening decides "close enough", so Stefan judges by ear.
- **In the game:** the same code generates the sounds into memory at startup, in a few milliseconds.
- **Fallback:** if a sound can't be matched well enough, it is re-recorded or replaced by a CC0 source.

## 5. Fixes planned along the way

- **Camera:** fit the orthographic view to the board and requirement stack, centred. This replaces
  `distance -= requirements.size() / (a * 5.0)`.
- **Settings and keys actually persist.** Remove the debug keys: Q quits, Space pauses the tree,
  PageUp/PageDown switch levels.
- **Display:** resizable window plus native fullscreen, HiDPI throughout. This replaces the fixed
  800×600 to 4K list. Old resolution settings become the starting window size.
- **One source of truth for progress** (today it lives in both `progress.bin` and ProjectSettings).
- **Input:** gamepad menus work (the Godot OptionButton didn't), with rebindable keys and pad buttons.
- **Tutorial overlays** laid out by the UI rather than by `get_viewport().size * 4/3` arithmetic.
- **Sound licensing** settled (§4.1).

## 6. Steam store refresh

Current required sizes (partner.steamgames.com/doc/store/assets, fetched 2026-10-23):

| Asset | Size |
|---|---|
| Header capsule | 920×430 |
| Small capsule | 462×174 |
| Main capsule | 1232×706 |
| Vertical capsule | 748×896 |
| Screenshots | ≥ 1920×1080, 16:9 |
| Page background (optional) | 1438×810 |
| Library capsule | 600×900 |
| Library header | 920×430 |
| Library hero | 3840×1240 |
| Library logo | ≤ 1280 wide / 720 tall |
| Shortcut icon | 256×256 |
| App icon | 184×184 |
| Event cover | 800×450 |

The old capsules (467×181 and smaller) can't be reused. With procedural art, the game itself can render
everything at any size:
- Key art rendered headless at the needed resolution, with a transparent background where needed.
- Screenshots re-captured at 1920×1080 and 4K.
- A new trailer recorded in-game.
- The system requirements text updated (it still reads Vista and OpenGL ES 3.0).

Decided 2026-10-25 (Stefan): the wide capsules (header, small, main, library header) and the library logo stay
the 2016 design, the title over the logo's row of tiles. The tall capsules have the title above a level, the
event cover the title on the left and a level on the right; the library hero is a level with its left kept free
for the logo.

## 7. Order of work

1. **Branches:** `libgdx` holds the Java code, with a README note like `godot`'s. `master` is emptied
   for the scaffold.
2. **Scaffold:** solution, `Directory.Build.props`, CLAUDE.md, docs, tests running.
3. **Simulation:**
   - level loader, rules, the solver test over all 48 levels
   - progress and settings models
   - legacy importers with fixtures
4. **macOS vertical slice:**
   - window, Metal, procedural meshes
   - one level playable
   - headless mode and screenshots
5. **The whole game on macOS:**
   - menus and text through Core Text
   - audio and music
   - all animations, the colouring shader, the tutorial
   - settings and key rebinding, gamepad
6. **Sound synthesis** (§4.1), with Stefan listening. This can run alongside step 5.
7. **Windows**, on the laptop: Win32, D3D11, DirectWrite, WASAPI, XInput.
8. **Linux**, on the laptop: X11, Vulkan, FreeType, ALSA, evdev.
9. **Steam:**
   - achievements and Auto-Cloud
   - depots and build scripts, which don't exist today
   - the beta-branch migration test on all three OSes (§2.2)
10. **Store assets and release.**

## 8. Reused from Solitude

- the ObjC bridge, AppKit window and input, Metal wrappers, GameController
- Core Text glyphs
- `Assert`, `Log`, the clock
- the headless command and script runner, and `tools/img.py`

Each piece is copied in and trimmed to what Cubicolor needs, not shared as a library: Solitude is
macOS-only and moves on its own.

## 9. Decisions and open points

1. **Branches** (decided 2026-10-23): the C# port on `master`, the Java version on `libgdx`.
2. **Glyphs from each OS's own rasterizer** (decided 2026-10-23): Core Text, DirectWrite, FreeType.
   - All three are reliably present. Core Text and DirectWrite ship with every supported macOS and
     Windows. FreeType is on every desktop Linux and in the Steam Linux Runtime (from memory, to
     verify on the laptop).
   - ClearType-style subpixel text is not planned on any platform. It needs to know the colour under
     each glyph and the screen's subpixel layout, and it breaks under alpha fades, scaling and
     transforms. Games (and much of Windows itself) draw grayscale text for that reason. HiDPI screens
     make the difference small anyway.
   - With a native rasterizer the door stays open if it is ever wanted.
3. **Music as Ogg Vorbis** (decided 2026-10-25; QOA from 2026-10-23 until then).
   - QOA was simple to decode but took 53 MB for the 24.8 minutes. Vorbis at quality 5 (about 138 kbit/s,
     libvorbis's `oggenc`) is 25.6 MB, the same as the Godot version shipped, and sounds better than QOA's
     3.2 bits a sample. Quality 4 would be 19.5 MB.
   - The decoder is our own (`Audio/Vorbis.cs`, `Ogg.cs`, `Mdct.cs`), written from the Vorbis I
     specification: floor 1 only, which is all libvorbis has written since 2002. It matches ffmpeg's decode
     within one 16-bit step and reads the whole track about 300 times faster than it plays.
   - libvorbis is needed only to encode, at build time (`brew install vorbis-tools`).
4. **Vulkan SDK at build time** (decided 2026-10-23).
5. **Order: macOS first.** Windows and Linux follow on Stefan's dual-boot laptop. There is no Steam
   Deck. GitHub Actions comes later.
6. **Steam Cloud on**, through Auto-Cloud (decided 2026-10-23).
7. **Open: pushing the branch change.** It rewrites what `master` shows on GitHub, so it waits for
   Stefan's go-ahead.
8. **Open: which shipped sound came from which recording** (§4.1). Only matters if synthesis falls short.
9. **Ten languages** (decided 2026-10-25): English, French, German, Spanish, Simplified Chinese, Russian,
   Brazilian Portuguese, Japanese, Korean and Polish, the languages most of Steam's players read.
   - The English text is the key. `assets/lang/<code>.txt` maps it to each translation, and widgets keep
     the English as their id, so focus and scripts work the same in every language.
   - The language is the player's choice in Settings. Otherwise it is Steam's game language, then the
     system's first supported language, then English. A choice is saved as `language = <code>`; older
     settings files simply have none.
   - Text longer than its box is set smaller until it fits.
   - Chinese, Japanese and Korean come from Noto Serif CJK Medium, shipped beside Vollkorn and trimmed to
     the characters the tables use (`tools/subset-fonts.sh`, about 150 KB for all three), so every
     platform draws them alike. The language's own font comes first, so shared Han characters take its
     forms. `LanguageTests` fails when a table uses a character the trimmed fonts lack.
   - The translations are machine-drafted and need a native speaker's pass. The Steam store page and the
     languages listed in Steamworks are to be translated and set with them.

## 10. Where it stands

- 2026-10-23: **macOS playable end to end, headless-verified.**
  - Done:
    - the rules, all 48 levels (solved by search, solutions recorded and replayed)
    - progress and settings with the import of both earlier versions' saves
    - the Metal renderer with procedural meshes and patterns
    - every animation, the menus, the level screen, Core Text
    - synthesized sound effects, the music, Core Audio
    - Steam achievements
    - a universal NativeAOT `.app`
  - All 48 levels play through the real game loop headless.
  - Not yet checked by a person: the game in a window (input feel, fullscreen, controller, sound). Visual
    checks so far are headless screenshots compared against the 2016 screenshots.
  - First drafts of every Steam store and library image, the icon and screenshots: `--art <dir>` renders them
    at their exact sizes from the game itself.
  - Open: how the 26 MB music file is versioned; the Steamworks SDK libraries for packaging; Stefan's ear on
    the synthesized sounds.
- 2026-10-25: **ten languages**, chosen in Settings or followed from Steam or the system (§9.9). Every screen
  checked headless in each. Open: native-speaker review, the store page.
