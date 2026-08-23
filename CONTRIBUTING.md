# Contributing

Hell yes. We want your help.

## Prerequisites

clet builds and tests with just the .NET SDK. **AOT publishing** (`make publish`) additionally needs a platform-native linker — that's where local setups go wrong.

Run this first; it will tell you what (if anything) is missing:

```sh
make doctor                          # macOS / Linux / Windows with make installed
```

No `make`? Run the script directly:

```sh
bash scripts/doctor.sh               # macOS / Linux / Git Bash / WSL
pwsh -File scripts/doctor.ps1        # Windows PowerShell
.\scripts\doctor.ps1                 # Windows PowerShell (shorthand)
```

The PowerShell and bash versions are kept in sync and check the same things.

| Platform | Required for build/test | Required for AOT publish | `make` |
|----------|-------------------------|--------------------------|--------|
| **macOS** | .NET 10 SDK (preview) | Xcode Command Line Tools (`xcode-select --install`) | included with CLT |
| **Linux** (Debian/Ubuntu) | .NET 10 SDK (preview) | `sudo apt install -y clang zlib1g-dev build-essential` | included with `build-essential` |
| **Linux** (Fedora/RHEL) | .NET 10 SDK (preview) | `sudo dnf install -y clang zlib-devel` | `sudo dnf install -y make` |
| **Windows** | .NET 10 SDK (preview) | Visual Studio Build Tools 2022 with the **"Desktop development with C++"** workload (provides the MSVC linker, the Windows SDK, and `vswhere.exe`). Either VS 2022 with the C++ workload or the standalone Build Tools installer works. | **separate install** — see below |

.NET 10 SDK (preview): <https://dotnet.microsoft.com/download/dotnet/10.0>.
Windows Build Tools: <https://aka.ms/vs/17/release/vs_BuildTools.exe>.

### `make` on Windows

The Visual Studio C++ workload does **not** include GNU `make` (it ships MSBuild and `nmake`, neither of which understands our Makefile). The Makefile is optional — every target maps to a plain `dotnet` command (see "Quick start" below) — but if you want it, install GNU make via one of:

```pwsh
choco install make            # Chocolatey
winget install GnuWin32.Make  # winget
scoop install make            # Scoop
```

Or use **WSL** / **Git Bash**, both of which can run the Makefile directly.

### Common Windows AOT failure

If `dotnet publish ... -p:PublishAot=true` (or `make publish`) fails with `vswhere.exe is not recognized` or `MSB3073`:

1. **MSVC linker not installed** — install the C++ workload (above). `.\scripts\doctor.ps1` will say so.
2. **`vswhere.exe` not on PATH** — the linker is installed, but the AOT MSBuild target shells out via `cmd.exe`, where `vswhere.exe` isn't reachable. Doctor will pass, but publish will still fail with the same MSB3073. Two fixes:
   - Run from a **Developer Command Prompt for VS 2022** (or `Developer PowerShell for VS 2022`) — these set the right PATH.
   - Or prepend the VS Installer dir to PATH in your current session:
     ```pwsh
     $env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;" + $env:PATH
     ```

## Quick start

```sh
make doctor      # one-time: verify your toolchain (or run scripts/doctor.sh)
make build
make test        # unit + integration + smoke
```

Or, without `make`:

```sh
dotnet restore
dotnet build
dotnet run --project tests/Clet.UnitTests
dotnet run --project tests/Clet.ConfigTests
dotnet run --project tests/Clet.IntegrationTests
dotnet run --project tests/Clet.SmokeTests
```

All green? Ship it.

## How to contribute

1. **File an issue first** if the change is non-trivial. We'll align on the approach before you write code.
2. **Branch from `develop`**, not `main`. PRs target `develop`. Merging to `main` is a release.
3. **Keep PRs small.** One thing per PR. If your PR touches the spec, the decisions log, *and* the code — good, that's the doc-update gate doing its job.
4. **Tests are not optional.** New clet? Unit + integration tests. New CLI flag? CommandLineRoot tests. Bug fix? Regression test.
5. **Configuration tests live only in `Clet.ConfigTests`.** Never call `CletConfiguration.Apply/Reload`, mutate the static settings classes (`EditorSettings`, `FileAccessSettings`), or apply a `TuiConfigurationBuilder` to the static facades in the parallel test projects (`Clet.UnitTests`, `Clet.IntegrationTests`, `Clet.SmokeTests`, `Clet.UITests`). The settings facades are process-global; `Clet.ConfigTests` disables all parallelization at the assembly level to keep them deterministic.
6. **Read `CLAUDE.md`** before your first PR. It has the build commands, the doc-update gate checklist, and pointers to the spec and decisions log.

## What we're looking for

- Bug reports with `clet --version` output and reproduction steps
- New clet ideas (file an issue first)
- Test coverage improvements
- Terminal compatibility fixes (especially Windows Terminal, iTerm2, GNOME Terminal)
- Documentation fixes

## Decisions log

Non-obvious choices go in `specs/decisions.md` (append at the bottom, never edit old entries). If your PR makes a choice a future reader might want to "fix," write it down.

## Code of conduct

Be kind. Be constructive. Ship code.
