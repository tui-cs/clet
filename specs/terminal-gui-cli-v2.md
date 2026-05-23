# Terminal.Gui.Cli Library Specification

> Definitive implementation spec for `gui-cs/Terminal.Gui.cli`: a `Terminal.Gui` library that lets applications expose Views as scriptable CLI commands with typed JSON output, POSIX exit codes, and AI-agent discoverability.

## 0. Repo Identity

`gui-cs/Terminal.Gui.cli` is a new gui-cs library repo for the NuGet package `Terminal.Gui.Cli`. The repo slug intentionally uses lowercase `cli`; the package, assembly, and namespace use Pascal-case `Cli`. Before writing library code, scaffold the repo by cloning the structure and maintenance model of `gui-cs/Editor` (the Terminal.Gui.Editor repo):

- `develop` is the integration branch; `main` is the stable release branch.
- Copy and adapt `specs/constitution.md`; it is the highest-authority engineering document.
- Copy and adapt `CLAUDE.md`, `AGENTS.md`, `.editorconfig`, `Directory.Build.props`, `Directory.Build.targets`, solution naming, and test conventions.
- Copy and adapt CI/CD workflows from Editor: `ci.yml`, `release.yml`, `prepare-release.yml`, and `finalize-release.yml`.
- Keep Editor's zero-warning policy, xUnit v3 executable tests, develop prerelease versioning, main/tag stable releases, and release-PR workflow.

Repository/package identity:

| Concern | Value |
|---------|-------|
| GitHub repo | `gui-cs/Terminal.Gui.cli` |
| Package ID | `Terminal.Gui.Cli` |
| Root namespace | `Terminal.Gui.Cli` |
| Primary TFM | `net10.0` |
| Dependencies | `Terminal.Gui` only |
| License | MIT |
| AOT | `<IsAotCompatible>true</IsAotCompatible>` |

## 1. Problem Statement

Many Terminal.Gui apps need the same hosting layer: parse command-line args, resolve an alias, initialize Terminal.Gui, run a View, serialize a typed result, emit predictable exit codes, and provide enough self-description for humans and AI agents. `clet` proved this pattern with 18 commands and 500+ tests, but the infrastructure should be a reusable library rather than app-specific hosting code.

`Terminal.Gui.Cli` supplies that reusable layer. A consumer creates a `CliHost`, registers command instances, and calls `RunAsync`. The library owns framework flags, Terminal.Gui lifecycle, result writing, JSON envelope shape, exit-code mapping, help metadata, and OpenCLI introspection. Consumers own their concrete commands and any domain policies.

## 2. Design Principles

1. **The library owns Terminal.Gui lifecycle.** Commands receive an initialized `IApplication`; commands never call `Application.Create()`, `app.Init()`, or `Application.Init()`.
2. **No reflection; NativeAOT from day one.** Commands self-describe via properties. JSON serialization uses source generation. Registration is explicit.
3. **Registry stores instances.** Commands are constructed by the consumer and registered as instances, supporting DI, conditional registration, and plugins.
4. **Three-tier option model.** Framework flags are hard-coded in `ArgParser`; consumer globals are declared in `CliHostOptions.GlobalOptions` and land in `CommandRunOptions.Extensions`; per-command options are declared by `CommandOptionDescriptor` and land in `CommandRunOptions.CommandOptions`.
5. **JSON envelope is the stable wire contract.** `{ schemaVersion, status, value?, code?, message? }` is controlled by the library and append-only within schema v1.
6. **Exit codes are library-controlled.** Commands return `CommandResult`; the library maps status/error code to POSIX-conventional process codes.
7. **AI discoverability is first-class.** `llms.txt`, `agent-guide`, and `--opencli` are normative parts of the model.
8. **No general-purpose CLI ambitions.** This library is for Terminal.Gui-backed interactive commands, not a replacement for System.CommandLine.

## 3. Engineering Constitution

`specs/constitution.md`, cloned and adapted from `gui-cs/Editor`, is canonical. It must include these Terminal.Gui.Cli-specific rules:

| Rule | Requirement |
|------|-------------|
| C1 | Only `CliHost` calls Terminal.Gui lifecycle APIs and disposes `IApplication`. |
| C2 | Public API changes require this spec to be updated in the same PR. |
| C3 | No reflection-based command discovery or runtime code generation. |
| C4 | Source-generated JSON only for library-owned JSON types. |
| C5 | All correctness tests run in parallel unless a test explicitly opts out for a process-global. |
| C6 | Commands never call `Environment.Exit`; return `CommandResult`. |
| C7 | Schema v1 is append-only within library major version 1.x. |
| C8 | Zero warnings in Debug and Release. |

The constitution must also document two narrow file-layout exceptions from the proven API: `CommandResult` and `CommandResult<T>` live together in `CommandResult.cs`, and `ICliCommand<TValue>` lives in `ICliCommandGeneric.cs` because generic type syntax cannot be represented directly in a portable filename.

## 4. Public API Surface

All public API lives in namespace `Terminal.Gui.Cli`. XML comments below are normative.

### 4.1 Command model

```csharp
namespace Terminal.Gui.Cli;

/// <summary>The two kinds of CLI commands the library knows about.</summary>
public enum CommandKind
{
    /// <summary>An interactive command that returns a typed value.</summary>
    Input,

    /// <summary>An interactive or headless command that does not return a typed result value.</summary>
    Viewer
}

/// <summary>Outcome status of a command run.</summary>
public enum CommandStatus
{
    /// <summary>The command completed successfully.</summary>
    Ok,

    /// <summary>The user or caller cancelled the command.</summary>
    Cancelled,

    /// <summary>The command failed.</summary>
    Error,

    /// <summary>The command completed but produced no result.</summary>
    NoResult
}

/// <summary>Metadata descriptor for a per-command option.</summary>
public sealed record CommandOptionDescriptor (
    string Name,
    string? ShortName,
    Type ValueType,
    string Description,
    bool Required,
    string? DefaultValue);

/// <summary>Non-generic result for dispatch and output formatting.</summary>
public readonly record struct CommandResult (
    CommandStatus Status,
    object? Value,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>Typed result returned by input commands.</summary>
public readonly record struct CommandResult<T> (
    CommandStatus Status,
    T? Value,
    string? ErrorCode,
    string? ErrorMessage);
```

`CommandResult` and `CommandResult<T>` must remain readonly record structs in one file because their conversion semantics are tightly coupled.

### 4.2 Command interfaces

```csharp
using Terminal.Gui.App;

namespace Terminal.Gui.Cli;

/// <summary>A CLI command backed by Terminal.Gui. Implemented by consumer apps and built-ins.</summary>
public interface ICliCommand
{
    /// <summary>The canonical alias shown in help and OpenCLI output.</summary>
    string PrimaryAlias { get; }

    /// <summary>All aliases that resolve to this command. Must include <see cref="PrimaryAlias" />.</summary>
    IReadOnlyList<string> Aliases { get; }

    /// <summary>Human-readable one-line command description.</summary>
    string Description { get; }

    /// <summary>The command kind.</summary>
    CommandKind Kind { get; }

    /// <summary>The CLR type of the value written to the JSON envelope, or <see cref="void" />.</summary>
    Type ResultType { get; }

    /// <summary>Per-command options accepted by this command.</summary>
    IReadOnlyList<CommandOptionDescriptor> Options { get; }

    /// <summary>Whether this command consumes positional arguments.</summary>
    bool AcceptsPositionalArgs => false;

    /// <summary>Validates the --initial value before Terminal.Gui starts.</summary>
    bool TryValidateInitial (string initial, CommandRunOptions options) => true;

    /// <summary>Runs the command after the host has initialized Terminal.Gui.</summary>
    Task<CommandResult> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken);
}

/// <summary>Typed command that returns a value.</summary>
public interface ICliCommand<TValue> : ICliCommand
{
    /// <summary>Runs the command and returns a typed result.</summary>
    new Task<CommandResult<TValue>> RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken);

    async Task<CommandResult> ICliCommand.RunAsync (
        IApplication app,
        string? initial,
        CommandRunOptions options,
        CancellationToken cancellationToken)
    {
        CommandResult<TValue> result = await RunAsync (app, initial, options, cancellationToken);
        return new (result.Status, result.Value, result.ErrorCode, result.ErrorMessage);
    }
}

/// <summary>
/// Viewer command. Viewers can be interactive TUI commands or headless content commands,
/// but they are invoked through the viewer path and default to fullscreen when a TUI is used.
/// </summary>
public interface IViewerCommand : ICliCommand
{
    /// <summary>
    /// Renders content to stdout without launching the TUI. Called when --cat is set.
    /// Return null to indicate --cat is not supported and normal TUI dispatch should continue.
    /// </summary>
    Task<CommandResult?> RenderCatAsync (
        CommandRunOptions options,
        TextWriter stdout,
        CancellationToken cancellationToken) => Task.FromResult<CommandResult?> (null);
}
```

### 4.3 Registry

```csharp
namespace Terminal.Gui.Cli;

/// <summary>Manages alias-to-command lookup.</summary>
public interface ICommandRegistry
{
    /// <summary>Registers a command instance.</summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>PrimaryAlias</c> is not present in <c>Aliases</c>, or any alias is already registered.
    /// </exception>
    void Register (ICliCommand command);

    /// <summary>Resolves an alias case-insensitively.</summary>
    bool TryResolve (string alias, out ICliCommand? command);

    /// <summary>All registered commands in registration order.</summary>
    IReadOnlyCollection<ICliCommand> All { get; }
}

/// <summary>Default case-insensitive, duplicate-rejecting command registry.</summary>
public sealed class CommandRegistry : ICommandRegistry
{
    /// <inheritdoc />
    public void Register (ICliCommand command);

    /// <inheritdoc />
    public bool TryResolve (string alias, out ICliCommand? command);

    /// <inheritdoc />
    public IReadOnlyCollection<ICliCommand> All { get; }
}
```

`PrimaryAlias` matching is case-insensitive. Duplicate alias detection is case-insensitive. Registration failure throws `InvalidOperationException`.

### 4.4 Run options and global option descriptors

```csharp
namespace Terminal.Gui.Cli;

/// <summary>Parsed options bag passed to commands.</summary>
public sealed class CommandRunOptions
{
    /// <summary>Pre-fill value for the View.</summary>
    public string? Initial { get; init; }

    /// <summary>Title override for TUI chrome. --prompt/-p is an alias for --title/-t.</summary>
    public string? Title { get; init; }

    /// <summary>Whether to emit the JSON envelope instead of plain text.</summary>
    public bool JsonOutput { get; init; }

    /// <summary>Cancel after this duration.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Force fullscreen. Input commands otherwise default to inline.</summary>
    public bool Fullscreen { get; init; }

    /// <summary>Render supported viewer content to stdout instead of launching the TUI.</summary>
    public bool Cat { get; init; }

    /// <summary>Write successful command output to this file instead of stdout.</summary>
    public string? OutputPath { get; init; }

    /// <summary>Constrain inline height.</summary>
    public int? Rows { get; init; }

    /// <summary>Positional arguments after the alias.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Per-command option values keyed by long option name without dashes.</summary>
    public IReadOnlyDictionary<string, string> CommandOptions { get; init; }
        = new Dictionary<string, string> ();

    /// <summary>Consumer-registered global option values keyed by long option name without dashes.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Extensions { get; init; }
        = new Dictionary<string, IReadOnlyList<string>> ();

    /// <summary>Gets the last value for a single-value consumer extension, parsed by <paramref name="parser" />.</summary>
    public T? GetExtension<T> (string key, Func<string, T> parser, T? defaultValue = default);

    /// <summary>Gets all values for a repeatable consumer extension.</summary>
    public IReadOnlyList<string> GetExtensionList (string key);

    /// <summary>Returns true when a consumer extension flag or value is present.</summary>
    public bool HasExtension (string key);
}

/// <summary>Describes a consumer-defined global option.</summary>
public sealed record GlobalOptionDescriptor (
    string Name,
    string? ShortName,
    string Description,
    bool IsFlag,
    bool Repeatable = false);
```

### 4.5 Host

```csharp
namespace Terminal.Gui.Cli;

/// <summary>The main entry point. Owns parsing, dispatch, Terminal.Gui lifecycle, and output.</summary>
public sealed class CliHost
{
    /// <summary>Creates a host, applies configuration, creates its registry, and registers built-ins.</summary>
    public CliHost (Action<CliHostOptions>? configure = null);

    /// <summary>The command registry owned by this host. Register consumer commands before RunAsync.</summary>
    public ICommandRegistry Registry { get; }

    /// <summary>Parses args, dispatches a command, writes output, and returns a process exit code.</summary>
    public Task<int> RunAsync (
        string[] args,
        CancellationToken cancellationToken = default,
        TextWriter? stdout = null,
        TextWriter? stderr = null);
}

/// <summary>Configuration options for <see cref="CliHost" />.</summary>
public sealed class CliHostOptions
{
    /// <summary>Application name shown in help, version output, and OpenCLI.</summary>
    public string ApplicationName { get; set; } = "app";

    /// <summary>Version string shown in --version and OpenCLI. Null uses 0.0.0.</summary>
    public string? Version { get; set; }

    /// <summary>Custom help provider. Null uses <see cref="MetadataHelpProvider" />.</summary>
    public IHelpProvider? HelpProvider { get; set; }

    /// <summary>Maximum characters accepted by --initial. Default is 64 KiB.</summary>
    public int MaxInitialChars { get; set; } = 64 * 1024;

    /// <summary>Agent guide embedded resource name or literal markdown. Null disables agent-guide.</summary>
    public string? AgentGuide { get; set; }

    /// <summary>True when <see cref="AgentGuide" /> is an embedded resource name; false when literal content.</summary>
    public bool AgentGuideIsResource { get; set; } = true;

    /// <summary>Consumer-defined global options parsed into <see cref="CommandRunOptions.Extensions" />.</summary>
    public List<GlobalOptionDescriptor> GlobalOptions { get; } = [];

    /// <summary>Replaces a library built-in command before it is registered.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="alias" /> is not a replaceable built-in alias.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the same built-in alias is replaced more than once.</exception>
    public void ReplaceBuiltInCommand (string alias, ICliCommand replacement);
}
```

`CliHost` constructs and owns its `CommandRegistry`. It registers built-ins during construction after applying options: `help` is always registered unless replaced; `agent-guide` is registered only when `AgentGuide` is non-null unless replaced. `ReplaceBuiltInCommand` supports reserved aliases `help` and `agent-guide`; replacements must have the replacement alias in `Aliases`.

### 4.6 Parser

`ArgParser` is public because the proven API contract includes direct parser tests and consumers may validate argument behavior without running a TUI. It remains a small, data-driven parser, not a general CLI framework.

```csharp
namespace Terminal.Gui.Cli;

/// <summary>Data-driven parser for framework flags, consumer globals, and per-command options.</summary>
public sealed class ArgParser
{
    /// <summary>Creates a parser with registered consumer globals and an --initial limit.</summary>
    public ArgParser (List<GlobalOptionDescriptor> globalOptions, int maxInitialChars = 64 * 1024);

    /// <summary>Parses command-line arguments, optionally validating against a resolved command.</summary>
    public ParseResult Parse (string[] args, ICliCommand? command = null);

    /// <summary>Parses duration strings accepted by --timeout: ms, s, m, h.</summary>
    public static bool TryParseTimeout (string input, out TimeSpan timeout);

    /// <summary>Represents the result of parsing arguments.</summary>
    public sealed class ParseResult
    {
        /// <summary>True if parsing succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>Error message when parsing failed.</summary>
        public string? Error { get; init; }

        /// <summary>The command alias, when this is not a root flag.</summary>
        public string? Alias { get; init; }

        /// <summary>The parsed initial value.</summary>
        public string? Initial { get; init; }

        /// <summary>The parsed options bag.</summary>
        public CommandRunOptions? Options { get; init; }

        /// <summary>Root flag detected before command dispatch.</summary>
        public RootFlag? RootFlag { get; init; }

        /// <summary>Creates a failed parse result.</summary>
        public static ParseResult Fail (string error);
    }

    /// <summary>Root flags that exit without command dispatch.</summary>
    public enum RootFlag
    {
        /// <summary>Root --help or -h.</summary>
        Help,

        /// <summary>Root --version.</summary>
        Version,

        /// <summary>Root --opencli.</summary>
        OpenCli
    }
}
```

### 4.7 Help and built-in commands

```csharp
using System.Reflection;
using Terminal.Gui.App;

namespace Terminal.Gui.Cli;

/// <summary>Pluggable help rendering.</summary>
public interface IHelpProvider
{
    /// <summary>Renders root-level help. Return null to use generated fallback text.</summary>
    string? GetRootHelp (ICommandRegistry registry);

    /// <summary>Renders per-command help. Return null to use generated fallback text.</summary>
    string? GetCommandHelp (ICliCommand command);
}

/// <summary>Generates help text from registry metadata.</summary>
public sealed class MetadataHelpProvider : IHelpProvider
{
    /// <inheritdoc />
    public string? GetRootHelp (ICommandRegistry registry);

    /// <inheritdoc />
    public string? GetCommandHelp (ICliCommand command);
}

/// <summary>Reads embedded markdown resources for root, command, and agent help.</summary>
public sealed class EmbeddedMarkdownHelpProvider : IHelpProvider
{
    /// <summary>Creates a provider that reads markdown resources from <paramref name="resourceAssembly" />.</summary>
    public EmbeddedMarkdownHelpProvider (Assembly resourceAssembly);

    /// <inheritdoc />
    public string? GetRootHelp (ICommandRegistry registry);

    /// <inheritdoc />
    public string? GetCommandHelp (ICliCommand command);

    /// <summary>Reads an embedded markdown resource by exact manifest resource name.</summary>
    public string? GetMarkdownResource (string resourceName);
}

/// <summary>Interactive TUI markdown help viewer, with --cat support for ANSI stdout.</summary>
public sealed class HelpCommand : IViewerCommand
{
    /// <summary>Creates a help command that lazily reads command metadata from <paramref name="registry" />.</summary>
    public HelpCommand (ICommandRegistry registry, IHelpProvider helpProvider);

    /// <inheritdoc />
    public string PrimaryAlias { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Aliases { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public CommandKind Kind { get; }

    /// <inheritdoc />
    public Type ResultType { get; }

    /// <inheritdoc />
    public IReadOnlyList<CommandOptionDescriptor> Options { get; }

    /// <inheritdoc />
    public bool AcceptsPositionalArgs { get; }

    /// <inheritdoc />
    public Task<CommandResult> RunAsync (IApplication app, string? initial, CommandRunOptions options, CancellationToken cancellationToken);

    /// <inheritdoc />
    public Task<CommandResult?> RenderCatAsync (CommandRunOptions options, TextWriter stdout, CancellationToken cancellationToken);
}

/// <summary>Non-interactive viewer command that prints the consumer's agent guide.</summary>
public sealed class AgentGuideCommand : IViewerCommand
{
    /// <summary>Creates an agent guide command from resolved markdown content.</summary>
    public AgentGuideCommand (string markdown);

    /// <inheritdoc />
    public string PrimaryAlias { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Aliases { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public CommandKind Kind { get; }

    /// <inheritdoc />
    public Type ResultType { get; }

    /// <inheritdoc />
    public IReadOnlyList<CommandOptionDescriptor> Options { get; }

    /// <inheritdoc />
    public Task<CommandResult> RunAsync (IApplication app, string? initial, CommandRunOptions options, CancellationToken cancellationToken);

    /// <inheritdoc />
    public Task<CommandResult?> RenderCatAsync (CommandRunOptions options, TextWriter stdout, CancellationToken cancellationToken);
}
```

`HelpCommand` uses `MarkdownRenderer` for ANSI output and a Terminal.Gui markdown viewer for TUI mode. [TG-PENDING: Markdown] The TUI renderer depends on the Terminal.Gui markdown APIs described in §13. `AgentGuideCommand` is headless: it returns the guide markdown as the command value so plain output prints the text and `--json` wraps it in the envelope.

### 4.8 Output and JSON

```csharp
namespace Terminal.Gui.Cli;

/// <summary>The stable wire format for CLI output.</summary>
public sealed class JsonEnvelope
{
    /// <summary>Wire schema version. Always 1 for library major version 1.x.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Status string: ok, cancelled, error, or no-result.</summary>
    public string Status { get; init; } = "ok";

    /// <summary>Result value. Omitted when null.</summary>
    public object? Value { get; init; }

    /// <summary>Error code. Omitted when null.</summary>
    public string? Code { get; init; }

    /// <summary>Error message. Omitted when null.</summary>
    public string? Message { get; init; }

    /// <summary>Creates an ok envelope.</summary>
    public static JsonEnvelope Ok (object? value = null);

    /// <summary>Creates a cancelled envelope.</summary>
    public static JsonEnvelope Cancelled ();

    /// <summary>Creates an error envelope.</summary>
    public static JsonEnvelope Error (string code, string message);

    /// <summary>Creates a no-result envelope.</summary>
    public static JsonEnvelope NoResult ();

    /// <summary>Serializes using the source-generated JSON context.</summary>
    public string ToJson ();
}

/// <summary>Formats command results to stdout, stderr, or an output file.</summary>
public static class ResultWriter
{
    /// <summary>Writes <paramref name="result" /> and returns false when output file creation fails.</summary>
    public static bool Write (CommandResult result, bool jsonOutput, TextWriter stdout, TextWriter stderr, string? outputPath = null);
}

/// <summary>Generates an OpenCLI JSON document from registry metadata.</summary>
public static class OpenCliWriter
{
    /// <summary>Generates OpenCLI JSON for the registered commands and framework options.</summary>
    public static string Generate (ICommandRegistry registry, CliHostOptions options);
}
```

`OpenCliWriter` hand-builds JSON with a shared string-escape helper. It must escape command aliases, descriptions, option names, short names, app names, versions, and metadata values.

### 4.9 Utilities

```csharp
namespace Terminal.Gui.Cli;

/// <summary>POSIX-conventional exit codes.</summary>
public static class ExitCodes
{
    /// <summary>Success.</summary>
    public const int Ok = 0;

    /// <summary>Successful command execution with no result.</summary>
    public const int NoResult = 1;

    /// <summary>Usage error: bad command, bad option, or output-file creation failure.</summary>
    public const int UsageError = 2;

    /// <summary>Validation error, equivalent to sysexits EX_DATAERR.</summary>
    public const int ValidationError = 65;

    /// <summary>I/O error, equivalent to sysexits EX_IOERR.</summary>
    public const int IoError = 74;

    /// <summary>Cancelled, equivalent to 128 + SIGINT.</summary>
    public const int Cancelled = 130;

    /// <summary>Maps a command result to a process exit code.</summary>
    public static int FromResult (CommandResult result);
}

/// <summary>Maps CLR types to stable wire-format type names.</summary>
public static class TypeNames
{
    /// <summary>Returns the wire-format name for <paramref name="type" />.</summary>
    public static string WireName (Type type);
}

/// <summary>Strips dangerous terminal escape sequences from untrusted content.</summary>
public static class TerminalEscapeSanitizer
{
    /// <summary>Sanitizes user-supplied content before it reaches a terminal driver.</summary>
    public static string? Sanitize (string? input);

    /// <summary>Sanitizes rendered ANSI, preserving only SGR CSI sequences generated by trusted renderers.</summary>
    public static string SanitizeRenderedOutput (string renderedAnsi);
}

/// <summary>Markdown-to-ANSI helper for help and viewer output.</summary>
public static class MarkdownRenderer
{
    /// <summary>Renders markdown as ANSI to <paramref name="output" /> and sanitizes rendered output.</summary>
    public static void RenderToAnsi (string markdown, TextWriter output);
}
```

[TG-PENDING: Markdown] `MarkdownRenderer` wraps the Terminal.Gui markdown-to-ANSI API described in §13. If the TG API is named `Markdown.RenderToAnsi`, this method is a thin pass-through plus `TerminalEscapeSanitizer.SanitizeRenderedOutput`.

### 4.10 InputCommandRunner

```csharp
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Terminal.Gui.Cli;

/// <summary>Shared boilerplate for input commands that wrap a control in RunnableWrapper.</summary>
public static class InputCommandRunner
{
    /// <summary>Configures, runs, and maps the result from an input command wrapper.</summary>
    public static Task<CommandResult<TValue>> RunAsync<TControl, TRawResult, TValue> (
        IApplication app,
        RunnableWrapper<TControl, TRawResult> wrapper,
        CommandRunOptions options,
        string defaultTitle,
        CancellationToken cancellationToken,
        Func<TRawResult?, CommandResult<TValue>> resultMapper,
        bool addEnterBinding = true)
        where TControl : View, new();

    /// <summary>Configures and runs a wrapper whose raw result is already the output value.</summary>
    public static Task<CommandResult<TValue>> RunAsync<TControl, TValue> (
        IApplication app,
        RunnableWrapper<TControl, TValue> wrapper,
        CommandRunOptions options,
        string defaultTitle,
        CancellationToken cancellationToken,
        bool addEnterBinding = true)
        where TControl : View, new();
}
```

`InputCommandRunner` applies these defaults before `app.RunAsync`: `Title = options.Title ?? defaultTitle`, `Width = Dim.Fill()`, `BorderStyle = LineStyle.Rounded`, `Border.Thickness = new Thickness (0, 1, 0, 0)`, and Enter key binding to `Command.Accept` when requested. Consumers override using standard Terminal.Gui lifecycle, especially `wrapper.Initialized`; the library must not add custom styling callbacks.

## 5. CLI Grammar

```text
<app> [--help|-h]
<app> --version
<app> --opencli
<app> <alias> [positional...] [framework-options] [consumer-global-options] [per-command-options]
<app> <alias> --help|-h|help
<app> help [alias] [--cat]
<app> agent-guide [--json]
```

There is no `list` command. Human listing is `--help`; structured listing is `--opencli`.

### 5.1 Framework flags

| Flag | Short | Value | Target property | Behavior |
|------|-------|-------|-----------------|----------|
| `--json` | `-j` | none | `JsonOutput=true` | Write JSON envelope. |
| `--initial` | `-i` | string | `Initial` | Pre-fill command value; max 64 KiB by default. |
| `--title` | `-t` | string | `Title` | Override TUI title. |
| `--prompt` | `-p` | string | `Title` | Exact alias for `--title` / `-t`. |
| `--timeout` | none | duration | `Timeout` | Positive number with `ms`, `s`, `m`, or `h`. |
| `--fullscreen` | `-f` | none | `Fullscreen=true` | Force fullscreen app model. |
| `--cat` | none | none | `Cat=true` | Ask viewer to render to stdout without TUI. |
| `--output` | `-o` | path | `OutputPath` | Write successful command output to a newly-created file. |
| `--rows` | `-r` | positive int | `Rows` | Constrain inline height. |
| `--opencli` | none | none | root flag | Emit OpenCLI JSON and exit. |

Supported syntax: `--option value`, `--option=value`, short flags listed above, and `--` to end option parsing. Short bundling (`-jf`) is not supported.

### 5.2 Consumer global options

Consumers add `GlobalOptionDescriptor` values to `CliHostOptions.GlobalOptions`. Matching is case-insensitive by long name or one-character short name. For flags, `Extensions[name]` contains one empty string per occurrence. For repeatable value options, all values are appended. For non-repeatable value options, the last value wins.

### 5.3 Per-command options

A token not matched as a framework flag or consumer global is accepted as a per-command option only if it matches the resolved command's `Options` by `Name` or `ShortName`. Values are strings. Unknown options fail with exit code 2. Required/default semantics are metadata the command must enforce; the parser validates presence and value consumption but does not coerce to `ValueType`.

### 5.4 Help interception

`<alias> --help`, `<alias> -h`, and `<alias> help` are intercepted by `CliHost.RunAsync` before `ArgParser.Parse()`. This is required because `--help` is not a per-command option and the parser would otherwise reject it. The command must already be registered; unknown aliases return usage error.

## 6. Execution Pipeline

```mermaid
sequenceDiagram
    participant Main as Program.Main
    participant Host as CliHost
    participant Parser as ArgParser
    participant Registry as CommandRegistry
    participant TG as Terminal.Gui
    participant Command as ICliCommand
    participant Writer as ResultWriter/OpenCliWriter

    Main->>Host: RunAsync(args, ct, stdout, stderr)
    Host->>Host: intercept <alias> --help|-h|help
    Host->>Parser: Parse(args)
    Parser-->>Host: ParseResult

    alt parse failed
        Host-->>Main: write stderr, exit 2
    else root --help / --version / --opencli
        Host->>Writer: write root output
        Host-->>Main: exit 0
    else command dispatch
        Host->>Registry: TryResolve(alias)
        Registry-->>Host: command or null
        Host->>Parser: Parse(args, command)
        Parser-->>Host: validated CommandRunOptions
        Host->>Host: validate positional args and --initial
        Host->>Host: create linked CTS(user token + timeout)

        alt options.Cat && command is IViewerCommand && RenderCatAsync returns result
            Host->>Command: RenderCatAsync(options, stdout, ct)
            Host->>Writer: ResultWriter.Write(result)
            Host-->>Main: ExitCodes.FromResult(result)
        else normal path
            Host->>TG: ConfigurationManager.Enable(All), fallback to None on failure
            Host->>TG: set Application.AppModel
            Host->>TG: Application.Create(); app.Init()
            Host->>Command: RunAsync(app, initial, options, ct)
            Command-->>Host: CommandResult
            Host->>TG: dispose IApplication
            Host->>Writer: ResultWriter.Write(result)
            Host-->>Main: ExitCodes.FromResult(result)
        end
    end
```

`CliHost` handles dispatch inline; there is no separate `CommandDispatcher` class. `OperationCanceledException` maps to `CommandStatus.Cancelled`. Other command or TG initialization exceptions map to `CommandStatus.Error` with code `io`.

## 7. Exit Codes, JSON, and Type Names

### 7.1 Exit codes

| Constant | Value | Meaning | Mapping |
|----------|-------|---------|---------|
| `Ok` | 0 | Success | `CommandStatus.Ok` |
| `NoResult` | 1 | Successful no-result | `CommandStatus.NoResult` |
| `UsageError` | 2 | Usage or generic command error | parse errors, unknown command, output path failure, unknown error code |
| `ValidationError` | 65 | EX_DATAERR | `CommandStatus.Error` with `validation` or `input-too-large` |
| `IoError` | 74 | EX_IOERR | `CommandStatus.Error` with `io` |
| `Cancelled` | 130 | 128 + SIGINT | `CommandStatus.Cancelled` |

### 7.2 JSON envelope

| Status | Required fields | Omitted fields |
|--------|-----------------|----------------|
| `ok` | `schemaVersion`, `status`, optional `value` | `code`, `message` |
| `cancelled` | `schemaVersion`, `status` | `value`, `code`, `message` |
| `error` | `schemaVersion`, `status`, `code`, `message` | `value` |
| `no-result` | `schemaVersion`, `status` | `value`, `code`, `message` |

Fields use camelCase. Null fields are omitted. Plain-text `Ok` output writes values directly; `JsonArray` writes one item per line; `JsonNode` writes JSON; errors write `error: {code}: {message}` to stderr.

### 7.3 TypeNames mapping

| CLR type | Wire name |
|----------|-----------|
| `string` | `string` |
| `int`, `long`, `short` and nullable forms | `int` |
| `decimal`, `double`, `float` and nullable forms | `decimal` |
| `bool` and nullable form | `bool` |
| `DateTime`, `DateOnly` and nullable forms | `date` |
| `TimeOnly` and nullable form | `time` |
| `TimeSpan` and nullable form | `duration` |
| `JsonArray` | `array` |
| `JsonObject` | `object` |
| `JsonNode` | `json` |
| `void` | `none` |
| other | `Type.Name` |

## 8. AI Discovery Model

### 8.1 `llms.txt`

Consumers SHOULD ship `llms.txt` in the repo root and, when applicable, at `https://<domain>/llms.txt`. It is a short orientation document for agents: what the tool does, install instructions, quick start, key flags, and pointers to `agent-guide` and `--opencli`. The library does not generate it.

### 8.2 `agent-guide`

When `CliHostOptions.AgentGuide` is set, `CliHost` registers `AgentGuideCommand` under `agent-guide`. Behavior:

| Invocation | Behavior |
|------------|----------|
| `<app> agent-guide` | Prints the guide text to stdout, exit 0. |
| `<app> agent-guide --json` | Writes JSON envelope with guide text as `value`, exit 0. |
| no configured guide | Command is absent; invoking it is unknown command, exit 2. |

When `AgentGuideIsResource` is true, the value is an embedded resource name resolved from the consumer assembly. When false, it is literal markdown content.

### 8.3 `--opencli`

Every host supports root `--opencli`. It emits OpenCLI JSON with:

- `opencli: "0.1"`
- app `info.title` and `info.version`
- all registered commands in registry order
- aliases, descriptions, options, and exit codes
- metadata entries `kind` (`input`/`viewer`) and `resultType` (`TypeNames.WireName`)
- recursive framework options

Agents should call `--opencli` once per session and cache the result.

## 9. Project Structure

```text
Terminal.Gui.Cli.slnx
Directory.Build.props
Directory.Build.targets
.editorconfig
CLAUDE.md
AGENTS.md
specs/
  constitution.md
  terminal-gui-cli-v2.md
src/
  Terminal.Gui.Cli/
    Terminal.Gui.Cli.csproj
    Abstractions/
      CommandKind.cs
      CommandStatus.cs
      CommandOptionDescriptor.cs
      CommandResult.cs
      ICliCommand.cs
      ICliCommandGeneric.cs       (contains ICliCommand<TValue>)
      IViewerCommand.cs
      ICommandRegistry.cs
      CommandRunOptions.cs
    Registry/
      CommandRegistry.cs
    Hosting/
      ArgParser.cs
      CliHost.cs                  (dispatch is inline; no CommandDispatcher.cs)
      CliHostOptions.cs
      ExitCodes.cs
      GlobalOptionDescriptor.cs
      InputCommandRunner.cs
    Commands/
      HelpCommand.cs
      AgentGuideCommand.cs
    Help/
      IHelpProvider.cs
      MetadataHelpProvider.cs
      EmbeddedMarkdownHelpProvider.cs
      MarkdownRenderer.cs
    Output/
      CliJsonContext.cs
      JsonEnvelope.cs
      OpenCliWriter.cs
      ResultWriter.cs
      TypeNames.cs
    Security/
      TerminalEscapeSanitizer.cs
    Properties/
      AssemblyInfo.cs
tests/
  Terminal.Gui.Cli.Tests/
  Terminal.Gui.Cli.IntegrationTests/
  Terminal.Gui.Cli.SmokeTests/
examples/
  Terminal.Gui.Cli.ExampleApp/
```

## 10. Test Strategy

Port `tests/Terminal.Gui.Cli.Tests` from clet PR #176 as the baseline public API contract. These tests validate parser, registry, JSON envelope, exit codes, type names, sanitizer, host dispatch, help interception, OpenCLI, and consumer global option flow.

| Tier | Project | Scope | Notes |
|------|---------|-------|-------|
| Unit | `Terminal.Gui.Cli.Tests` | Parser, registry, result writer, JSON, OpenCLI, sanitizer, type names, host paths not requiring real TUI interaction | No Terminal.Gui driver initialization except where unavoidable. |
| Integration | `Terminal.Gui.Cli.IntegrationTests` | `CliHost` end-to-end with `Application.Create()`, cancellation, timeout, `InputCommandRunner`, interactive help rendering | Full parallel; no process-global mutation unless isolated by collection. |
| Smoke | `Terminal.Gui.Cli.SmokeTests` | Spawn `examples/Terminal.Gui.Cli.ExampleApp`; verify `--help`, `--version`, `--opencli`, `agent-guide`, JSON output, exit codes | OS matrix, Release build. |

Write fresh tests for `HelpCommand`, `AgentGuideCommand`, `ReplaceBuiltInCommand`, `EmbeddedMarkdownHelpProvider`, `MarkdownRenderer`, and smoke tests tied to the new repo/example app. Tests run with `dotnet run --project tests/<project>`.

## 11. CI/CD

Clone from `gui-cs/Editor` and adapt names.

| Workflow | Trigger | Responsibilities |
|----------|---------|------------------|
| `ci.yml` | PRs and branch pushes | Restore, build Debug/Release as appropriate, format verification, unit/integration/smoke tests on ubuntu/macos/windows, AOT publish example app. |
| `release.yml` | `develop` push and `v*` tags | Compute version, Release build/test, pack, NuGet push with skip-duplicate, GitHub release artifacts for tags. |
| `prepare-release.yml` | manual dispatch | Compute next beta/rc/stable version, create release branch, open release PR to `main`. |
| `finalize-release.yml` | release PR merged | Create annotated tag/GitHub Release, delete release branch, open main-to-develop back-merge PR. |

Versioning follows Editor: base `<Version>` in `Directory.Build.props`; develop builds append GitHub run number to a `-develop` base; tag builds strip leading `v`; `<TerminalGuiVersion>` pins the Terminal.Gui dependency and can be overridden in CI.

## 12. Scope Boundaries

| Out of scope | Reason |
|--------------|--------|
| Concrete consumer commands such as clet's `select`, `md`, `edit`, or config tools | Consumers own domain commands. |
| FileAccessPolicy / file access settings | clet-specific AI-agent threat model. |
| MarkdownContentResolver | clet-specific file and stdin resolution. |
| InputCletRunner | clet-specific styling wrapper around `InputCommandRunner`. |
| CletStyling / scheme names | App-specific visual policy. |
| General-purpose markdown file browser command | Library provides help/agent guide primitives, not file browsing. |
| DI container | Consumers construct commands however they want. |
| Reflection/source-generated command discovery | Explicit registration is the contract. |
| Logging abstraction | Consumers may add logging around their commands. |
| Environment-variable option fallback or short-option bundling | Not required for proven API; keep parser small. |
| Owning consumer config files | Consumers set `ConfigurationManager.AppName`; library only enables/falls back during dispatch. |

## 13. External Terminal.Gui Dependencies

| Marker | Dependency | Required by | Acceptance criteria | Fallback |
|--------|------------|-------------|---------------------|----------|
| `[TG-PENDING: Markdown]` | Public Terminal.Gui markdown-to-ANSI rendering API and markdown View suitable for help display | `MarkdownRenderer`, `HelpCommand` | A pinned `TerminalGuiVersion` exposes stable APIs that render markdown to ANSI and display markdown interactively without private reflection. | If interactive markdown View is unavailable, `HelpCommand` may use a read-only text View with ANSI/plain markdown content; if ANSI rendering changes name, `MarkdownRenderer` adapts internally without changing public API. |
