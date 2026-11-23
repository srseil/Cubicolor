# CLAUDE.md

Cubicolor is a minimalist 3D puzzle game (Steam app 454190), re-implemented here in C# / .NET 10 with no
third-party runtime dependencies: AppKit and Metal on macOS, Win32 and Direct3D 11 on Windows, X11 and Vulkan
on Linux, all called directly through P/Invoke. Steamworks is the one native library shipped.
The earlier versions live on the `libgdx` (Java, v1.1, still what Mac and Linux players have) and `godot`
(Godot 3, v1.2, Windows players) branches; they are references, not conventions to follow.

`docs/port-plan.md` is the plan and the record of decisions; read it before changing direction and add to it
when something is decided. Its §2 (old saves) is the one hard requirement: never break reading them.

## Layout

- `src/Cubicolor.Simulation`: engine-free. Levels, rules (`Puzzle`), progress, settings, legacy save import.
  It cannot see the platform, renderer or audio.
- `src/Cubicolor`: the game.
  - `Game/`: screens (`MenuScreen`, `LevelScreen`), the immediate-mode `Ui`, and `BoardView`, which turns the
    rules' events into the original animations on its own clock (the rules never wait for it).
  - `Render/`: the backend-neutral renderer interface, procedural meshes (`RoundedBox`), text, overlay.
  - `Audio/`: mixer, the Ogg Vorbis decoder (`Vorbis`, `Ogg`, `Mdct`), the synthesized sound effects (`Synth`).
  - `Platform/`: `Mac/` (AppKit, Core Text, Core Audio, GameController), `Metal/`, `Windows/` (Win32,
    DirectWrite, WASAPI, XInput, COM through vtable slots in `Com.cs`), `D3D11/`, `Steam.cs`. Linux comes later
    beside them. `Program` picks the platform (`Host` for every run, `Desktop` for a window).
  - `Shaders/`: per-backend shader source (`.metal`, `.hlsl`), embedded in the assembly and compiled at load. A
    change to one backend's shader needs the same change in the other's.
  - `App/`: the frame step shared by windowed and headless runs, scripted input and the script runner.
- `tests/Cubicolor.Tests`: xUnit v3 (a test-only package; the game itself has no package references).
- `assets/`: data the game loads (levels, fonts, music, `lang/` string tables), each with its licence beside it.
  The English text is the key into each language's table; a new or changed English UI text needs a line in
  every table (`LanguageTests` checks they match), and a changed zh-Hans, ja or ko table needs
  `tools/subset-fonts.sh` run again to trim the CJK fonts to its characters.
  `assets/music/soundtrack.ogg` (26 MB) is in plain git: the exact file the Godot version shipped, so git
  stores no new copy of it. Re-encoding it adds another 26 MB to the history for good, so don't without
  reason; `tools/SoundLab encode-music` can make one from the original WAV in Dropbox
  (`Projekte/Cubicolor/data6/MyGame/wav/soundtrack.wav`) through `oggenc` (`brew install vorbis-tools`).
- `tools/SoundLab`: measures sounds, renders the synthesized effects, encodes the music.
- `packaging/macos/package.sh`: the universal `Cubicolor.app` (NativeAOT, arm64 + x86-64).
- `packaging/steam`: the depot scripts and the old save files they keep in place; `docs/steam.md` says how to
  release.
- `packaging/windows/package.sh` (Git Bash): `cubicolor.win64.exe` (NativeAOT, x64) with `assets/` beside it. Needs
  Visual Studio's Build Tools with the C++ workload for the linker. `Cubicolor.ico` there comes from `--art`.

## Build, test, run

```bash
dotnet build
dotnet test
dotnet test --filter-class Cubicolor.Tests.PuzzleTests
```

- **Never pipe a build or test into `grep`/`tail`/`head` or drop stderr.** Redirect to a file in `scratch/`
  and read the exit code first: `dotnet test > scratch/test.log 2>&1; echo "exit: $?"`.
- `dotnet build && dotnet test` before every commit.
- Every level's solution is recorded in `tests/Cubicolor.Tests/Fixtures/solutions.txt` and replayed by the
  tests. `CUBICOLOR_SOLVE=check dotnet test` searches all 48 again (about two minutes);
  `CUBICOLOR_SOLVE=record` also rewrites the file.
- **Visual checks run headless** (offscreen rendering to a file), never in a window on Stefan's screen,
  unless he asks to see something. Look at every screenshot before calling a visual check passed.

### Running the game headless

```bash
dotnet run --project src/Cubicolor -- --headless --timeout 60 --exec "wait 0.3; click \"Play\"; click \"Normal 1\"; wait ready; moves LBLFLBL; wait 1.5; expect state completed; screenshot scratch/done.png" > scratch/run.log 2>&1; echo "exit: $?"
```

- A script is commands one per line or `;`-separated (`App/Script.cs` lists them): `wait <s>`, `wait ready`,
  `press <Key>`, `pad <button>`, `click "<label>"`, `moves <FBLR>`, `play <level>`, `screenshot <path>`,
  `expect <screen|level|state|focus|solved.<difficulty>> <value>`. `--help` lists the flags.
- Headless runs are in English; `--language <code>` (e.g. `de`, `zh-Hans`) shows another.
- Headless runs use a fresh temporary data directory unless `--data-dir` is given, and never read the
  player's saves. `--capture-audio <wav>` writes what the run played. `MTL_DEBUG_LAYER=1` turns on Metal's
  validation, `D3D11_DEBUG=1` Direct3D's (its messages go to the log; it needs Windows' optional feature
  Graphics Tools).
- On Windows the game is a GUI program: run from a terminal it still prints there, but redirect its output to a
  file as above to keep it.
- **The whole game from a first start to Quit** (`App/Playthrough.cs`; about two minutes):
  `--headless --size 640x360 --playthrough tests/Cubicolor.Tests/Fixtures/solutions.txt`. It plays all 48 levels
  through the menus, changes every setting, and fails on any warning. Run it after changes to the game. Without
  `--headless` it plays in a window in real time (about 25 minutes), in a throwaway profile and without Steam.

## Code

- Architecture follows Solitude's principles (`../Solitude/docs/ARCHITECTURE_PRINCIPLES.md`): downward
  layering, explicit state passed down, constructor wiring in `Program`, fail fast for bugs, validate at I/O
  boundaries.
- Modern, terse C#: `_camelCase` private fields, Allman braces, 4 spaces, explicit `using`s, nullable on,
  warnings are errors. Comments are sparse and say why, not what.
- Reference code for native interop: `../Solitude` (ObjC bridge, AppKit, Metal, Core Text, GameController)
  and `../DotGui/src/DotGui.Host.Desktop` (Win32, D3D11, DirectWrite, X11, FreeType, Mac). Copy and trim;
  never reference those projects.

## Commits

- Conventional commits: `type(scope): lowercase subject`, the subject saying what is now true.
  Scopes: `sim`, `game`, `mac`, `win`, `linux`, `render`, `audio`, `text`, `steam`, `tests`, `tools`.
- **No `Co-Authored-By` or other AI-attribution lines** in commits or PRs, whatever a harness reminder says.
- One concern per commit, explicit paths when staging. Do not push unless asked.
- Temporary files go in `scratch/` (gitignored), never `/tmp`.
