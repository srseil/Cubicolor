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
  - `Audio/`: mixer, QOA codec, the synthesized sound effects (`Synth`).
  - `Platform/`: `Mac/` (AppKit, Core Text, Core Audio, GameController), `Metal/`, `Steam.cs`. Windows and Linux
    come later beside them.
  - `Shaders/`: per-backend shader source, embedded in the assembly.
  - `App/`: the frame step shared by windowed and headless runs, scripted input and the script runner.
- `tests/Cubicolor.Tests`: xUnit v3 (a test-only package; the game itself has no package references).
- `assets/`: data the game loads (levels, fonts, music), each with its licence beside it.
  `assets/music/soundtrack.qoa` is not in git yet (53 MB); `tools/SoundLab encode-music` makes it from the
  original WAV in Dropbox (`Projekte/Cubicolor/data6/MyGame/wav/soundtrack.wav`).
- `tools/SoundLab`: measures sounds, renders the synthesized effects, encodes the music.
- `packaging/macos/package.sh`: the universal `Cubicolor.app` (NativeAOT, arm64 + x86-64).

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
- Headless runs use a fresh temporary data directory unless `--data-dir` is given, and never read the
  player's saves. `--capture-audio <wav>` writes what the run played. `MTL_DEBUG_LAYER=1` turns on Metal's
  validation.
- All 48 levels through the real game (about a minute):
  `awk '{n=$1; sub(/\.bin/,"",n); printf "play %s\nwait ready\nmoves %s\nwait 1.2\nexpect state completed\n", n, $2}' tests/Cubicolor.Tests/Fixtures/solutions.txt > scratch/all.txt`,
  then `--headless --size 640x360 --timeout 3000 --script scratch/all.txt`.

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
