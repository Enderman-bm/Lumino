# AGENTS.md — Lumino

## Build & Run

- **Target framework**: .NET 9 (`net9.0`).
- **Build from `Lumino/` subdirectory**, not the repo root: `cd Lumino && dotnet build`.
  Building the solution from root (`dotnet build Lumino.sln`) also works, but the project convention is to operate inside `Lumino/`.
- **Run**: `cd Lumino && dotnet run [--debug] [--open-vulkan-test]`.
  - `--debug` launches the standalone log viewer (`LuminoLogViewer.exe`) and enables debug mode.
  - `--open-vulkan-test` opens a non-modal Vulkan test window on startup.

## Tests

- **xUnit** test projects:
  - `MidiReader.Tests/` — tests the `MidiReader` library.
  - `Lumino.StorageTests/` — tests project storage logic.
- Run a single project: `dotnet test MidiReader.Tests` or `dotnet test Lumino.StorageTests`.
- No solution-wide test runner config; test discovery is standard xUnit + `Microsoft.NET.Test.Sdk`.

## Solution Structure

7-project .NET solution (`Lumino.sln`):

| Project | Role |
|---------|------|
| `Lumino` | Main Avalonia UI app (executable entrypoint) |
| `LuminoRenderEngine` | Vulkan rendering engine (Silk.NET.Vulkan) |
| `LuminoWaveTable` | MIDI wavetable/playback engine (winmm) |
| `MidiReader` | MIDI file read/write library |
| `ImageToMidi.Core` | Image-to-MIDI conversion library (also targets `net48` for packaging) |
| `EnderAudioAnalyzer` | Audio analysis tools (NAudio) |
| `EnderWaveTableAccessingParty` | Wavetable access helpers |
| `EnderDebugger` | Shared logging infrastructure |

- `Lumino` references all other projects except test projects.
- `LuminoRenderEngine` references `MidiReader` and `EnderDebugger`.

## Project-Specific Quirks

### Avalonia Compiled Bindings
- `AvaloniaUseCompiledBindingsByDefault` is **disabled in Debug** and **enabled in Release**.
- It is **force-disabled during publish** (`IsPublishing=true`) to avoid XAML parsing errors.
- If you see XAML binding errors in Release or publish builds, this flag is the first thing to check.

### FodyWeavers.xml (Auto-Generated)
- `FodyWeavers.xml` is **generated dynamically during build** (`GenerateFodyWeavers` target in `Lumino.csproj`).
- **Do not edit it manually** — it will be overwritten.
- Release weaves `Costura`; Debug weaves nothing.

### Unsafe Code
- `AllowUnsafeBlocks` is enabled in `Lumino`, `LuminoRenderEngine`, and `EnderAudioAnalyzer`.

### Shaders & Assets
- Shaders live in `Lumino/Shaders/` and are copied to output with `PreserveNewest`.
- `Assets/` are embedded as `AvaloniaResource`.

### Rendering Mode Persistence
- The app reads `RenderingMode` from `%APPDATA%/Lumino/graphics_settings.json` on startup.
- Values map to the `RenderingModeType` enum (Hardware vs Software/CPU Skia).

## Publish

- Single-file publish script: `publish.bat [compress] [platform]` or `publish-singlefile.ps1`.
- Defaults to `win-x64`, Release, self-contained.
- Optional UPX compression (auto-downloaded if missing).
- Publish props (`PublishSingleFile`, `SelfContained`, `PublishReadyToRun`, `EnableCompressionInSingleFile`) are gated behind `IsPublishing=true`.

## Code Style

- PascalCase for classes, methods, constants.
- `_camelCase` for private fields.
- Views use `.axaml` extension.
- XML doc comments expected on public APIs.
- MVVM: `Models/` → `ViewModels/` → `Views/`; services go in `Services/Implementation` and `Services/Interfaces`.

## Git

- **Two remotes**: `origin` → GitHub, `gitee` → Gitee.
- Commit messages are conventionally written in **Chinese**.
- No CI workflows (`.github/workflows/` does not exist).

## Stale / Misleading Files

- `CONTRIBUTING.md` mentions Rust crates and `cargo`. **This repo is C#/.NET** — that file is stale and should be ignored for build/test guidance.

## Missing Tooling

- No `.editorconfig`, `global.json`, or `Directory.Build.props` found.
- No lint/typecheck commands beyond `dotnet build`.
