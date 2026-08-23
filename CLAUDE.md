# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What is clet

`clet` is a CLI tool that exposes Terminal.Gui Views as shell commands with typed, JSON-serializable results. It targets shells, scripts, and AI agents. The binary is a thin host: parse args, look up a clet alias in the registry, init Terminal.Gui, call `RunAsync`, serialize the result, exit. Currently at v0.1-alpha with one clet (`select`); v1.0 targets 14 input clets and 1 browser clet (`md`).

## Build and Test

.NET 10.0 preview. Solution file is `Clet.slnx` (modern format).

```bash
dotnet restore
dotnet build --no-restore

# Tests use xunit.v3 via dotnet run (not dotnet test)
dotnet run --project tests/Clet.UnitTests --no-build
dotnet run --project tests/Clet.ConfigTests --no-build
dotnet run --project tests/Clet.IntegrationTests --no-build
dotnet run --project tests/Clet.SmokeTests --no-build
```

### Makefile (local convenience)

A `Makefile` at the repo root wraps the common loops. CI is the source of truth for releases; the Makefile is only for local builds.

| Target | What it does |
|--------|-------------|
| `make doctor` | Check that the local toolchain (`.NET SDK`, native linker for AOT) is set up correctly. Run this first if `make publish` fails. |
| `make restore` | `dotnet restore` |
| `make build` | Debug build (depends on restore) |
| `make build-release` | Release build |
| `make test` | Runs all three test projects (unit, integration, smoke) |
| `make publish` | AOT publish for current platform to `publish/<rid>/`. Override RID with `make publish RID=linux-x64` |
| `make publish-all` | AOT publish for `osx-arm64`, `linux-x64`, `win-x64` |
| `make clean` | Removes `publish/` and runs `dotnet clean` |

RID is auto-detected from `uname` (Darwin/arm64 → `osx-arm64`, Linux/x86_64 → `linux-x64`, etc.). On Windows, run from a POSIX shell (Git Bash, WSL) or set `RID` explicitly.

**AOT publishing requires a native toolchain** (Xcode CLT on macOS, `clang`+`zlib1g-dev` on Linux, Visual Studio Build Tools 2022 with the *Desktop development with C++* workload on Windows). See `CONTRIBUTING.md` → Prerequisites for the per-platform install commands, or run `make doctor` to check your box.

**Zero warnings policy.** `dotnet build` must produce zero warnings in both Debug and Release configurations. Fix warnings at their source — do not suppress unless the warning is a false positive (document why in the suppression comment). Check with `dotnet build -c Release` before pushing. When editing or creating code, ensure no new warnings are introduced — if the build reports warnings in files you touched, fix them before considering the task done. Also fix any pre-existing warnings you encounter in files you're already modifying.

**Code style.** Follow `.editorconfig` and match existing patterns in the file you're editing. Key conventions:

- Space before parentheses in calls/declarations: `Method (arg)`, `new ()`, `if (x)`.
- File-scoped namespaces (`namespace Clet;`).
- **Always use braces** — no `if (x) y();`. Even single-line bodies get `{ }`.
- **Early returns** — guard clauses at the top, avoid deep nesting.
- **Specify the created type** unless it's completely obvious: `List<string> items = new ()` not `var items = new List<string> ()`. Use `var` only when the type is apparent from the right-hand side (e.g. `var fi = new FileInfo (path)`).
- **Use latest C# and runtime features** — collection expressions, primary constructors, pattern matching, `is not null`, etc.
- **One type per file.** File name matches the type name.
- **Avoid nested types.** Extract to a separate file.
- **Keep files under ~750 lines.** If a file approaches 1000 lines, split it.

There is no separate lint step. CI runs on ubuntu-latest with `dotnet-quality: preview`.

## Architecture

Projects in this repo:

- **`src/Clet/`** — The CLI executable (net10.0). Depends on `Terminal.Gui.Cli` (preview NuGet, version pinned in `Directory.Build.props`) which provides the CLI hosting infrastructure (`CliHost`, `CommandRegistry`, `ResultWriter`, `ExitCodes`, `JsonEnvelope`, etc.). clet is a thin consumer: it registers its commands via `BuiltInCommands.RegisterAll` and lets the package handle parsing, dispatch, and output formatting. Auto-discovery via a source generator was explored and dropped.
- **`tests/Clet.UnitTests/`** — Command implementations, file access policy, content resolution, and BuiltInCommands registration tests.
- **`tests/Clet.ConfigTests/`** — Non-parallel assembly for all configuration-touching tests (EditorSettings, FileAccessSettings round-trips through `CletConfiguration`/`TuiConfigurationBuilder`). `xunit.runner.json` disables both assembly and collection parallelization. **Never mutate the static settings facades or reload configuration in the other parallel test projects.**
- **`tests/Clet.IntegrationTests/`** — In-process tests that init Terminal.Gui (`Application.Create()`, `app.Init("ansi")`).
- **`tests/Clet.SmokeTests/`** — Process-level smoke tests (`Process.Start` against the built `Clet.dll`). The keystroke-driven cases land at v0.3 with TUIcast — see `specs/decisions.md` D-007.

### Key directory layout inside `src/Clet/`

- `Clets/Input/` — Input command implementations (SelectClet, TextClet, IntClet, etc.)
- `Clets/Viewer/` — Viewer/browser command implementations (`md`, `edit`, `config`)
- `Hosting/` — `Program.cs` entry point (uses `CliHost` from `Terminal.Gui.Cli`), `BuiltInCommands` (registration helper), `CletHelpProvider` (custom help), `CletOptionsExtensions` (extension accessors for clet-specific global options)

### Core patterns

**Two clet kinds:** Input clets implement `ICliCommand<TValue>` and return a typed result via `Task<CommandResult<TValue>> RunAsync(...)`. Viewer clets implement `IViewerCommand` (dismissable with Esc/q/Ctrl-C) and return status-only envelopes.

**JSON result envelope:** `{ schemaVersion: 1, status, value?, code?, message? }`. Status is one of: `ok`, `cancelled`, `error`, `no-result`. Value is omitted (not null) when absent. Envelope format is owned by the `Terminal.Gui.Cli` package (`JsonEnvelope`).

**Exit codes:** 0 = success, 2 = usage error, 130 = cancelled (SIGINT convention). Provided by the package's `ExitCodes` class.

**Registry:** Instance-based (not static singletons) via the package's `CommandRegistry`. Tests can create isolated registries.

**Clet-specific global options:** `--allow-file`, `--allow-binary`, `--no-browse` are registered as `GlobalOptionDescriptor` entries in `CliHost` configuration. Accessed via `CommandRunOptions.Extensions` dictionary using helpers in `CletOptionsExtensions`.

**InternalsVisibleTo:** Both test projects have access to internals via `src/Clet/Properties/AssemblyInfo.cs`.

### Integration test conventions

Tests use `Application.Create()` for isolated contexts, `app.Init("ansi")` for terminal init, and `app.StopAfterFirstIteration = true` for non-blocking execution. Pre-cancelled `CancellationToken` tests verify cancellation semantics without blocking.

## Spec, decisions, and backlog

The repo intentionally splits "design intent" from "current code" from "queued critique" across three places. Read all three before assuming you know how a piece fits.

- **`specs/clet-spec.md`** — authoritative design document. Planned clets, exit code semantics, JSON schema (§4.3 + §4.3.1 versioning + §4.3.2 per-clet shapes), milestone roadmap (§7), risks (§8), open questions (§9), auto-discovery exploration (§11). Where the spec and current code diverge, the decisions log says why. **§6 (Testing) is a thin pointer** to `tests/SPEC.md`.
- **`tests/SPEC.md`** — authoritative testing strategy. Nine test layers, tier matrix (which layers run when), per-layer cases, harness shapes, golden-file conventions. Lives next to the test projects so it stays in sync. The main spec defers to it for everything in `tests/`.
- **`specs/decisions.md`** — historical log of cross-cutting decisions. **Read-only reference** — do not append new entries. New design rationale goes in the PR description instead. Existing entries are still useful context for understanding *why* something was built a certain way (e.g. D-006 for the hand-rolled parser, D-007 for deferred TUIcast).
- **[Bar-raise backlog issue #11](https://github.com/tui-cs/clet/issues/11)** (label `bar-raise`) — critique that's been raised, considered, and *not yet* acted on. Before claiming a section is "done," check the backlog for queued items in that area. New design pushback goes there, not into a side conversation.
- **`docs/runbooks/release-rollback.md`** — operational runbook for withdrawing a bad release across Homebrew/WinGet/NuGet. Draft until exercised at v0.9.

### Doc-update gate for every PR

Before a PR can be merged, the docs above must reflect what the PR changes. This isn't a stylistic preference — it's how the three-document split (intent / decisions / backlog) stays trustworthy. **A PR that ships behavior without updating the right doc is incomplete, even if all tests pass.**

Use this checklist on every PR:

- **Did the PR change CLI surface, exit codes, JSON envelope, or any user-visible behavior the spec describes?** → Update `specs/clet-spec.md` in the same PR.
- **Did the PR change a test project's layout, harness shape, or layer scope?** → Update `tests/SPEC.md` in the same PR.
- **Did the PR make a non-obvious design choice?** → Document the rationale in the PR description.
- **Did the PR resolve or invalidate an item on bar-raise issue #11?** → Tick the checkbox or add a follow-up note on the issue.
- **Did the PR complete a checkbox on a milestone tracking issue (#2/#9/#3/#4/#5/#6)?** → Tick the box on the issue itself, not just locally.
- **Did the PR change release/operational steps?** → Update `docs/runbooks/release-rollback.md` (or add a new runbook under `docs/runbooks/`).

If none of the above apply, say so explicitly in the PR description ("no spec/runbook impact").

### Milestones

Tracked as GitHub issues with `milestone` + `tracking` labels. Spec §7 cross-references each:

- **#2 v0.1 alpha** — library + `select`, in-process integration test (no runnable binary).
- **#9 v0.11** — runnable binary, CLI host, plain-text help, process-level smoke (5/6 cases).
- **#3 v0.3 alpha** — all 14 input clets + AOT publish + TUIcast keystroke harness.
- **#4 v0.5 beta** — schema/exit-code/naming locks; threat model; release workflow proven; **TG dep on a release tag, not develop**.
- **#5 v0.9 RC** — full test matrix green; one real release cycle exercised; rollback runbook exercised.
- **#6 v1.0 GA** — tied to TG v2 GA; brew/winget/nuget channels live.

Each issue's checkboxes are the source of truth for "is this milestone done." When you complete an item, tick the box in the issue, not just in your head.
