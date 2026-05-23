# clet design decisions log

Cross-cutting decisions that don't fit cleanly into a single milestone issue or a single spec section. **New entries go at the bottom** (chronological order) to avoid merge conflicts. Each entry is short by design — context, decision, status, and a pointer to where it took effect.

When a decision changes, **don't edit the entry** — append a new one at the bottom that supersedes the old, and mark the old one `Superseded by #N`. The log is append-only so future agents can see what was tried.

Format: `## D-NNN: <short title> (status)`. Status is one of `Active`, `Superseded by D-NNN`, `Reversed`, or `Pending`.

---

## D-001: No `type` field on the JSON wire envelope (Active)

**Context.** Press release originally showed `{"status":"ok","type":"System.String","value":"prod"}`. CLR-typed `type` field leaked .NET into a cross-language wire format that AI agents in any language consume.

**Decision.** Drop `type` from the result envelope entirely. Result types are advertised once per alias by `clet list --json`; consumers cache the registry once per session and don't branch on per-call type names.

**Status.** Active. Locked at schema v1 (spec §4.3, §4.3.1).

**Pointers.** `specs/clet-spec.md` §4.3, §4.3.1. `README.md` lines 70 and 80 (envelope examples). `src/Clet/Json/SchemaV1.cs`.

---

## D-002: Cancel envelope is `{"schemaVersion":1,"status":"cancelled"}` regardless of TG behavior (Active)

**Context.** TG #5157 (now landed on develop) leaves "disposition of `IValue<T>.Value` after cancel" as a TG-internal decision. Clet's wire contract was at risk of being coupled to that decision.

**Decision.** Decouple. On cancel, clet emits exactly `{"schemaVersion":1,"status":"cancelled"}` — no `value`, no `code`, no partial result — regardless of whether the underlying View's `IValue<T>.Value` is readable. TG's eventual answer is welcome but not load-bearing.

**Status.** Active. Locked in spec §3.1 and §4.3.

**Pointers.** `specs/clet-spec.md` §3.1, §4.3. `src/Clet/Json/SchemaV1.cs` `Cancelled()` factory.

---

## D-003: `range` clet emits `{"low": <T>, "high": <T>}` (Active)

**Context.** Spec §9 originally listed the `range` `value` shape as an open question (tuple vs object vs separate fields).

**Decision.** Named object: `{"low": <T>, "high": <T>}` where `<T>` matches the range's underlying numeric/date/time type. Stable JSON across languages, self-documenting, easy to extend later (e.g., add `step`).

**Status.** Active. Locked at v0.5 schema-lock per spec §4.3.2.

**Pointers.** `specs/clet-spec.md` §4.3.2.

---

## D-004: Source generator deferred — `BuiltInClets.RegisterAll` hand-written (Superseded by D-021)

**Context.** Spec §4.4 specifies a Roslyn source generator (`src/Clet.SourceGen`) that emits `BuiltInClets.RegisterAll(ICletRegistry)` from `[Clet("alias", typeof(TResult))]` attributes. The generator project landed in v0.1 as a placeholder; the actual generator is not implemented.

**Decision.** Hand-write `Registry/BuiltInClets.cs` for now. As clets are added (v0.3 wave), keep registering them manually until the generator earns its keep. Bar-raise critique #11 questioned whether the generator is worth its complexity at all.

**Status.** Superseded by [D-021](#d-021-auto-discovered-clets-any-ivaluet-view-just-works-deferred-to-v2-active). The "revisit before v0.3 GA" trigger fired with the answer: don't bother in v1.x. The auto-discovery question is broader than just the source generator; D-021 captures the full design exploration and the deferral to v2.

**Pointers.** `src/Clet/Registry/BuiltInClets.cs`, `src/Clet.SourceGen/Placeholder.cs`. Bar-raise [#BR-11 in the bar-raise backlog issue](https://github.com/gui-cs/clet/issues/11) ticked.

---

## D-005: Non-generic `IClet.RunBoxedAsync` via default interface methods (Active)

**Context.** The host needs to dispatch any clet without knowing its `TValue` at compile time, but reflection-based generic dispatch isn't AOT-clean (and AOT lands at v0.3).

**Decision.** Add a non-generic `Task<BoxedCletResult> RunBoxedAsync(...)` to `IClet`. Provide it as a default interface method on `IClet<T>` and `IViewerClet` that wraps the typed `RunAsync`. Concrete clets (`SelectClet`) get it for free; the host calls through `IClet`.

**Status.** Active. `BoxedCletResult` is `(CletRunStatus, object? Value, string? ErrorCode, string? ErrorMessage)`.

**Pointers.** `src/Clet/Abstractions/IClet.cs`, `src/Clet/Abstractions/BoxedCletResult.cs`, `src/Clet/Abstractions/IViewerClet.cs`.

---

## D-006: Hand-rolled CLI parser at v0.11 instead of System.CommandLine (Active)

**Context.** Spec §4.6 names `System.CommandLine` (SCL) as the CLI root. SCL is in long-running beta and its API has churned across `2.0.0-beta4`, `beta5`, `beta6` (2022–2025).

**Decision.** Hand-roll a ~300-line parser at v0.11 (`src/Clet/Hosting/CommandLineRoot.cs`). The v0.11 surface is small: `--help`, `--version`, `help <alias>`, `list [--json]`, `<alias> [initial] [--json] [--timeout] [--<opt> <value>]`. The dependency churn isn't worth it for this surface size.

**Status.** Active. The hand-rolled parser has handled all 15 clets plus global flags cleanly; no plan to revisit. If the surface ever grows enough to justify a library, the call site is one constructor in `Program.Main`.

**Pointers.** `src/Clet/Hosting/CommandLineRoot.cs`.

---

## D-007: TUIcast keystroke smoke deferred from v0.11 to v0.3 (Active)

**Context.** Spec §5.3 / §6.3 specify TUIcast (PTY-based, deterministic-script keystroke driver) as the process-level smoke harness. Issue #9 (v0.11) initially listed all six smoke cases including a `clet select --json` happy-path that requires an `Enter` keystroke through a PTY.

**Decision.** Land five of the six smoke cases at v0.11 using `Process.Start` (no PTY: `--version`, `--help`, `list --json`, `help select`, `help <unknown>`). Defer the keystroke-driven cases (happy-path Enter, `--timeout 100ms` cancel envelope) to v0.3, where 13 more clets land at the same time and TUIcast pays for its dependency cost. The cancel/timeout *behavior* is unit-tested at v0.11 (`OutputFormatterTests`, `CommandLineRootTests`, `ExitCodesTests`); only the process-level wiring is deferred.

**Status.** Active. TUIcast is not yet wired; the `[Fact(Skip=...)]` test and `tests/Clet.SmokeTests/scripts/select.txt` placeholder are in place. All 14 input clets have landed but the keystroke-driven smoke cases still await TUIcast integration.

**Pointers.** [Issue #9](https://github.com/gui-cs/clet/issues/9), `tests/Clet.SmokeTests/CletSmokeTests.cs` (the deliberately `[Fact(Skip=...)]` test).

---

## D-008: `select` returns text, not zero-based index (Active)

**Context.** Spec §4.3.2 says `select` value shape is `integer` (zero-based index of the selected item).

**Decision.** The v0.1 implementation returns the selected label text (`string?`), not the index. Rationale: shell scripts and AI agents almost always want the label, not the index. The index is recoverable from the choices list if needed. This is the shipped behavior since v0.1.

**Status.** Active. Locked at v0.5 schema-lock.

**Pointers.** `src/Clet/Clets/Input/SelectClet.cs`.

---

## D-009: `multi-select` returns array of strings, not indices (Active)

**Context.** Spec §4.3.2 says `multi-select` value shape is `array of integers (zero-based indices, ascending)`.

**Decision.** Return array of selected label texts as a `JsonArray` of strings, not indices. Same reasoning as D-008: shell scripts and AI agents almost always want the label, not the index. Consistent with `select` returning text.

**Status.** Active. Locked at v0.5 schema-lock.

**Pointers.** `src/Clet/Clets/Input/MultiSelectClet.cs`.

---

## D-010: `pick-file`/`pick-directory` run inline, not fullscreen (Active)

**Context.** Spec implies file dialogs need fullscreen because `OpenDialog` is a TG `Dialog`. Early draft plan proposed adding a `RequiresFullscreen` property to `IClet`.

**Decision.** File picker clets render inline by default with `Width = Dim.Fill, Height = Dim.Fill`. Users who want fullscreen pass `--fullscreen` (already handled by `AliasDispatcher` via `options.Fullscreen`). No `RequiresFullscreen` property added to `IClet` — the existing `--fullscreen` flag is sufficient.

**Status.** Active.

**Pointers.** `src/Clet/Clets/Input/PickFileClet.cs`, `src/Clet/Clets/Input/PickDirectoryClet.cs`, `src/Clet/Hosting/AliasDispatcher.cs` (line 39 already checks `options.Fullscreen`).

---

## D-011: `range` is integer-only at v0.3 (Superseded by D-032)

**Context.** Spec §4.3.2 defines the `range` value shape as `{"low": <T>, "high": <T>}` where `<T>` is the scalar of the underlying numeric/date/time type.

**Decision.** At v0.3, `T = int` only. The `RangeClet` uses two `NumericUpDown<int>` controls. Decimal range and date/time range are deferred until demand exists — adding them later is a new clet alias or a generic type parameter, not a breaking change to the existing wire format.

**Status.** Superseded by D-032. The `range` clet itself is gone; `linear-range` replaces it with an option-label string type, so the integer-only constraint no longer applies.

**Pointers.** *(Files referenced here have been removed; see D-029.)*

---

## D-012: Code signing deferred post-1.0 (Active)

**Context.** Spec §5.2 calls for macOS (Developer ID + notarization) and Windows (Authenticode) code signing in the release pipeline. Apple Developer Program costs $99/yr; Azure Trusted Signing costs ~$10/mo. Homebrew bottles require signed binaries for Gatekeeper; unsigned binaries get quarantined.

**Decision.** Defer all code signing until after v1.0, when adoption numbers justify the cost. At v0.5/v1.0:
- Homebrew ships a **build-from-source formula** (no bottles, no signing needed; user's machine compiles via `dotnet publish`).
- `dotnet tool install -g clet` works without signing (NuGet packages aren't gated by OS code signing).
- WinGet can ship unsigned `.exe` with a SmartScreen warning; acceptable for early adopters.
- Skip `scripts/sign-macos.sh` and `scripts/sign-windows.ps1` steps in release workflows.

Revisit when download numbers show users hitting Gatekeeper/SmartScreen friction, or when a corporate adopter requires signed binaries.

**Status.** Active. Only three secrets needed at v0.5: `CLET_DISPATCH_PAT`, `NUGET_API_KEY`, `HOMEBREW_TAP_TOKEN`.

**Pointers.** Spec §5.2 (build matrix signing steps), §5.4 (publish steps). `gui-cs/homebrew-tap` repo (must be created before v0.5).

---

## D-013: All clet wrappers/dialogs render with `Schemes.Base` (Active)

**Context.** By default a `RunnableWrapper` inherits the surrounding scheme; an `OpenDialog` calls `FileDialog.SetStyle()` which forces `SchemeName` to `Schemes.Dialog` once the dialog enters its running state — even if we set `Schemes.Base` in the object initializer. Without intervention, file/directory pickers render with a different palette than the other 12 inline clets.

**Decision.** All clets set `SchemeName = CletStyling.BaseSchemeName` (resolves `SchemeManager.SchemesToSchemeName(Schemes.Base)`) on their wrapper/dialog. For `pick-file` and `pick-directory`, an `IsRunningChanged` handler re-applies `Schemes.Base` once the dialog has actually started running, working around `FileDialog.SetStyle()`'s `Base → Dialog` rewrite. The handler is **load-bearing** — deleting it silently regresses the file pickers to the dialog scheme.

**Status.** Active. Revisit when Terminal.Gui exposes a way to opt out of `FileDialog.SetStyle()`'s scheme rewrite, at which point the handler can be replaced with the simpler initializer-only form.

**Pointers.** `src/Clet/Hosting/CletStyling.cs`, `src/Clet/Clets/Input/PickFileClet.cs` (IsRunningChanged handler), `src/Clet/Clets/Input/PickDirectoryClet.cs` (same).

---

## D-014: `--title` is a built-in CLI flag, not a per-clet option (Active)

**Context.** Every input clet renders its `RunnableWrapper`/`OpenDialog` with a `Title` and falls back to a per-clet default ("Select an option…", "Enter a number…", etc.). All 14 clets honor `CletRunOptions.Title` if set. The CLI parser, however, had no way to populate it — `--title` was being routed into the per-clet `--<opt>` bucket where most clets ignored it. Additionally, `ConfirmClet` had a per-clet `--prompt` option that overrode `--title`, making the two flags behave inconsistently across clets.

**Decision.** `--title <text>` (and its alias `--prompt <text>`) is parsed at the host level (`CommandLineRoot.DispatchAlias`) alongside `--initial`, `--json`, `--timeout`, `--fullscreen`, and stored as `CletRunOptions.Title`. `--prompt` / `-p` is a synonym for `--title` / `-t` — both set the same value. Individual clets do **not** declare `title` or `prompt` in their `Options` list — adding it 14 times would be churn and the per-clet help would falsely imply each clet handles it differently.

**Status.** Active. Listed in root help (§4.7).

**Pointers.** `src/Clet/Hosting/CommandLineRoot.cs` (`DispatchAlias` parsing + `WriteRootHelp`), `src/Clet/Abstractions/CletRunOptions.cs` (Title property), each clet's `Title = options.Title ?? "default"` line.

---

## D-015: `clet md` content source is file arguments + stdin at v0.5 (Active)

**Context.** Spec §9 open question #4 asks whether `clet md` takes file arguments (`clet md README.md`), stdin (`cat README.md | clet md`), or both.

**Decision.** Both, with the following precedence:
1. File arguments in `options.Arguments` — treated as file paths or glob patterns, expanded and read.
2. Inline content via `--initial` — rendered directly as Markdown text.
3. Stdin if redirected (`Console.IsInputRedirected`) — read to end and render.
4. If none, return `Error("io", "No file specified.")`.

The file expansion logic (glob support, file-not-found warnings) is adapted from mdv's `ExpandFiles()`. Multi-file support uses a `DropDownList` in the status bar, also adapted from mdv.

**Why:** Both input methods are expected by shell users and AI agents. File args are the primary use case; stdin enables piping.

**How to apply:** This resolves spec §9 question #4. The content resolution precedence order is fixed for v1.0.

**Status.** Active. Resolves spec §9 open question #4.

**Pointers.** `src/Clet/Clets/Viewer/MarkdownClet.cs` (content resolution logic).

---

## D-016: Help rendering uses print mode, not interactive fullscreen (Active)

**Context.** Spec §4.7 says help is "surfaced through the same dismissable, themed, scrollable viewer experience" as `clet md`. Taken literally, `clet --help` would open an interactive fullscreen TUI that blocks until the user presses `q`. This conflicts with CLI conventions: help must work in pipes (`clet --help | less`), must not block for user interaction, and AI agents read stdout non-interactively.

**Decision.** `clet --help` and `clet help <alias>` render Markdown to ANSI escape sequences and write to stdout, then exit immediately. "Same code path" means the same `Markdown` rendering engine (Terminal.Gui's `Markdown` View with `TextMateSyntaxHighlighter`), not the same interactive fullscreen mode. The print-mode pipeline is adapted from mdv's `RenderMarkdown()`. Root help reads from an embedded `overview.md` resource; per-alias help is generated dynamically from `IClet` metadata.

**Why:** CLI help must be non-interactive for pipes, redirection, and AI agent consumption.

**How to apply:** Any future help-related changes should keep the print-mode pipeline. If interactive help browsing is desired, it should be a separate command (e.g., `clet browse-help`), not the default for `--help`.

**Status.** Active. Spec §4.7 should be read as "same rendering engine" rather than "same interactive mode."

**Pointers.** `src/Clet/Hosting/MarkdownHelpRenderer.cs`, `src/Clet/Hosting/CommandLineRoot.cs` (WriteRootHelp, WriteAliasHelp), `src/Clet/Help/overview.md`.

---

## D-017: Link safety default is SurfaceOnly for `clet md` (Active)

**Context.** Spec Appendix A defines a `SurfaceOnly` link policy: hyperlinks are displayed but never opened automatically. The mdv reference viewer already implements this pattern — `LinkClicked` shows the URL in the status bar and sets `e.Handled = true`. AI agents need predictable, safe behavior when running `clet md` on untrusted Markdown.

**Decision.** Default link behavior is SurfaceOnly: clicking a link shows the URL in the status bar, nothing more. A future `--allow-link-open` clet option can opt in to opening links in the default browser. Not implemented at v0.5 — the safe default ships first.

**Status.** Active.

**Pointers.** `src/Clet/Clets/Viewer/MarkdownClet.cs` (LinkClicked handler), spec Appendix A.

---

## D-018: ASCII logo wired into `--help` banner and README hero section (Active)

**Context.** [Issue #12 (branding)](https://github.com/gui-cs/clet/issues/12) approved the three-line box-drawing logo and tagline "One binary. Every prompt. JSON out. Go home." and called for the logo to be wired into `clet --help` and the README hero section.

**Decision.** The ASCII logo is prepended to the Markdown-rendered `--help` output (embedded in `src/Clet/Help/overview.md`), before the tagline/description and usage block. The README `## Press Release` heading is preceded by a full hero section: hero image, code-block logo, tagline, install commands, comparison table, and usage examples (human + AI agent). Spec §4.7 updated to document the `--help` banner format. The logo is also the canonical visual identity for all documentation.

**Status.** Active. Logo, tagline, and install commands are locked as of this PR. GIF/asciinema demo placeholder lands in the README; actual recording defers to v0.3 (issue #3).

**Pointers.** `src/Clet/Help/overview.md` (logo in Markdown template), `README.md` (hero section), `specs/clet-spec.md` §4.7.

---

## D-019: Distribute clet as a single-project `dotnet tool` (mdv pattern) (Active)

**Context.** Spec §5.4 originally hand-waved at "the `Clet.Tool` project (which references `Clet` and packages the build output as a global tool)" — but no such project exists in the repo, and there is no need for one. The sibling [`gui-cs/mdv`](https://github.com/gui-cs/mdv) viewer ships as a single-csproj global tool: `<PackAsTool>true</PackAsTool>` + `<ToolCommandName>mdv</ToolCommandName>` + `<PackageId>Terminal.Gui.mdv</PackageId>` directly on the executable's csproj. Install command is `dotnet tool install -g Terminal.Gui.mdv`. clet should adopt the same pattern: a single csproj that produces both the AOT single-file binary (for Homebrew/WinGet) and a `dotnet tool` package (for the cross-platform `dotnet tool install` path).

**Decision.** Add `PackAsTool`, `ToolCommandName=clet`, and a `PackageId` directly to `src/Clet/Clet.csproj`. Pack the README and LICENSE into the NuGet package via `<None Include="..." Pack="true" PackagePath="/" />`. No separate `Clet.Tool` project. The AOT binary continues to be produced by `dotnet publish -c Release` against the same csproj — `PackAsTool` only affects `dotnet pack` output, not `dotnet publish`. End users on any platform with the .NET SDK can install via `dotnet tool install -g <package-id>` and invoke `clet` from PATH. *(The original choice of `PackageId=Terminal.Gui.clet` to match the `Terminal.Gui.mdv` precedent has been superseded by [D-024](#d-024-package-id-is-bare-clet-active) — the bare `clet` id is now used.)*

**Why:** mdv has already proven the single-csproj approach works for a Terminal.Gui-based tool, and a separate `Clet.Tool` project would add a layer of indirection (project reference, build orchestration, second csproj to keep in sync) that earns nothing. The spec's earlier "Clet.Tool project" phrasing was aspirational — never built.

**How to apply:** §5.4 ".NET tool (NuGet)" describes this packaging in concrete terms (properties to set, exact `dotnet pack` / `dotnet tool install` commands). The current install hint is `dotnet tool install -g clet` (per D-024). v0.5 milestone exit criterion (§7) requires `dotnet pack` + local `dotnet tool install` to work end-to-end before the channels-live exit criteria for v1.0 GA.

**Pointers.** Spec §5.4, §7 v0.5 row, §10 step 10. README "Install" section. The `mdv.csproj` reference: <https://github.com/gui-cs/mdv/blob/main/mdv.csproj>.

---

## D-020: Continuous-release loop on TG develop + release; channel from version suffix (Active — point 4 superseded by D-022)

**Context.** Spec §5.1 originally fired clet's release workflow on a single trigger: `repository_dispatch type=tg-released` from a TG release tag. That left the §8 develop-pin risk wide open — clet had to hand-pin `Terminal.Gui Version="2.0.2-develop.NN"` and bump manually whenever TG develop changed. It also left clet silent during the long stretches between TG releases, even when develop carries shippable improvements. We want clet to track TG continuously (every develop NuGet publish drives a clet prerelease) **and** still produce stable artifacts on TG release tags (Homebrew, WinGet, NuGet "latest"). See [issue #30](https://github.com/gui-cs/clet/issues/30) for the kicked-off plan.

**Decision.**

1. **Two dispatch types.** TG fires `tg-released` on release tags and `tg-develop-published` on every develop NuGet publish. Both carry `client_payload.tg_version`. clet's workflow accepts both.
2. **Channel from version suffix.** `tg_version` containing `-` ⇒ develop channel; otherwise ⇒ release channel. No separate `channel` field needed — the version string already carries the signal, and SemVer prerelease semantics line up with NuGet's listing rules.
3. **Per-channel publishing.** NuGet runs on both channels; Homebrew and WinGet run on release only. NuGet's prerelease semantics ensure `dotnet tool install -g clet` resolves to stable; `--prerelease` opts into develop.
4. **clet version = TG version verbatim.** No version negotiation, no compatibility matrix; the workflow passes `-p:Version=${{ env.TG_VERSION }}` to pack/publish.
5. **Replace the hard-pinned `<PackageReference>` with an MSBuild variable.** `<PackageReference Include="Terminal.Gui" Version="$(TerminalGuiVersion)" />`, with `<TerminalGuiVersion>` defaulted in the same `PropertyGroup` to a known-good develop build for local development. The workflow passes `-p:TerminalGuiVersion=${{ env.TG_VERSION }}` so the build pulls exactly the TG version named by the dispatch — no version drift between the package label and what's linked.

**Why:** The previous "pin develop, replace with release tag at v0.5" plan put the release schedule in tension with TG's own. Mirroring TG's develop story directly removes that tension; consumers who want stability stay on stable, consumers who want early bits opt in. Publishing the same version string TG uses keeps the 1:1 promise from §5.6 honest in both directions.

**How to apply:** Spec §5.1, §5.4, §5.5, §5.6, §7 v0.5 row, and §8 risks all updated in the same PR. The §8 develop-pin risk row is **resolved**; a new "develop publish volume" row is added in its place. Failure handling distinguishes channel: release failures page, develop failures don't (next develop supersedes within hours; spam-paging on every flake would be untenable).

**Status.** Active. Pending TG-side work: a `notify-clet.yml` workflow on `gui-cs/Terminal.Gui` that fires both dispatches with a `CLET_DISPATCH_PAT` (tracked as a separate TG-side issue).

**Pointers.** Spec §5.1, §5.4, §5.5, §5.6, §7, §8. `src/Clet/Clet.csproj` (`<TerminalGuiVersion>` + variable PackageReference). `.github/workflows/release.yml` (renamed from `release-on-tg.yml` per D-022).

---

## D-021: Auto-discovered clets ("any IValue<T> View just works") deferred to v2 (Active)

**Context.** The original PR/FAQ pitched clet as a way to expose any Terminal.Gui View with `IValue<T>` to the shell automatically. v1.0 ships 15 hand-written clets instead. Spec §11 lays out what we learned, what full auto-discovery would require on both the TG side (a `[Shellable]` attribute or marker, wire-format declaration, initial-value parser, per-View option surface) and the clet side (a real source generator), and the cross-cutting cost (TG core would need to host clet-shaped opinions, schema-lock would couple to TG's `[Shellable]` surface).

**Decision.** Don't pursue full auto-discovery in v1.x. Hand-written `BuiltInClets.RegisterAll` continues through v1.0. `Clet.SourceGen` stays as a placeholder (don't delete — it's a parking spot for the v2 reopen). The `[Clet("alias", typeof(TResult))]` attribute sketched in spec §4.5 is illustrative only; shipped code uses plain `IClet<TValue>` interface implementation. Revisit at v2 if and when third-party clets become a goal — at which point the TG-side asks (§11.3 A–E) become a co-design topic with TG core, with §11 as the starting checklist.

**Why:** 15 clets at ~50–150 lines each ≈ 1500 LOC of mostly-metadata is not the bottleneck. Cross-cutting concerns (`--title`, scheme, link safety, exit codes) already live above the per-clet layer; auto-discovery wouldn't change that. The leverage of full auto-discovery only kicks in if a long tail of new TG Views or third-party Views want shell exposure post-v1.0 — both are explicitly out of scope today (§1, plugin loading exclusion in Appendix A). And introducing a `[Shellable]` attribute on TG core softens the §2 "nothing in TG core knows about clets" decision; that's a TG-side opinion-shift we shouldn't ask for without the v2 third-party-clets driver behind it.

**How to apply:** Spec §11 is the canonical exploration. D-004 (source generator deferred) is **superseded by this entry** — D-004's "Pending — revisit before v0.3 GA" is now closed: the answer is "don't bother in v1.x." Bar-raise [#BR-11](https://github.com/gui-cs/clet/issues/11) ticked. v1.x refinements that pay down clet boilerplate without locking in a TG-side commitment (Options-declaration builder helper, generated §4.3.2 wire-format table, contract test for wire-format conformance) are listed in §11.5 and remain candidates for separate PRs.

**Status.** Active. Supersedes D-004.

**Pointers.** Spec §11 (full exploration), §11.5 (recommendation + v1.x refinements), §11.6 (open questions for v2). `src/Clet.SourceGen/` retained as placeholder. `src/Clet/Registry/BuiltInClets.cs` continues as hand-written. Bar-raise issue [#11](https://github.com/gui-cs/clet/issues/11) #BR-11.

---

## D-022: clet versions independently of Terminal.Gui (Superseded by D-023)

**Context.** The original spec (§1, §5.6, §7) tied clet's version 1:1 to Terminal.Gui's. D-020 point 4 made this explicit: "clet version = TG version verbatim." That made sense when clet was conceived as a TG satellite moving in lockstep. It's now clear this was a bad idea: clet has its own wire contract (`schemaVersion: 1`) that versioning should reflect; TG releases on its own cadence; and the develop NuGet builds (`2.0.0-develop.X`) polluted the package history with TG's version numbers.

**Decision.** clet maintains its own SemVer starting at `1.0.0`. Major bumps = schema version bumps (per §4.3.1). Minor bumps = new clets or significant CLI additions. Patch = bug fixes, including new TG builds. TG version is surfaced in `--version` output (`X.Y.Z (Terminal.Gui A.B.C)`) for diagnostics but does not drive clet's version number. D-020 point 4 ("clet version = TG version verbatim") is superseded by this entry; the rest of D-020 (two dispatch types, channel from suffix, per-channel publishing, MSBuild variable for TG version) remains active.

**Release triggers and auto-increment.** The release workflow (`.github/workflows/release.yml`) fires on three triggers: (1) TG dispatch (`tg-released` / `tg-develop-published`), (2) push to main that changes `src/` or `tests/`, and (3) manual `workflow_dispatch`. On each trigger the workflow reads the base version from `Clet.csproj`, finds the latest `vMAJOR.MINOR.*` tag, and increments patch by one. For develop-channel builds (TG version contains `-`), the prerelease suffix is appended (e.g. `1.0.1-develop.37`). A git tag (`v*`) on HEAD overrides the computed version entirely, allowing maintainers to force a minor/major bump.

**Why:** With `schemaVersion: 1` as the public API surface, clet's major version should reflect schema stability, not TG's release cadence. Starting at `1.0.0` aligns with the JSON contract already locked. The develop-build NuGet pollution made the cost concrete. The pre-release `2.0.0-develop.X` builds will be unlisted on nuget.org.

**Status.** Superseded by D-023. The independent versioning principle remains; the branching model changed.

**Pointers.** Spec §1, §4.7 (`--version` format), §5.6 (versioning), §4.3.1 (schema versioning policy). `src/Clet/Clet.csproj` (`<Version>`). `src/Clet/Hosting/CommandLineRoot.cs` (GetVersion + GetTerminalGuiVersion combined output). `.github/workflows/release.yml` (auto-increment logic).

---

## D-023: Two-branch versioning — main (alpha/stable) + develop (Supersedes D-022)

**Context.** D-022 established independent versioning but treated `main` as the only branch. With the release workflow triggering on every push to `main`, merging any code change became an accidental release. TG itself uses a two-branch model (`main` for releases, `develop` for daily work) and we should mirror it.

**Decision.** Two branches, two version channels:

- **`main`** (release): produces `v{BASE}-{PHASE}.N` during prerelease phases (e.g. `v1.0.0-alpha.1`, `v1.0.0-alpha.2`) and `v{MAJOR}.{MINOR}.{PATCH}` once stable. Build number `N` auto-increments from the latest matching tag.
- **`develop`** (default): produces `v{BASE}-develop.N` (e.g. `v1.0.0-develop.1`, `v1.0.0-develop.2`). Tracks main's base version from csproj `<Version>`.

The csproj `<Version>` (e.g. `1.0.0-alpha`) controls the phase. To exit alpha: change to `1.0.0` and merge to main; the workflow produces `v1.0.0`. `develop` is the default GitHub branch; PRs target `develop`; merging `develop` → `main` is a deliberate release act.

Both channels tag every build (needed for auto-increment). Both publish to NuGet (prerelease suffixes keep develop/alpha off `latest`). Homebrew/WinGet publish only on stable main releases (no `-` in version).

**Why:** Prevents accidental releases from code merges. Mirrors TG's proven model. PRs get CI on `develop` without triggering the release pipeline.

**Status.** Active. Supersedes D-022.

**Pointers.** `.github/workflows/release.yml`, `.github/workflows/ci.yml` (PR targets both branches), `src/Clet/Clet.csproj` (`<Version>1.0.0-alpha</Version>`).

---

## D-024: Package id is bare `clet` (Active)

**Context.** [D-019](#d-019-distribute-clet-as-a-single-project-dotnet-tool-mdv-pattern-active) chose `PackageId=Terminal.Gui.clet` to mirror the [`gui-cs/mdv`](https://github.com/gui-cs/mdv) `Terminal.Gui.mdv` precedent and to keep the gui-cs origin obvious in NuGet search. We've since confirmed the bare `clet` id is unclaimed on nuget.org (verified via the flat-container API and the search index, both empty as of 2026-05-06), and the maintainer has scoped a NuGet API key to a single package id (`clet`) for tighter blast radius. The shorter id matches the tool command name (`dotnet tool install -g clet` is already what users type — and now what they search for too).

**Decision.** Switch `<PackageId>` in `src/Clet/Clet.csproj` from `Terminal.Gui.clet` to `clet`. Install command becomes `dotnet tool install -g clet` (and `--prerelease` for the develop channel). Existing `Terminal.Gui.clet.*` versions on nuget.org are unlisted by the maintainer; the package id is freed but the historical versions remain restorable via explicit `--version` for anyone pinned to them (NuGet immutability).

**Why:** Three reasons.

1. **Discoverability and ergonomics.** `clet` is what the tool *is*; `Terminal.Gui.clet` was a hedge for discoverability when we weren't sure the bare id was available. With the bare id confirmed unclaimed and the tool command already named `clet`, the namespace prefix earned no extra signal.
2. **API key scope.** The maintainer's NuGet API key is now scoped to package id `clet` only — narrower blast radius than a key with broader push rights.
3. **Aligns with how users already think about it.** README, spec §10, and CLAUDE.md all refer to "the `clet` binary" / "the `clet` command." The package id matching that is one less name to keep track of.

**How to apply:** csproj edit. README install snippets, spec §5.4 / §7 v0.5 row / §10 step 10, decisions log D-019/D-020 references, runbook §2.3 NuGet section, and `.github/workflows/README.md` all updated in the same PR. D-019 *Decision* paragraph annotated to point at this entry; the rest of D-019 (single-csproj `PackAsTool`, no separate `Clet.Tool` project, `ToolCommandName=clet`) remains active.

**Status.** Active. Supersedes the package-id choice in D-019; the rest of D-019 stays in force.

**Pointers.** `src/Clet/Clet.csproj` (`<PackageId>clet</PackageId>`). README "Install" / FAQ. Spec §5.4, §7 v0.5 row, §10 step 10. `docs/runbooks/release-rollback.md` §2.3. `.github/workflows/README.md` "NuGet" section.

---

## D-025: Clets declare `AcceptsPositionalArgs`; non-positional clets reject them early (Active)

**Context.** The CLI parser in `CommandLineRoot.DispatchAlias` collected all non-flag tokens into `CletRunOptions.Arguments` and forwarded them unconditionally. Clets that don't consume positional args (everything except `select`, `multi-select`, and `md`) silently ignored them, giving the user no feedback. `clet color - blue` would open the color picker as if the args weren't there. A bare `-` (common stdin convention) also fell through to positional args rather than being flagged.

**Decision.** Add `bool AcceptsPositionalArgs { get; }` to the `IClet` interface with a default implementation of `false`. `SelectClet`, `MultiSelectClet`, and `MarkdownClet` override it to `true`. In `CommandLineRoot.DispatchAlias`, after parsing all args and before dispatching, the clet is resolved from the registry and checked: if `!clet.AcceptsPositionalArgs && positionalArgs.Count > 0`, emit an error to stderr (exit 2) without starting Terminal.Gui. When exactly one positional arg is present, a `hint:` line suggests the likely intended flag (`--initial`).

**Why:** Early rejection in the CLI layer (before TUI init) keeps the feedback fast and the error message useful. The property approach is the simplest declaration that doesn't require clets to report "consumed" state after run. The three positional-consuming clets are known and stable; the default of `false` means new clets are safe by default.

**How to apply.** `src/Clet/Abstractions/IClet.cs` — new default property. `src/Clet/Hosting/CommandLineRoot.cs` — early validation block. `SelectClet`, `MultiSelectClet`, `MarkdownClet` — explicit override. Spec §4.7 updated to describe the rejection. New unit tests in `CommandLineRootTests`, `SelectCletTests`, `MultiSelectCletTests`, `MarkdownCletTests`, `IntCletTests`.

**Status.** Active.

**Pointers.** `src/Clet/Abstractions/IClet.cs`, `src/Clet/Hosting/CommandLineRoot.cs`, spec §4.7.

---

## D-026: `--rows` / `-r` is a built-in CLI flag, not a per-clet option (Active)

**Context.** The `multiline-text` clet issue (#48) proposed `--rows` as a clet-specific option (in the `Options` list) to control the visible row height of the `TextView`. During implementation review, the requirement was broadened: `--rows` should work for any clet, not just `multiline-text`. The same "number of visible rows" concept is meaningful for other clets (e.g., `select`, `multi-select`).

**Decision.** Add `--rows` / `-r` as a built-in global flag parsed by `CommandLineRoot.DispatchAlias`, alongside `--title`, `--initial`, `--json`, `--fullscreen`, and `--timeout`. Store the value in `CletRunOptions.Rows` (type `int?`). Each clet reads `options.Rows` and falls back to its own default. `MultilineTextClet` defaults to 5 rows. Other clets may adopt `options.Rows` in future PRs as needed.

**Why:** Keeping `--rows` global is consistent with how `--title` was handled (D-014 — title is also conceptually per-clet but elevated to a universal flag because it applies uniformly). It avoids each clet that wants row control having to redeclare a `rows` option descriptor. It keeps the CLI surface predictable: any clet that renders a scrollable or variable-height view honours the same flag.

**How to apply:** `CletRunOptions.Rows` added. `CommandLineRoot` now parses `--rows <n>` (n must be a positive integer) and `-r <n>`. Error message on missing or non-positive value. `MultilineTextClet` uses `options.Rows ?? 5`. Spec §4.7 updated to include `--rows <n>` in the CLI surface line.

**Status.** Active.

**Pointers.** `src/Clet/Abstractions/CletRunOptions.cs`, `src/Clet/Hosting/CommandLineRoot.cs`, `src/Clet/Clets/Input/MultilineTextClet.cs`. Compare D-014 (`--title` as built-in flag).

## D-027: `--cat` non-interactive stdout rendering for viewer clets (Active)

**Context.** The `md` viewer clet renders in a fullscreen interactive TUI, but many workflows need formatted markdown dumped to a pipe — quick previews in CI logs, piping to a pager (`clet md --cat README.md | less -R`), or AI agents consuming human-readable output without driving a TUI (issue #29).

**Decision.** Add `--cat` as a built-in CLI flag (like `--fullscreen` or `--json`). When passed to a viewer clet, it bypasses TUI initialization and renders content directly to stdout using `MarkdownHelpRenderer.RenderToAnsi` — the same ANSI rendering pipeline used by `clet --help`. Content is resolved from file arguments, `--initial`, or stdin (same sources as the normal viewer path). If no content is available, exits with usage error (exit 2). The flag is a no-op (ignored) for input clets.

**Rationale.** Reuses the existing `MarkdownHelpRenderer.RenderToAnsi` infrastructure already proven for help rendering. The flag is host-level (not clet-specific) because the rendering bypass happens before the clet's `RunAsync` is called — the dispatcher short-circuits. Name `--cat` chosen for familiarity with the unix `cat` command.

**Status.** Active.

**Pointers.** `src/Clet/Abstractions/CletRunOptions.cs` (`Cat` property), `src/Clet/Hosting/CommandLineRoot.cs` (parsing), `src/Clet/Hosting/AliasDispatcher.cs` (`ResolveViewerContent` + render bypass), spec §4.7.

---

## D-028: `--output <path>` writes result to a file instead of stdout (Active)

**Context.** Terminal.Gui renders to stdout. Any form of stdout redirection (`$()`, `|`, `>`) hides the TUI — the user sees nothing (gui-cs/Terminal.Gui#5207). Until TG supports rendering to `/dev/tty` when stdout is redirected, clet needs a workaround to let scripts capture the result while keeping the TUI visible.

**Decision.** Add `--output <path>` / `-o <path>` as a built-in CLI flag. When set, `OutputFormatter` writes successful results (plain text or JSON) to the specified file path instead of stdout. The file is created with non-overwriting semantics; existing paths are refused rather than truncated. Non-success results are not written to the output file and are emitted through the normal stdout/stderr paths. stdout remains fully available for TUI rendering. If the file cannot be written, an error is emitted to stderr and the process exits with code 2 (usage error).

**Rationale.** This is a minimal, composable workaround: the user picks the file path, the result goes there, and stdout stays for TUI. It works equally well in bash (`clet select --json --output /tmp/r.json`) and PowerShell. The flag is host-level (not clet-specific) because it controls where the *result* goes, not how the clet behaves.

**Status.** Active.

**Pointers.** `src/Clet/Abstractions/CletRunOptions.cs` (`OutputPath` property), `src/Clet/Hosting/CommandLineRoot.cs` (parsing), `src/Clet/Hosting/OutputFormatter.cs` (file write logic), `src/Clet/Hosting/AliasDispatcher.cs` (error handling), spec §4.7.

---

## D-029: Release workflow uses `env:`-bound variables + allowlist validation for user-supplied inputs (Active)

**Context.** `.github/workflows/release.yml` originally interpolated `${{ github.event.client_payload.tg_version || github.event.inputs.tg_version }}` and `${{ github.event.inputs.version_override }}` directly into a `run: bash` script. GitHub Actions evaluates `${{ }}` expressions before the shell parses the script, so shell quoting in YAML does not help — this is the canonical `actions/script-injection` CodeQL pattern. The job runs with `contents: write` + `issues: write` and the downstream `publish-nuget` job consumes `secrets.NUGET_API_KEY`.

**Decision.** All user-controlled expressions in the `Compute version` step are bound to step-level `env:` variables (`TG_VERSION_INPUT`, `VERSION_OVERRIDE`, `EVENT_NAME`) and only referenced as `$VAR` in the `run:` script. Additionally, `TG_VERSION_INPUT` and `VERSION_OVERRIDE` are validated against an allowlist regex (`^[0-9A-Za-z._+*-]+$`) before use; the step exits 2 immediately if validation fails. `github.event_name` (runner-set, not attacker-controllable) is also moved to `env:` for defense-in-depth.

**Why not a third-party action?** The logic is simple bash; adding a new action dependency for this pattern would be a larger attack surface than the single-step env binding. The regex covers every version string shape in use: SemVer (`1.2.3`), prerelease (`1.2.3-alpha.1`, `2.0.2-develop.37`), floating wildcard (`2.0.2-develop.*`), and the `+` build-metadata suffix.

**Status.** Active.

**Pointers.** `.github/workflows/release.yml` (`Compute version` step), `docs/threat-model.md` ("Release pipeline" section), issue #37.

---

## D-030: Terminal escape sanitization at the clet layer, not relying on TG (Active)

**Context.** `docs/threat-model.md` "Terminal escape sanitization" originally claimed that TG's cell model prevented escape injection. While TG's `TextField` and `OptionSelector` do render cell-by-cell, the `Markdown` view (used by `clet md`) passes content through a richer pipeline where raw escape bytes in fenced code blocks or inline content could survive to the terminal. The `MarkdownHelpRenderer.RenderToAnsi` `--cat` path writes rendered ANSI directly to stdout, giving smuggled escapes a direct path to the user's terminal. Relying on a `2.0.2-develop.*` preview library for security filtering is unacceptable.

**Decision.** Implement `TerminalEscapeSanitizer` (single internal helper) that strips ESC (`\x1b`), BEL (`\x07`), 8-bit CSI (`\x9b`), 8-bit OSC (`\x9d`), and all C1 7-bit pairs (`\x1b@` through `\x1b_`) from user content. Apply at three call sites:
1. `MarkdownClet` — inline content before `markdownView.Text = content`.
2. `MarkdownClet.LoadFile` — file content before `markdownView.Text = fileContent`.
3. `MarkdownHelpRenderer.RenderToAnsi` — input sanitization before rendering, plus a final output pass (`SanitizeRenderedOutput`) that strips dangerous sequences while preserving the renderer's own CSI/SGR sequences.

This is clet's own defense. TG's cell model is treated as defense-in-depth, not relied upon. Future TG version bumps cannot silently regress this protection.

**Status.** Active.

**Pointers.** `src/Clet/Hosting/TerminalEscapeSanitizer.cs`, `src/Clet/Clets/Viewer/MarkdownClet.cs`, `src/Clet/Hosting/MarkdownHelpRenderer.cs`, `docs/threat-model.md` ("Terminal escape sanitization" section), `specs/clet-spec.md` Appendix A, issue #92.

---

## D-031: SurfaceOnly link policy locked at v0.5; `--allow-link-open` deferred (Active)

**Context.** D-017 established the SurfaceOnly default for `clet md` links. At v0.5 we lock this behavior: the `LinkClicked` handler sets `e.Handled = true` and shows the URL in the status bar; no `Process.Start` or `ShellExecute` exists in `src/`. Spec Appendix A previously mentioned `--allow-link-open` as if it were available — it is not wired up.

**Decision.** The closed-by-default SurfaceOnly policy is locked for v1.0. No `--allow-link-open` flag ships. The flag name is reserved for a future v1.x release that will require explicit user acceptance of documented risks (arbitrary URL scheme handling, potential data exfiltration). Spec Appendix A updated to state "deferred — not wired in v1.0." Integration tests verify the invariant.

**Status.** Active.

**Pointers.** D-017, `src/Clet/Clets/Viewer/MarkdownClet.cs` (LinkClicked handler), `docs/threat-model.md` (Markdown link policy section), `specs/clet-spec.md` (Appendix A), `tests/Clet.IntegrationTests/MarkdownCletIntegrationTests.cs` (link safety tests).

---

## D-032: Replace `range` clet with `linear-range` backed by Terminal.Gui's `LinearRange<T>` (Active, supersedes D-011)

**Context.** The original `range` clet wrapped a hand-rolled `RangeView` (`NumericUpDown<int>` × 2 + a `..` label) and emitted `{"low": <int>, "high": <int>}` per spec §4.3.2. It worked, but the UX was poor — two independent spinners with no visual sense of the range relationship — and it duplicated functionality TG shipped via [Terminal.Gui PR #5204](https://github.com/gui-cs/Terminal.Gui/pull/5204) (the `LinearRange<T>` `IValue` refactor).

**Decision.** Delete `RangeClet`, `RangeView`, and their tests. Add `LinearRangeClet` (alias `linear-range`) backed by the `LinearRange<T>` view family, with three modes controlled by `--mode single|multi|range` (default `single`). Wire format changes from `{low, high}` to a mode-dependent JSON object:

- `--mode single` → `{"mode":"single", "value":"<label>", "index":<N>}`
- `--mode multi` → `{"mode":"multi", "values":[...], "indices":[...]}`
- `--mode range` → `{"mode":"range", "kind":"closed|left|right|none", "start":"<label>", "end":"<label>", "startIndex":<N>, "endIndex":<M>}` (fields conditional on kind)

Additional options: `--orientation horizontal|vertical`, `--range-kind closed|left|right` (default `closed`, only for `--mode range`), `--allow-empty`, `--hide-legends`.

**Status.** Active. Supersedes [D-011](#d-011-range-is-integer-only-at-v03-active).

**Pointers.** `src/Clet/Clets/Input/LinearRangeClet.cs`, spec §4.3.2, README, [Terminal.Gui PR #5204](https://github.com/gui-cs/Terminal.Gui/pull/5204).

---

## D-033: `clet md` file-access confinement policy (Active)

**Context.** `clet md FILE` is an arbitrary file-read primitive. In agent contexts, positional arguments may be attacker-influenced via indirect prompt injection. An attacker could instruct an agent to run `clet md /home/$USER/.aws/credentials`, exfiltrating file content through the rendered ANSI output or `--cat` stdout. Glob patterns (`clet md '/etc/*.conf'`) amplify this to directory enumeration. Issue #93 documents the full threat surface.

**Decision.** Default-deny file access policy with explicit opt-in escape hatches:
1. **Extension allowlist:** `.md`, `.markdown`, `.txt` only.
2. **Working directory confinement:** Files must be under the process cwd.
3. **Per-file size cap:** 16 MiB.
4. **Aggregate size cap:** 32 MiB across all glob matches.
5. **Glob count cap:** 128 files maximum.
6. **Binary rejection:** NUL byte in first 8 KiB refuses the file.
7. **`--allow-file <path>`** (repeatable): Bypasses extension + cwd checks for the named path or directory. Size and binary checks still apply.
8. **`--allow-binary`:** Disables NUL-byte detection.

For `pick-file`/`pick-directory`, `--root` remains a starting directory, not a sandbox. The interactive file picker is user-driven and OS-permission-gated; confinement is the OS sandbox's responsibility, not clet's. Help text updated to make this explicit.

**Why not confine `pick-*`?** The user is interactively choosing files. Constraining navigation would break legitimate workflows and provide false security (the user can always run `ls` separately). The agent-context risk is in *non-interactive* file reads (`clet md`), not interactive dialogs.

**Status.** Active.

**Pointers.** `src/Clet/Hosting/FileAccessPolicy.cs`, `src/Clet/Hosting/AliasDispatcher.cs` (`ResolveViewerContent`), `src/Clet/Clets/Viewer/MarkdownClet.cs` (`ExpandFiles`), `src/Clet/Hosting/CommandLineRoot.cs` (`--allow-file`, `--allow-binary` parsing), `docs/threat-model.md` ("File access scope" section), issue #93.

## D-034: Explicit `TextMateSharp` PackageReference to repair publish output (Active)

**Context.** `dotnet publish` of a clet binary (AOT or non-AOT, self-contained) was dropping `TextMateSharp.dll` from the output directory while keeping `TextMateSharp.Grammars.dll`. At runtime, the markdown viewer's syntax highlighter throws `FileNotFoundException: TextMateSharp` the first time anything tries to render highlighted code (e.g. `clet help select --cat`). This blocked PR #110's bar of "non-AOT and AOT binaries actually run on this machine," which is the only way to verify the release pipeline produces something users can run.

The asset-flow break is upstream: Terminal.Gui transitively pulls in `TextMateSharp` via `TextMateSharp.Grammars`, but something about how the package is composed prevents the runtime DLL from appearing in publish output even though it is present in `bin/` after `dotnet build`. Reproducible on `2.0.2-develop.57` and `2.0.2-develop.24`.

**Decision.** Add a direct `<PackageReference Include="TextMateSharp" Version="2.0.3" />` to `src/Clet/Clet.csproj` so publish picks it up. Pin matches what `TextMateSharp.Grammars` 2.0.3 already brings in transitively; we are not introducing a new version surface.

**Why not push entirely upstream.** PR #110's release pipeline is the user-visible deliverable; we cannot ship until the published binary works. A clet-side workaround is one line and fully reversible. We will remove the explicit reference once the upstream packaging is fixed.

**Status.** Active. Remove once Terminal.Gui's package correctly flows the `TextMateSharp` runtime asset into downstream publish output. Re-test on every TG bump until then.

**Pointers.** `src/Clet/Clet.csproj` (`<PackageReference Include="TextMateSharp" />`), PR #110.
