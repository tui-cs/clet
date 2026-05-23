# `clet` Implementation Spec

**Status:** draft v0.5 · companion to the PR/FAQ in [issue #5155](https://github.com/gui-cs/Terminal.Gui/issues/5155)

This is the implementation spec. It assumes the PR/FAQ is broadly accepted and covers what to build, where it lives, what changes in Terminal.Gui to support it, how it ships, and how it's tested.

## 1. Scope and Non-Goals

### In scope (v1.0)

- New repo `gui-cs/clet` containing all clet code: abstractions, registry, JSON, built-in clets, CLI binary, release automation.
- Targeted changes to `gui-cs/Terminal.Gui` core (§3) that benefit TG generally and unblock clet specifically.
- Fourteen input clets and one browser clet (`md`) statically registered in v1.0.
- Native installer channels: Homebrew (gui-cs tap), WinGet, .NET tool. NativeAOT for native channels.
- Independent SemVer; major version tied to `schemaVersion` changes per §4.3.1 (see [D-022](decisions.md)).
- JSON output contract (schemaVersion 1).
- Inline input rendering; alt-screen viewer rendering.
- Theming via TG's `ConfigurationManager`.

### Out of scope (deferred to v2 or later)

- Extracting clet abstractions into a published NuGet for third-party consumption (`Clet.Abstractions`). Today, `IClet` is internal-to-the-binary; v2 may publish it.
- Third-party clet runtime loading (`Assembly.LoadFrom` into the AOT'd CLI).
- `password` clet.
- Additional viewer clets (`json`, `log`, `diff`).
- Telemetry beyond a one-shot opt-in install ping.
- Embedded/inline-in-other-TG-app use of clets.

## 2. Architecture Overview

Two repos. One assembly that matters (the CLI exe). One release cadence.

```
gui-cs/Terminal.Gui                           gui-cs/clet
├── Terminal.Gui/                             ├── src/
│     (core; §3 tweaks land here,             │   └── Clet/
│      no clet-specific types)                │         Abstractions/  (IClet, ICletRegistry, ...)
├── Tests/                                    │         Registry/
│     (TG core tests only;                    │         Json/          (CletJsonContext, SchemaV1)
│      clet tests live in gui-cs/clet)        │         Clets/Input/   (14 input clets)
└── .github/workflows/                        │         Clets/Viewer/  (MarkdownClet)
      notify-clet-on-release.yml (NEW)        │         Hosting/       (Program.cs, CLI parser)
                                              │         Help/          (overview.md)
                                              ├── tests/
                                              │     Clet.UnitTests/
                                              │     Clet.IntegrationTests/
                                              │     Clet.SmokeTests/
                                              │     Clet.UITests/
                                              │     SPEC.md
                                              ├── docs/
                                              │     threat-model.md
                                              │     runbooks/release-rollback.md
                                              ├── specs/
                                              │     clet-spec.md (this file)
                                              │     decisions.md
                                              │     press-release.md
                                              └── .github/workflows/
                                                    ci.yml
                                                    release.yml
```

**Process model.** The `clet` binary is a thin shell:
1. Parses CLI args (hand-rolled parser — see [D-006](decisions.md)).
2. Looks up the alias in its in-process `ICletRegistry`.
3. Initializes a Terminal.Gui `IApplication`.
4. Calls `clet.RunBoxedAsync(...)`.
5. Serializes the result, emits to stdout, exits with the right code.

All of (3)+(4) is plain Terminal.Gui hosting against TG's public API. The clet itself is a Terminal.Gui View. Nothing in TG core knows about clets; nothing in clets requires private TG API.

### 2.1 Ownership

**TG core** (rendering, drivers, Views, keybindings) → `gui-cs/Terminal.Gui` maintainers.
**CLI host, registry, JSON envelope, packaging, clets** → `gui-cs/clet` maintainers.
**Cross-repo bugs** file in `gui-cs/clet` first; the clet maintainer reproduces, isolates, and escalates upstream if the root cause is in TG core.

## 3. Terminal.Gui Changes Required

All prerequisite TG changes have landed on `develop`:

- **Inline rendering** — shipping and exercised by `md`, the inline examples, and `gui-cs/ai`.
- **AOT compatibility** — tracked in TG core; remaining issues surface by building/running `clet`.
- **`ConfigurationManager`** path-based loading — broadly used and tested.
- **`Markdown` View** — vetted for the read-only, dismissable, themed shape clet needs.
- **Terminal-driver inline-capable detection** — in place.
- **`Application.RunAsync(Toplevel, CancellationToken)`** ([#5157](https://github.com/gui-cs/Terminal.Gui/issues/5157)) — landed. Clet binds directly.
- **`FileDialog` typed result** ([#5158](https://github.com/gui-cs/Terminal.Gui/issues/5158)) — landed as `Dialog<IReadOnlyList<string>?>`.

clet builds against the TG version named by `<TerminalGuiVersion>` in `src/Clet/Clet.csproj`; the release workflow overrides it from the dispatch payload. See [D-020](decisions.md).

### 3.1 Cancellation contract

On cancel, clet emits `{"schemaVersion":1,"status":"cancelled"}` and nothing else — no `value`, no `code`, no partial result — regardless of whether `IValue<T>.Value` is readable on the underlying View. This is clet's wire contract; TG's disposition of `IValue<T>.Value` on cancellation is a TG-internal concern.

### 3.2 FileDialog typed result

`FileDialog` inherits from `Dialog<IReadOnlyList<string>?>`. The `pick-file` and `pick-directory` clets bind to this directly. The §4.3.2 per-clet `value` shape table specifies the resulting JSON wire format (string for single-select, array of strings for `--multi`).

## 4. `gui-cs/clet` Repo

This repo holds everything: abstractions, registry, JSON, built-in clets, the CLI binary, and release automation. One assembly is published; everything else is build-time only or test-only.

### 4.1 Project layout

```
gui-cs/clet/
├── Clet.slnx
├── src/
│   └── Clet/                              (single Exe; PublishAot=true; net10.0)
│       ├── Abstractions/
│       │     IClet.cs                     (IClet + IClet<T> with RunBoxedAsync DIM)
│       │     IViewerClet.cs               (with RunBoxedAsync DIM)
│       │     ICletRegistry.cs
│       │     BoxedCletResult.cs
│       │     CletKind.cs                  (Input | Viewer)
│       │     CletRunOptions.cs
│       │     CletRunResult.cs             (non-generic + generic)
│       │     CletRunStatus.cs
│       │     CletOptionDescriptor.cs
│       ├── Registry/
│       │     CletRegistry.cs
│       │     BuiltInClets.cs              (hand-written)
│       ├── Json/
│       │     CletJsonContext.cs           ([JsonSerializable] source-gen)
│       │     SchemaV1.cs
│       ├── Clets/
│       │   ├── Input/
│       │   │     SelectClet.cs, TextClet.cs, IntClet.cs, DecimalClet.cs,
│       │   │     ConfirmClet.cs, MultiSelectClet.cs, PickFileClet.cs,
│       │   │     PickDirectoryClet.cs, DateClet.cs, TimeClet.cs,
│       │   │     DurationClet.cs, ColorClet.cs, AttributePickerClet.cs,
│       │   │     RangeClet.cs
│       │   │     (+ helpers: LabelParser.cs, FileFilterParser.cs, RangeView.cs)
│       │   └── Viewer/
│       │         MarkdownClet.cs
│       ├── Help/
│       │     overview.md                  (embedded resource for --help)
│       ├── Hosting/
│       │     Program.cs
│       │     CommandLineRoot.cs           (hand-rolled CLI parser; D-006)
│       │     AliasDispatcher.cs
│       │     OutputFormatter.cs
│       │     ExitCodes.cs
│       │     MarkdownHelpRenderer.cs
│       │     CletStyling.cs
│       └── Properties/
│             AssemblyInfo.cs              (InternalsVisibleTo)
├── tests/
│   ├── SPEC.md                            (testing spec)
│   ├── Clet.UnitTests/
│   ├── Clet.IntegrationTests/
│   ├── Clet.SmokeTests/
│   └── Clet.UITests/
└── docs/
    ├── threat-model.md
    └── runbooks/release-rollback.md
```

**One src project (`Clet`).** Abstractions, registry, JSON, built-in clets, and `Program.Main` all compile into one assembly.

### 4.2 Core types

All types are `internal` to the `Clet` assembly in v1.0. v2 may extract them to `Clet.Abstractions`. See source files in `src/Clet/Abstractions/` for canonical definitions. Key shapes:

- **`IClet`** — declares `PrimaryAlias`, `Aliases`, `Description`, `Kind`, `ResultType`, `Options`, and `RunBoxedAsync(IApplication, string?, CletRunOptions, CancellationToken)`.
- **`IClet<TValue> : IClet`** — adds typed `RunAsync(...)` returning `CletRunResult<TValue>`. Provides `RunBoxedAsync` as a default interface method.
- **`IViewerClet : IClet`** — typed `RunAsync(...)` returning `CletRunResult` (no value). Also provides `RunBoxedAsync` via DIM.
- **`BoxedCletResult`** — `(CletRunStatus, object?, string?, string?)` record struct for non-generic dispatch.
- **`CletRunOptions`** — `Title`, `JsonOutput`, `Timeout`, `Fullscreen`, `CletOptions`, `Arguments`.
- **`CletRunResult` / `CletRunResult<T>`** — `Status`, `Value` (generic only), `ErrorCode`, `ErrorMessage`.
- **`ICletRegistry`** — `Register(IClet)`, `TryResolve(string, out IClet?)`, `All`.

### 4.3 JSON schema (v1)

```json
{
  "$id": "https://gui-cs.github.io/clet/schema/v1.json",
  "type": "object",
  "required": ["schemaVersion", "status"],
  "properties": {
    "schemaVersion": { "const": 1 },
    "status": { "enum": ["ok", "cancelled", "error", "no-result"] },
    "value":   { },
    "code":    { "type": "string" },
    "message": { "type": "string" }
  },
  "allOf": [
    { "if": { "properties": { "status": { "const": "ok"    } } },
      "then": { "anyOf": [ { "required": ["value"] }, { "not": { "required": ["value"] } } ] } },
    { "if": { "properties": { "status": { "const": "error" } } },
      "then": { "required": ["code", "message"] } }
  ]
}
```

**Normative envelopes:**
```json
{ "schemaVersion": 1, "status": "ok", "value": <T> }                  // input clet success
{ "schemaVersion": 1, "status": "ok" }                                // viewer clet dismiss
{ "schemaVersion": 1, "status": "cancelled" }                         // cancel (any clet)
{ "schemaVersion": 1, "status": "error", "code": "...", "message": "..." }
{ "schemaVersion": 1, "status": "no-result" }
```

**No `type` field on the envelope.** Result types are advertised once per alias by `clet list --json`. Consumers cache the registry; they do not branch on a per-call CLR type name. See [D-001](decisions.md).

**Cancel is decoupled from TG.** See §3.1.

Schema is pinned in `src/Clet/Json/SchemaV1.cs`. JSON contract tests ([`tests/SPEC.md`](../tests/SPEC.md) §2.5) validate emitted output against this schema.

### 4.3.1 Schema versioning policy

`schemaVersion: 1` is the contract for the entire `clet 1.x` line. Changes within `1.x` are additive only — existing fields never change meaning, never become required, never change type. A `schemaVersion: 2` is permitted only at a `clet 2.0.0` boundary, and `clet 2.x` must accept `--schema-version 1` to emit the v1 envelope for at least one minor release, giving consumers a parallel-period to migrate.

### 4.3.2 Per-clet `value` shapes

For schema-lock at v0.5, the shape of `value` is fixed per alias.

| Alias                         | `value` shape                                                |
|-------------------------------|--------------------------------------------------------------|
| `text`                        | string (newlines preserved as `\n`)                          |
| `int`                         | integer                                                      |
| `decimal`                     | number (JSON number; consumer decides float vs decimal)      |
| `confirm`                     | boolean                                                      |
| `select`                      | string (label text of the selected item — see [D-008](decisions.md)) |
| `multi-select`                | array of strings (label texts, in display order — see [D-009](decisions.md)) |
| `pick-file`                   | string (path)                                                |
| `pick-file --multi`           | array of strings (paths, ascending sort)                     |
| `pick-directory`              | string (path)                                                |
| `date`                        | string, ISO-8601 date (`YYYY-MM-DD`)                         |
| `time`                        | string, ISO-8601 time (`HH:MM:SS`)                           |
| `duration`                    | string, ISO-8601 duration (`PT1H30M`)                        |
| `color`                       | string, `#RRGGBB` (lowercase hex)                            |
| `attribute-picker`            | object, `{"fg": "#RRGGBB", "bg": "#RRGGBB", "style": "..."}` |
| `linear-range`                | object, shape depends on `--mode`. Single: `{"mode":"single","value":"<label>","index":N}`. Multi: `{"mode":"multi","values":[...],"indices":[...]}`. Range: `{"mode":"range","kind":"closed\|left\|right\|none",...}` with start/end fields conditional on kind. See [D-032](decisions.md). |

### 4.4 Registration

`BuiltInClets.RegisterAll(ICletRegistry)` hand-registers all 18 clets. Auto-discovery via a source generator was explored and dropped — there is no `[Clet]` attribute in shipped code, and no source-generator project in the repo.

### 4.5 Built-in clet implementation pattern

Each input clet wraps a TG View in `RunnableWrapper<TView, TResult>`, which auto-extracts the typed result via `IValue<TResult>`. See `src/Clet/Clets/Input/SelectClet.cs` for the canonical example.

**Pattern:**
- The clet does not own the run loop or the application lifecycle; the host (`Program.Main`) does.
- `RunnableWrapper<TView, TResult>` is the TG primitive that bridges Views to typed results.
- `await app.RunAsync(wrapper, ct)` uses the `IApplication.RunAsync(Toplevel, CancellationToken)` overload (§3).
- Viewer clets follow the same shape but return `CletRunResult` (no `T`).
- `SelectClet` returns `string?` (the label text), not `int?` (the index) — see [D-008](decisions.md).

### 4.6 `Program.Main`

See `src/Clet/Hosting/Program.cs`. The host creates a `CancellationTokenSource`, registers all clets, and delegates to `CommandLineRoot.InvokeAsync(args, token, Console.Out, Console.Error)`.

### 4.7 CLI surface

```
clet <alias> [positional...] [--initial <value>] [--title|--prompt <text>] [--json] [--timeout 30s] [--fullscreen] [--cat] [--no-browse] [--rows <n>] [--output <path>] [--<opt> <value>]...
clet list [--json]
clet help <alias>
clet --help
clet --version
```

**`--version` output.** `clet --version` prints `X.Y.Z (Terminal.Gui A.B.C)` where `X.Y.Z` is clet's own SemVer and `A.B.C` is the TG version the binary was built against. See [D-022](decisions.md).

**`--help` banner.** `clet --help` (and bare `clet`) prints the ASCII logo followed by usage:

```
  ╔═╗╦  ╔═╗╔╦╗
  ║  ║  ╠═  ║
  ╚═╝╩═╝╚═╝ ╩
```

**Built-in flags.** `--initial`, `--title` (alias `--prompt`), `--json`, `--timeout`, `--fullscreen`, `--cat`, `--no-browse`, `--rows`, and `--output` are parsed at the host level and apply to every clet. Anything else of the form `--<name> <value>` is validated against the dispatched clet's option descriptors and then forwarded as a clet-specific option (see each clet's `clet help <alias>`). Unknown options are rejected with a usage error (exit 2) instead of consuming the next token as a value. Bare positional tokens are forwarded as `CletRunOptions.Arguments` for clets that consume them (e.g. `select`, `multi-select`, `md`); clets that do not consume positional args reject them with a usage error (exit 2) before the clet runs. See [D-025](decisions.md) for the `AcceptsPositionalArgs` design and [D-014](decisions.md) for why `--title` is a host flag.

**`--cat` (non-interactive rendering).** When `--cat` is passed to a viewer clet (currently `md`), content is rendered as ANSI-formatted text directly to stdout — no alt-screen, no interactive session. Useful for piping (`clet md --cat README.md | less -R`), CI logs, and AI agents. Content is resolved from file arguments, `--initial`, or stdin, same as the normal viewer path. If no content is available, exits with usage error (exit 2). See [D-027](decisions.md).

**`--no-browse` (disable browser mode).** When `--no-browse` is passed to `md`, clicking local `.md` links shows the URL in the status bar instead of navigating. By default, `md` runs as a browser: following local links navigates to them with a back/forward history stack (Ctrl+Left / Ctrl+Right or ← → buttons in the status bar), and fragment anchors (`file.md#heading`) scroll to the matching heading.

**`--output <path>` / `-o <path>` (file output).** Writes a successful clet result (plain text or JSON) to the specified file instead of stdout. `OutputFormatter` creates the file with non-overwriting semantics; existing paths are refused rather than truncated, including paths that are symlinks. Non-success results are not written to the output file and are emitted through the normal stdout/stderr paths. This works around the Terminal.Gui limitation where stdout redirection (`$()`, `|`, `>`) swallows the TUI (see gui-cs/Terminal.Gui#5207). If the file cannot be written, an error is emitted to stderr and the process exits with code 2. See [D-028](decisions.md).

**Input-size caps.** `--initial` is capped at 64 K characters (code units) after alias resolution; unknown aliases remain usage errors rather than being reported as oversized-input validation failures. `clet md` stdin is capped at 8 M characters. On exceed for known aliases/content paths: exit 65, error code `input-too-large`, JSON envelope `{"schemaVersion":1,"status":"error","code":"input-too-large","message":"..."}`. These caps prevent OOM from untrusted piped input (see Appendix A). Per-clet options (`--<name> <value>`) are not yet capped; tracked as a follow-up.

**Defaults.** Input clets render inline. Viewer clets (`md`) render fullscreen. `--fullscreen` forces fullscreen for input clets; it's a no-op for viewers.

**Theming.** No `--theme` flag. Theme selection goes through `ConfigurationManager`'s existing mechanisms.

**Help rendering.** `clet --help` and `clet help <alias>` render Markdown to ANSI escape sequences and write to stdout (print mode), then exit immediately — no interactive viewer (see [D-016](decisions.md)). Root help reads from embedded `src/Clet/Help/overview.md`; per-alias help is generated dynamically from `IClet` metadata by `MarkdownHelpRenderer.BuildAliasHelpMarkdown()`. Help is pipeable (`clet --help | less`) and consumable by AI agents reading stdout.

### 4.8 Exit code mapping

| Status                | Exit |
|-----------------------|-----:|
| Ok (input or viewer)  |    0 |
| NoResult              |    1 |
| Usage error           |    2 |
| Validation error      |   65 |
| I/O error             |   74 |
| Cancelled             |  130 |

### 4.9 NativeAOT publish settings

In `Clet.csproj`:
```xml
<PublishAot>true</PublishAot>
<InvariantGlobalization>false</InvariantGlobalization>
<StackTraceSupport>true</StackTraceSupport>
<DebuggerSupport>false</DebuggerSupport>
<EventSourceSupport>false</EventSourceSupport>
<UseSystemResourceKeys>true</UseSystemResourceKeys>
```

Target binary size: ~8MB. Cold-start budget: <100ms on Apple Silicon, <100ms on Linux x64, <150ms on Windows x64.

## 5. Release and Update Pipeline

### 5.1 Trigger

`gui-cs/Terminal.Gui` and `gui-cs/Editor` fire `repository_dispatch` events to `gui-cs/clet` for main-branch package publishes (`tg-main-published` and `editor-main-published`). Develop-package dispatches do not publish clet builds.

Additionally, the release workflow fires on pushes to clet's own main branch (changes in `src/` or `tests/`) and manual `workflow_dispatch`.

### 5.2 Build matrix

The actual workflow is `.github/workflows/release.yml`. It builds AOT binaries for the target RIDs. Code signing is deferred post-1.0 per [D-012](decisions.md); Homebrew ships a build-from-source formula.

### 5.3 Smoke test gate (P0; release fails closed)

Before any publish step, every built binary runs a smoke matrix. The gate is process-level: it spawns the AOT'd binary, drives it from outside, and asserts on exit code + stdout JSON.

**Driver:** [`gui-cs/TUIcast`](https://github.com/gui-cs/TUIcast) in deterministic-script mode. TUIcast spawns the binary inside a PTY, writes keystrokes to the PTY fd, and captures an asciinema stream. Deterministic mode takes a comma-separated keystroke script (`"wait:500,ArrowDown,Enter"`).

**Cases:**

1. `clet --version` — returns `X.Y.Z (Terminal.Gui A.B.C)`.
2. `clet list --json` — validates against the schema.
3. For each input clet: TUIcast spawns with `--initial <stub> --json --timeout 1s` and a per-clet keystroke script; verify exit 0 and JSON envelope.
4. For `md`: spawns against a fixture markdown file with `"wait:500,q"`; verify exit 0 and `{"schemaVersion":1,"status":"ok"}`.
5. Cancellation: spawn with `--timeout 100ms`, no keystrokes; verify exit 130 and `{"schemaVersion":1,"status":"cancelled"}`.

**Asciinema artifact.** TUIcast captures every smoke run as `.cast`; on failure, the cast is uploaded as a workflow artifact.

Any failure halts the publish workflow.

### 5.4 Publish steps

After all matrix jobs and smoke tests pass. Channel determines which publish steps run:

| Channel | Trigger | NuGet | Homebrew | WinGet |
|---------|---------|:-----:|:--------:|:------:|
| Prerelease | push to `main`, main-branch TG/Editor dispatch, or manual dispatch while `<Version>` has `-rc` | prerelease | — | — |
| Stable | push to `main`, main-branch TG/Editor dispatch, or manual dispatch while `<Version>` has no prerelease suffix | stable | build-from-source | manifest PR |

**.NET tool** (NuGet) — follows the [mdv](https://github.com/gui-cs/mdv) pattern: `<PackAsTool>true</PackAsTool>`, `<ToolCommandName>clet</ToolCommandName>`, `<PackageId>clet</PackageId>` on `src/Clet/Clet.csproj`. Install: `dotnet tool install -g clet`. See [D-019](decisions.md) (packaging) and [D-024](decisions.md) (package id).

**Homebrew tap** (`gui-cs/homebrew-tap`) — release channel only. Build-from-source formula per [D-012](decisions.md).

**WinGet** (PR to `microsoft/winget-pkgs`) — release channel only. Unsigned binary with SmartScreen warning acceptable for early adopters per [D-012](decisions.md).

### 5.5 Failure handling

If any publish step fails:
- The workflow opens or updates an issue titled `Release clet v<VERSION> failed (<channel>)`.
- Release failures page; repeated failures for the same open incident are added as comments.
- Already-published channels are noted; rollback is manual (see `docs/runbooks/release-rollback.md`).

### 5.6 Versioning

clet maintains its own SemVer, independent of Terminal.Gui's version. Major bumps = `schemaVersion` changes (§4.3.1). Minor bumps = new clets or significant CLI additions. Patch bumps = bug fixes, including rebuilds against new TG versions. The TG version is surfaced in `--version` output for diagnostics only. See [D-022](decisions.md).

**Auto-increment.** The release workflow reads the base version from `Clet.csproj <Version>`. For the rc phase (`1.0.0-rc`), it finds the latest matching `v1.0.0-rc.*` tag and increments the prerelease build number. For stable phases (`1.0.0`), it finds the latest stable `vMAJOR.MINOR.*` tag and increments patch. Manual `workflow_dispatch` can override the computed version (required when dispatching from a non-main branch).

`Directory.Build.props` declares `<TerminalGuiVersion>` and `<TerminalGuiEditorVersion>` known-good pins, and the Clet project references both via MSBuild properties. The release workflow passes `-p:TerminalGuiVersion=${{ env.TG_VERSION }}` and `-p:TerminalGuiEditorVersion=${{ env.TGE_VERSION }}`. Release restores fail if either resolved version is older than the latest stable NuGet package.

## 6. Testing

Full testing strategy lives in [`tests/SPEC.md`](../tests/SPEC.md). Summary:

- **Nine test layers**, each with a clear "what does this catch" purpose. Three harness families: in-process logic (no `Application.Init`), in-process UI (`IApplication` + `InputInjection` + `Driver.Contents` snapshots, frame-stepped), process-level (TUIcast over PTY).
- The **four-terminal manual matrix** ([#23](https://github.com/gui-cs/clet/issues/23)) is the v0.5 gate.
- The **JSON contract tests** are the schema-lock guard (`SchemaV1`).
- The **smoke tests** are the release gate.

## 7. Milestones

Schedule follows TG releases, not a calendar.

| Milestone | Tracking | Exit criteria |
|-----------|----------|---------------|
| **v0.1 alpha** | [#2](https://github.com/gui-cs/clet/issues/2) | Repo bootstrapped; abstractions, registry, JSON in place; `select` clet working in unit + integration tests. No runnable binary — see v0.11. |
| **v0.11** | [#9](https://github.com/gui-cs/clet/issues/9) | Runnable binary. CLI host per §4.6/§4.7. `clet --help` / `--version` / `help <alias>` / `list --json` / `<alias> --json` work end-to-end. Process-level smoke harness (Process.Start-based; TUIcast keystroke harness deferred to v0.3 — [D-007](decisions.md)). |
| **v0.3 alpha** | [#3](https://github.com/gui-cs/clet/issues/3) | All 14 input clets functional. JSON schema drafted. AOT publish green. TUIcast keystroke harness wired up. |
| **v0.5 beta** | [#4](https://github.com/gui-cs/clet/issues/4) | Naming/schema/exit-codes locked; inline rendering verified on four-terminal matrix; `Markdown` View integration verified; threat model published (`docs/threat-model.md`); `dotnet tool install -g clet` works locally ([D-019](decisions.md), [D-024](decisions.md)); main-channel rc workflow proven. Release-tag trigger proof and Homebrew/WinGet draft manifests moved to v0.9 RC. |
| **v0.75 rc** | [#33](https://github.com/gui-cs/clet/issues/33) | Friends-and-family rc. >=5 external testers; >=3 Issues filed by non-maintainers; maintainer dogfooding for >=2 weeks; >=1 AI agent harness consuming `--json`; all P0 rc bugs resolved or deferred. |
| **v0.9 RC** | [#5](https://github.com/gui-cs/clet/issues/5) | All §6 test layers passing. Release workflow proven against a real TG release. Homebrew formula + WinGet manifest in working-draft form. One real release cycle exercised. Rollback runbook exercised once. |
| **v1.0 GA** | [#6](https://github.com/gui-cs/clet/issues/6) | Tied to TG v2 GA. Brew, WinGet, NuGet channels live. Documentation published. |

## 8. Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------:|-------:|------------|
| AOT issue surfaces during build or smoke test | Medium | Medium | AOT publish tests ([`tests/SPEC.md`](../tests/SPEC.md) §2.7) catch before publish; fall back to self-contained single-file (~30MB) if blocking. |
| Native installer pipeline (Homebrew/WinGet) ops cost | Medium | Medium | §5.3 smoke gate + release-pipeline dry-runs catch most issues; `docs/runbooks/release-rollback.md` documents withdrawal. |
| Markdown View quality regression vs `glow` | Low | Medium | TG-side golden-file corpus (#5156); quarterly comparison run. |
| Develop publishes create NuGet version sprawl | ~~Retired~~ | — | Develop no longer publishes. Main-only rc channel eliminates version sprawl. |
| First real release fails mid-publish | Medium | High | Weekly release-pipeline dry-runs; `docs/runbooks/release-rollback.md` walks through per-channel withdrawal. Runbook exercised before v0.9 RC. |
| Naming concerns about "clet" | Low | Low | Acknowledge in docs; outlast. |

## 9. Open Questions

1. **Telemetry.** Not in v1.0 scope; revisit at v1.1 with a privacy review.
2. **Homebrew tap repo name.** `gui-cs/homebrew-tap` assumed; confirm it exists or create.
3. **Code signing certs.** Deferred post-1.0 per [D-012](decisions.md). Confirm ownership/renewal before signing is re-enabled.
4. ~~**`md` content source.**~~ Resolved ([D-015](decisions.md)). Both file arguments and stdin, with precedence: file args -> `--initial` -> stdin -> error.
5. **PR/FAQ update upstream.** Issue #5155's PR/FAQ still references `Terminal.Gui.Clets` as a separate assembly. Update the issue body to match this spec before v1.0.
6. **"Any-View ambition" / auto-discovered clets.** Deferred to v2 per [D-021](decisions.md). See §11.

## 10. Implementation Order

Steps 1-10 are done (through v0.5). Remaining:

11. **Publish channels:** Homebrew (build-from-source), then WinGet, then NuGet tool push.
12. **v0.75 rc** — friends-and-family testing ([#33](https://github.com/gui-cs/clet/issues/33)).
13. **v0.9 RC** — release workflow proven against real TG release; Homebrew/WinGet manifests in working-draft form; rollback runbook exercised.
14. **v1.0 GA.**

## 11. Future: auto-discovered clets ("any IValue<T> View just works")

The original PR/FAQ pitched clet as exposing **any** `IValue<T>` View to the shell automatically. v1.0 ships 15 hand-written clets instead. This section captures the design exploration; [D-021](decisions.md) records the deferral to v2.

### 11.1 What v1.0 ships

15 clets, each ~50-150 lines under `src/Clet/Clets/{Input,Viewer}/`. Each declares aliases, description, kind, result type, options, and a `RunAsync`. `BuiltInClets.RegisterAll` is hand-written ([D-004](decisions.md)/[D-021](decisions.md)).

### 11.2 Key learnings

- **Most per-clet code is metadata, not behavior.** `TextClet` is 65 lines; unique behavior is 2 lines.
- **Cross-cutting concerns live above clets.** `--title` ([D-014](decisions.md)), scheme ([D-013](decisions.md)), link safety ([D-017](decisions.md)), exit codes, JSON envelope — all host-level.
- **Wire format isn't derivable from `IValue<T>`.** `SelectClet`'s `IValue<int?>` becomes a `string` on the wire. `PickFile`'s `IValue<IReadOnlyList<string>?>` becomes string or array depending on `--multi`. The §4.3.2 table is hand-curated.
- **Initial-value parsing is per-clet.** `IntClet` parses `"42"` as `int`; `SelectClet` parses as label-to-index lookup; `PickFileClet` doesn't take an initial.

### 11.3 What full auto-discovery would require

**TG-side (the harder half):** `[Shellable]` attribute or marker interface, wire-format declaration (`ToWire` hook), initial-value parser, per-View option surface.

**clet-side:** Real source generator scanning TG assemblies for `[Shellable]` types, emitting registration + dispatch glue.

**Cross-cutting:** Schema-lock coupling (TG attribute changes become wire-format changes); plugin-loading exclusion still applies (runtime `LoadFrom` remains out of scope).

### 11.4 Decision

**Don't pursue in v1.x.** The leverage only kicks in with a long tail of post-v1.0 Views or third-party Views wanting shell exposure — both are post-v1.0 stories. At v2, use §11.3 as the starting checklist for TG-side co-design. See [D-021](decisions.md).

### 11.5 Open questions for v2

- Is the v2 third-party-clets story still wanted?
- Would TG core accept `[Shellable]` and friends, or must they live in a companion package?
- Can `IValue<T>` plus conventions cover 90% of clets, with overrides for the rest?
- Does adding a `[Shellable]` View on TG develop trigger a clet wire-format change?

## Appendix A: Threat Model Summary

Full document published at `docs/threat-model.md`.

- **Untrusted inputs:** `--initial`, env vars, stdin content, fixture file paths, `--title`, clet-specific options.
- **Input-size caps:** `--initial` is capped at 64 K characters after alias resolution; `clet md` stdin is capped at 8 M characters. On exceed for known aliases/content paths: exit 65, error code `input-too-large`, JSON envelope `{"schemaVersion":1,"status":"error","code":"input-too-large","message":"..."}`. Per-clet options are not yet capped; tracked as a follow-up.
- **Sanitization:** A `TerminalEscapeSanitizer` strips ESC, BEL, 8-bit CSI/OSC, and C1 7-bit pairs from all user-supplied content before it reaches the terminal driver or Terminal.Gui views. Applied at `MarkdownClet` (inline + file content) and `MarkdownHelpRenderer.RenderToAnsi` (input + rendered output). clet does not rely on TG to filter terminal escapes (D-030).
- **Markdown link policy:** Default `SurfaceOnly` (links shown, never auto-opened). `--allow-link-open` flag deferred — not wired in v1.0; safe-by-default for AI agent use. See D-017, D-031.
- **File access:** `clet md` confines file reads by default to `.md`, `.markdown`, and `.txt` files under the working directory, with per-file, aggregate-size, glob-count, and binary-content checks. `--allow-file <path>` explicitly opts in a file or directory outside that default boundary; `--allow-binary` opts out of NUL-byte rejection. `pick-file` and `pick-directory` treat `--root` as a starting directory, not a sandbox, and rely on the OS permission model for interactive file selection.
- **Plugin loading:** None in v1.0. (Closes the entire LoadFrom-based attack surface.)

## Appendix B: Cross-References

- PR/FAQ: [issue #5155](https://github.com/gui-cs/Terminal.Gui/issues/5155)
- TG core docs: `docfx/docs/application.md`, `docfx/docs/View.md`, `docfx/docs/cancellable-work-pattern.md`
- Contributor rules: `.claude/rules/`, `CLAUDE.md`
