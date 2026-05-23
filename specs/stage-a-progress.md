# Stage A Progress: Terminal.Gui.Cli Library Extraction

## Status: COMPLETE ✅ — All Phases Done

## Overview

Implementing Stage A of the Terminal.Gui.Cli library spec: extracting the hosting infrastructure from clet into a reusable class library at `src/Terminal.Gui.Cli/`, then migrating clet to consume it.

## Approach

**Test-first**: Write tests for the library's public API surface against the spec before implementing. Then implement to make tests pass.

## Phases

### Phase 1: Project scaffolding ✅
- [x] Create `src/Terminal.Gui.Cli/Terminal.Gui.Cli.csproj`
- [x] Create `tests/Terminal.Gui.Cli.Tests/Terminal.Gui.Cli.Tests.csproj`
- [x] Add both to `Clet.slnx`
- [x] Add `AssemblyInfo.cs` with InternalsVisibleTo

### Phase 2: Core Abstractions (test-first) ✅
- [x] `CommandKind` enum
- [x] `CommandStatus` enum
- [x] `CommandOptionDescriptor` record
- [x] `CommandResult` (non-generic + generic)
- [x] `ICliCommand` interface
- [x] `ICliCommand<TValue>` generic interface
- [x] `IViewerCommand` interface
- [x] `ICommandRegistry` interface
- [x] `CommandRunOptions` class
- [x] `GlobalOptionDescriptor` record

### Phase 3: Registry & Exit Codes (test-first) ✅
- [x] `CommandRegistry` implementation
- [x] `ExitCodes` static class

### Phase 4: JSON Envelope & Output (test-first) ✅
- [x] `JsonEnvelope` class
- [x] `CliJsonContext` source-generated
- [x] `TypeNames` static class
- [x] `ResultWriter` static class

### Phase 5: Arg Parser (test-first — rewrite from scratch) ✅
- [x] Data-driven parser consuming `GlobalOptionDescriptor` registrations
- [x] Framework-owned flags (--json, --initial, --timeout, etc.)
- [x] Consumer-registered global options
- [x] Per-command option validation
- [x] `--option=value` syntax support
- [x] `--` separator (end of options)
- [x] Unknown option rejection (exit 2)

### Phase 6: Host & Dispatcher ✅
- [x] `CliHostOptions` class
- [x] `CliHost` class (with TUI dispatch, cancellation, ConfigurationManager)
- [x] `InputCommandRunner` helper
- [x] `OpenCliWriter` (hand-built OpenCLI JSON from registry metadata)
- [x] `IHelpProvider` interface
- [x] `MetadataHelpProvider` (auto-generated from registry)

### Phase 7: Utilities ✅
- [x] `TerminalEscapeSanitizer`

### Phase 8: Built-in Commands 🔲 (deferred — not needed to prove the API)
- [ ] `HelpCommand` (IViewerCommand — TUI mode)
- [ ] `AgentGuideCommand` (IViewerCommand)
- [ ] `EmbeddedMarkdownHelpProvider`
- [ ] `MarkdownRenderer` (depends on TG markdown-to-ANSI API)

### Phase 9: clet migration ✅
- [x] Add `<ProjectReference>` from clet to the library
- [x] Refactor all 14 input clets to `ICliCommand<T>`
- [x] Refactor all 4 viewer clets to `IViewerCommand`
- [x] Rewrite `Program.cs` to use `CliHost`
- [x] Delete 16 superseded files (abstractions, registry, hosting, JSON)
- [x] Migrate `BuiltInClets.RegisterAll` to `ICommandRegistry`
- [x] Migrate `MarkdownHelpRenderer` to library types
- [x] Migrate `MarkdownContentResolver` to `CommandRunOptions` extensions
- [x] Add `RenderCatAsync` to `MarkdownClet` (replaces AliasDispatcher --cat logic)
- [x] Update all test projects (UnitTests, IntegrationTests, ConfigTests, UITests, SmokeTests)
- [x] Delete 8 unit test files now covered by library tests
- [x] All existing tests pass (495 across 6 projects, 0 warnings)

## Test Summary

- **95 tests** passing in `Terminal.Gui.Cli.Tests`
- **310 tests** passing in `Clet.UnitTests`
- **54 tests** passing in `Clet.IntegrationTests` (2 skipped: FileDialog under ansi driver)
- **26 tests** passing in `Clet.ConfigTests`
- **14 tests** passing in `Clet.SmokeTests` (1 skipped: deferred to v0.3)
- **0 warnings**, **0 errors** across full solution build (Debug and Release)

## Decisions & Learnings

| # | Decision/Learning | Rationale |
|---|---|---|
| 1 | `CommandResult` is a `readonly record struct` (not class) | Matches spec §5.1. Value-type avoids allocations on hot path. Both generic and non-generic in same file since they're tightly coupled record structs. |
| 2 | `ICliCommand<TValue>` in separate file `ICliCommandGeneric.cs` | One-type-per-file rule. Named distinctly from `ICliCommand.cs`. |
| 3 | `<alias> --help` interception happens BEFORE ArgParser.Parse() | The parser would reject `--help` as an unknown per-command option. Must intercept `args[1] is "help"/"--help"/"-h"` early in CliHost.RunAsync(). |
| 4 | `CliHost` owns the `CommandRegistry` instance (constructs it internally) | Consumers register commands via `host.Registry.Register(cmd)` after construction. This matches spec §5.5 API shape. |
| 5 | Deferred `HelpCommand`/`AgentGuideCommand` TUI viewers | These require Terminal.Gui Views (markdown rendering, interactive help). The API is proven without them; consumers can register their own. `MetadataHelpProvider` handles --help output. |
| 6 | `OpenCliWriter` uses hand-built StringBuilder JSON | AOT-friendly per spec §8.3. No JsonSerializerContext needed for the introspection format. |
| 7 | `--cat` is a framework flag (not per-command option) | ArgParser handles `--cat` at framework level, sets `CommandRunOptions.Cat`. CliHost calls `IViewerCommand.RenderCatAsync` when `Cat=true`. MarkdownClet no longer declares `--cat` in its Options list. |
| 8 | `--no-browse` is a consumer global option (not per-command) | Registered via `CliHostOptions.GlobalOptions`. Accessed via `options.HasExtension("no-browse")`. Same for `--allow-file` (repeatable) and `--allow-binary` (flag). |
| 9 | `list` command removed — use `--opencli` or `--help` | The old `CommandLineRoot` handled `list` inline. CliHost provides `--opencli` (JSON registry dump per OpenCLI spec) and `--help` (human-readable). No separate `list` command needed. |
| 10 | clet keeps its own `InputCletRunner` (wraps library's `InputCommandRunner` concept) | clet's runner adds `CletStyling.BaseSchemeName` which the library deliberately omits (spec §9.1: library doesn't set scheme). clet-specific styling stays internal. |
| 11 | `FileAccessPolicy` and `MarkdownContentResolver` stay internal to clet | These are clet-specific security/content-resolution helpers. They use `CommandRunOptions.Extensions` to access `--allow-file`/`--allow-binary` values. Not promoted to library. |

## Spec Discrepancies / Feedback

| # | Issue | Notes |
|---|---|---|
| 1 | Spec §5.5 shows `ReplaceBuiltInCommand` on `CliHostOptions` | Not implemented yet since built-in commands (HelpCommand, AgentGuideCommand) are deferred. Will add when those are implemented. |
| 2 | ~~`MarkdownRenderer` utility deferred~~ | **RESOLVED**: TG #5388 added `Markdown.RenderToAnsi()`. clet now uses it directly in `MarkdownHelpRenderer` (commit `2157025`). |
| 3 | C# `\x` escape greedy parsing | When writing test literals like `"\x1bb"`, the `\x` consumes up to 4 hex digits, so `b` becomes part of the escape. Must use `\u001b` + string concatenation for hex escapes followed by hex characters in tests. |
| 4 | ~~`--cat` should be documented as framework flag in spec~~ | **RESOLVED**: Already present in spec §6.1 table at line 524. |
| 5 | ~~`list` command removal needs spec update~~ | **RESOLVED**: No stale `list` command references exist in the spec. |
| 6 | Consumer global options vs per-command options | The spec doesn't clearly distinguish between options owned by the framework, options registered by the consumer app as globals (like --allow-file), and per-command options. In practice: framework flags are hard-coded in ArgParser; consumer globals go in Extensions; per-command options go in CommandOptions. |

## Files Created (25 source + 8 test)

### Library (`src/Terminal.Gui.Cli/`)
- `Terminal.Gui.Cli.csproj`
- `Properties/AssemblyInfo.cs`
- `Abstractions/CommandKind.cs`
- `Abstractions/CommandStatus.cs`
- `Abstractions/CommandOptionDescriptor.cs`
- `Abstractions/CommandResult.cs`
- `Abstractions/ICliCommand.cs`
- `Abstractions/ICliCommandGeneric.cs`
- `Abstractions/IViewerCommand.cs`
- `Abstractions/ICommandRegistry.cs`
- `Abstractions/CommandRunOptions.cs`
- `Registry/CommandRegistry.cs`
- `Hosting/GlobalOptionDescriptor.cs`
- `Hosting/ExitCodes.cs`
- `Hosting/ArgParser.cs`
- `Hosting/CliHostOptions.cs`
- `Hosting/CliHost.cs`
- `Hosting/InputCommandRunner.cs`
- `Output/JsonEnvelope.cs`
- `Output/CliJsonContext.cs`
- `Output/TypeNames.cs`
- `Output/ResultWriter.cs`
- `Output/OpenCliWriter.cs`
- `Help/IHelpProvider.cs`
- `Help/MetadataHelpProvider.cs`
- `Security/TerminalEscapeSanitizer.cs`

### Tests (`tests/Terminal.Gui.Cli.Tests/`)
- `Terminal.Gui.Cli.Tests.csproj`
- `StubCommand.cs`
- `CommandRegistryTests.cs`
- `ExitCodesTests.cs`
- `TypeNamesTests.cs`
- `JsonEnvelopeTests.cs`
- `TerminalEscapeSanitizerTests.cs`
- `CommandRunOptionsTests.cs`
- `ArgParserTests.cs`
- `CliHostTests.cs`
