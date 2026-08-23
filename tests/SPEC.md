# clet Testing Spec

Companion to [`specs/clet-spec.md`](../specs/clet-spec.md). This doc lives next to the test projects so it stays in sync when test layout or harness shape changes. The main spec defers to this one for everything in `tests/`.

Nine test layers, each with a clear "what does this catch" purpose. All test code lives in `tests/<project>/`. `Markdown` View rendering quality is tested in TG core, not here ([tui-cs/Terminal.Gui#5156](https://github.com/tui-cs/Terminal.Gui/issues/5156)).

## 1. When each layer runs (tier matrix)

Three harness families, three questions, three cost profiles. Keep them separate; don't merge.

- **In-process logic (xUnit, no `Application.Init`)** answers *"is the data plumbing right?"* — milliseconds per case. §2.1, §2.5, §2.8.
- **In-process UI (xUnit + `IApplication` + `InputInjection` + `Driver.Contents` snapshots)** answers *"does the right thing get rendered when the user types this?"* — milliseconds per case, frame-stepped. §2.2, §2.3.
- **Process-level (TUIcast over PTY)** answers *"does the deployed binary behave right end-to-end?"* — covers argument parsing, stdout JSON, exit codes, signal handling, AOT trim divergence. ~1–2s per case. §2.4, §2.7.

Tier matrix:

| Trigger                       | Layers run                                                         |
|-------------------------------|--------------------------------------------------------------------|
| Inner loop (laptop)           | §2.1, §2.2, §2.3                                                   |
| PR CI                         | §2.1, §2.2, §2.3, §2.4 (happy path per clet), §2.5, §2.8           |
| Nightly                       | All PR CI, plus full §2.4, §2.7, §2.9 dry-run                      |
| Pre-release / release gate    | Full §2.4, §2.7, release-pipeline §5.3 smoke gate, §2.6 manual run |

The legitimate worry that in-process injection drifts from AOT behavior is addressed by §2.7 (AOT publish tests run the full §2.4 smoke matrix against the AOT binary). Don't double-pay it by routing §2.2/§2.3 through TUIcast — the wall-clock cost is real and the marginal coverage is near zero.

## 2. Test layers

### 2.1 Unit tests (`Clet.UnitTests`)

**What this catches:** Logic bugs in the registry, options, parsing, JSON serialization, exit code mapping.

**Coverage target:** 90%+ for `src/Clet/Abstractions`, `src/Clet/Registry`, `src/Clet/Json`.

**Cases:**
- `CletRegistry`:
  - Register/resolve by primary alias, by secondary alias.
  - Conflict on duplicate alias raises `InvalidOperationException`.
  - `All` is stable in iteration order.
- `CletRunOptions`:
  - Default values.
  - `Fullscreen` flag round-trips correctly.
- `IParsable<T>` integration:
  - String → int, decimal, DateTime, TimeSpan via reflection-free hooks.
  - Bad input → `CletRunResult { Status = Error, ErrorCode = "validation" }`.
- `CletJsonOutput`:
  - Round-trip every result variant.
  - Output matches `SchemaV1` byte-for-byte for canonical inputs (golden files).
  - No properties leak: cancelled envelopes contain only `schemaVersion` and `status`; viewer success envelopes contain only `schemaVersion` and `status`; no envelope ever emits a wire-format `type` field (the field was dropped at v0.5; result types are advertised once via `clet list --json`, not on every result).
- Exit code mapping: each `CletRunStatus` and error code maps to the documented exit.
- Cancellation: `CletRunResult.Cancelled` propagates through every layer.

**Per-clet behavior tests** (one fixture per clet; 15 total):
- Register, resolve, advertise correct `Kind` and `ResultType`.
- Default options round-trip.
- Initial-value parsing with valid input.
- Initial-value rejection with invalid input.
- (Where applicable) options: `--root`, `--filter`, `--multi`, etc., each tested in isolation.

**Patterns:** xUnit v3, `[Fact]` and `[Theory]`. No `Application.Init`. No threading. A module initializer sets `DisableRealDriverIO=1` and replaces `Console.In` with `TextReader.Null` so runners that keep stdin redirected but open (for example ReSharper test sessions) cannot wedge stdin-reading code paths.

### 2.1b Configuration tests (`Clet.ConfigTests`)

**What this catches:** Races and ordering bugs in the process-global static settings state (`EditorSettings`, `FileAccessSettings`, and Terminal.Gui's static settings facades loaded via `CletConfiguration`/`TuiConfigurationBuilder`). Configuration tests that run in a parallel assembly can observe half-applied state depending on which collection reloads configuration first.

**Why a separate project:** `DisableParallelization = true` on a collection only stops intra-/cross-collection concurrency *within* one assembly — it doesn't prevent a *different* parallel collection in the same assembly from mutating the settings before the configuration tests run. The only robust isolation (used by Terminal.Gui itself and the sibling `tui-cs/Editor` repo) is a separate assembly with `parallelizeAssembly: false` and `parallelizeTestCollections: false` in `xunit.runner.json`.

**Cases:**
- `EditorSettings`: ManagedKeys completeness, Save round-trips (JSONC comments, existing keys, default template, key updates), Load from the nested `"EditorSettings"` section restores values.
- `FileAccessSettings`: AllowedPaths Load from the nested section, MergeWithConfigPaths logic, AddToConfig persistence, FileAccessPolicy integration.

**Canonical CM test pattern:** Each test defensively resets CM (`if (IsEnabled) Disable(true)`), sets `ThrowOnJsonErrors = true`, uses `RuntimeConfig` for in-memory config injection (never file-based `AppHome`), and resets via `Disable(resetToHardCodedDefaults: true)` in `finally`/`Dispose`.

**Patterns:** xUnit v3, `[Fact]` and `[Theory]`. No `Application.Init`. No threading. Full assembly serialization via `xunit.runner.json`. A module initializer sets `DisableRealDriverIO=1` before any test code can touch Terminal.Gui and replaces `Console.In` with `TextReader.Null` so redirected-but-open runner stdin cannot block tests.

### 2.2 Integration tests (`Clet.IntegrationTests`)

**What this catches:** TG hosting bugs (init/teardown, cancellation propagation, lifecycle) that unit tests can't see because they don't run an `IApplication`. **Pure-state assertions** — these tests assert on `CletRunResult`, exit codes, and lifecycle events, not on rendered output. Rendered-output assertions live in §2.3.

**Cases:**
- Each clet runs end-to-end with `StopAfterFirstIteration = true`; verify final `CletRunResult`.
- Pre-cancelled `CancellationToken` short-circuits with `Status = Cancelled`.
- `CletRunOptions.Title` flows through to the wrapper.
- Theme override per invocation; verify the View's effective scheme name.
- Inline vs alt-screen mode; verify driver state transitions.

**Patterns:** `Application.Create()` per test (isolation), `app.Init("ansi")`. Tests are synchronous after `await clet.RunAsync(...)` returns. No `InputInjection` here — keystrokes are §2.3's territory. A module initializer sets `DisableRealDriverIO=1` before any test code can touch Terminal.Gui and replaces `Console.In` with `TextReader.Null` so redirected-but-open runner stdin cannot block tests.

### 2.3 UI snapshot tests (`Clet.UITests`)

**What this catches:** UI bugs that pure-state tests miss — wrong title rendered, focus on the wrong field, expected text clipped off-screen, key-binding regressions, the kinds of visual regressions that today only surface on the §2.6 manual matrix or after a tester files an alpha-feedback Issue.

**Why this layer exists:** The original §2.2 wording promised "rendered output captured for assertions" but the existing tests never delivered it — they only assert on `CletRunResult`. TG's `AppTestHelper` does capture rendered output (`Driver.ToString()` / `ToAnsi()` / `Contents`) but writes it to a `TextWriter` for human inspection, not to an assertion. §2.3 closes that gap with a clet-side harness whose snapshots flow into `Assert.*` directly.

**Source of truth.** Snapshots come from `app.Driver.Contents` (the `Cell[,]` grid). Note: `Contents` is pre-clipping — a View that draws outside its bounds shows up here even though it would be clipped before reaching a real terminal. For clet's clets this is acceptable; clipping rarely matters and tests that specifically need post-clipping behavior can fall back to `Driver.GetOutput().GetLastOutput()` for the rare cases. Default to `Contents`.

**Test harness:** `tests/Clet.UITests/CletUiHarness.cs` (sketched in §3.2 of this doc). Builds an `IApplication` with the `ansi` driver, locks screen size, runs the clet on a `Task` that the test thread cooperates with via the `Iteration` event — no background loop, no semaphores, no wall-clock waits. The test thread owns the clock; every `harness.Press(Key.X)` advances the loop one iteration deterministically.

**Three assertion styles**, picked by what the test cares about:
- **Substring:** `Assert.Contains("Pick one", harness.SnapshotText())`. Most resilient to layout tweaks. Use when "this text is on screen" is the contract.
- **Cell region:** `harness.AssertCellsAt(row: 1, col: 2, len: 7, expected: "staging")`. Use when position matters (focused-row indicator, fixed columns).
- **Golden snapshot:** `harness.AssertMatchesGolden("select_initial.txt")`. Stored under `tests/Clet.UITests/Goldens/<test-name>.txt`. Plain text by default; opt-in `.ansi` files for tests that care about color/scheme. Regenerable via `CLET_REGEN_GOLDENS=1 dotnet test` — fails the run when the env var is set, after rewriting the golden file in place. Use sparingly: goldens are noisy on intentional layout changes, valuable when the §2.6 manual matrix can't reach (e.g., D-013 base-scheme on `pick-file`).

**Cases:**
- Initial render per clet: title, prompt, initial value visible. (15 cases)
- Keystroke-driven flows: type text → render → backspace → render → Enter → result. (per-clet cases)
- Mouse interaction where applicable: `pick-file` row selection, `md` link surfacing.
- Theme regressions: D-013 `Schemes.Base` applied across all wrappers. Captured as ANSI golden so a scheme-rewrite regression is visible in PR diff.
- `clet --help` and `clet help <alias>` rendering: a regression in the Markdown pipeline (e.g., the Windows OEM-encoding bug we hit) lights up here, not just on a tester's machine.

**Boundary against §2.6 four-terminal manual matrix:** §2.3 exercises **only** the `ansi` driver. Cursor save/restore, alt-screen toggles, real-terminal glyph mapping (CP437 vs Windows Terminal) are §2.6's territory. §2.3 is the home for "the right *content* renders at the right *cell positions* given the right *input sequence*."

**Boundary against §2.2 integration tests:** §2.2 stays pure-state. §2.3 is rendered-output. A test that asserts on both `harness.SnapshotText()` and `result.Value` belongs in §2.3, not §2.2 — the snapshot capture is what defines the layer.

**Patterns:** xUnit v3. `using var harness = await CletUiHarness.StartAsync(...)` per test. No `Application.Init` outside the harness. No threading outside the cooperative `Iteration` step. Frame-stepping discipline: every input event is followed by a render tick before the next assertion. A module initializer sets `DisableRealDriverIO=1` before any test code can touch Terminal.Gui and replaces `Console.In` with `TextReader.Null` so redirected-but-open runner stdin cannot block tests.

### 2.4 Process / smoke tests (`Clet.SmokeTests`)

**What this catches:** Bugs that only appear when `clet` runs as a real process — argument parsing, stdout/stderr wiring, exit codes, signal handling, AOT-vs-JIT divergence.

**Parallelization:** Disabled at the assembly level. Process-level cases share the test-copied `clet` output and must not race multiple child processes over the same output artifacts. A module initializer sets `DisableRealDriverIO=1` and `Console.In = TextReader.Null` in the smoke-test host, and `CletProcess` passes `DisableRealDriverIO` to spawned `clet` children. Child stdin is still explicitly redirected only for smoke cases that pass `stdin`.

**Cases:** Identical to the release-pipeline smoke matrix in [`specs/clet-spec.md` §5.3](../specs/clet-spec.md#53-smoke-test-gate-p0-release-fails-closed) (every clet boots, returns valid JSON, exits with the correct code). Run on every PR to `tui-cs/clet`, every TG-triggered release build, and nightly against the latest TG develop branch.

**Tooling:** TUIcast in deterministic-script mode (same driver as §5.3 in the main spec). The xUnit fixture shells out to TUIcast with a per-clet keystroke script, captures the resulting JSON from the spawned `clet` process's stdout, and asserts on exit code + envelope shape. Using the same driver as the release gate means a green CI run is byte-equivalent evidence that the release gate will be green; we do not maintain two parallel smoke harnesses.

**Scope guardrail.** §2.4 covers exactly one happy path per clet plus the cancellation case. Bug repros, option-matrix coverage, and behavior variants go in §2.3 (in-process, fast). Without this rule, every regression PR adds a TUIcast case, the release gate hits 30 minutes by v0.7, and the smoke layer becomes the integration layer at process-level cost.

**Scripts as data, not code.** Per-clet keystroke scripts live as text files under `tests/Clet.SmokeTests/scripts/<alias>.txt` (one line per script, TUIcast comma-separated keystroke syntax). The xUnit fixture loads the file by alias; it does not embed scripts in C# string literals. A contributor can add a smoke case by editing data, and any script can be re-run locally with `npx tuicast --binary ./clet --script-file scripts/<alias>.txt` without rebuilding the test project.

**Asciinema artifacts.** TUIcast captures every smoke run as a `.cast`. Successful runs discard the cast (artifact-store noise); failed runs upload it as a workflow artifact for forensic replay. Retention follows GitHub Actions' default (90 days). If we ever need longer for a specific incident, copy the artifact to the issue manually.

### 2.5 JSON contract tests (`Clet.ContractTests`)

**What this catches:** Schema drift; promises to AI agent consumers being broken silently.

**Cases:**
- Every line emitted by every clet across the full input matrix validates against `SchemaV1`.
- Schema additions in v1.x are confirmed additive only (a v1.0 consumer can still parse v1.x output).
- `clet list --json` validates against its own list schema.

**Tooling:** `JsonSchema.Net` for validation. The schema file is the source of truth; tests read it, not a copy.

### 2.6 Cross-terminal manual matrix

**What this catches:** Driver-specific rendering bugs that automated tests can't reproduce reliably (cursor save/restore, alt-screen toggles, real-terminal glyph mapping, mouse).

**Matrix:**

|                  | macOS Terminal | iTerm2 | Windows Terminal | GNOME Terminal |
|------------------|:--------------:|:------:|:----------------:|:--------------:|
| `clet text`      |       ☐        |   ☐    |        ☐         |       ☐        |
| `clet pick-file` |       ☐        |   ☐    |        ☐         |       ☐        |
| `clet md`        |       ☐        |   ☐    |        ☐         |       ☐        |
| Theme switch     |       ☐        |   ☐    |        ☐         |       ☐        |
| Mouse click      |       ☐        |   ☐    |        ☐         |       ☐        |
| Inline restore   |       ☐        |   ☐    |        ☐         |       ☐        |

Run before every minor release (v1.0, v1.1, ...). Captured in a release checklist issue ([#23](https://github.com/tui-cs/clet/issues/23) tracks the first pass for v0.5). v0.5 milestone gate.

### 2.7 AOT publish tests

**What this catches:** Trim warnings, runtime AOT failures, regressions in AOT-compatibility of TG core. With no separate AOT audit (the original §3 entry was dropped because TG core already tracks AOT work), these tests are the primary discovery mechanism for AOT issues; failures here are filed as issues against `tui-cs/Terminal.Gui` with a minimal repro.

**Cases:**
- CI publishes the AOT binary on every PR to `tui-cs/clet` and on the nightly TG-develop run.
- Zero trim warnings tolerated; warnings fail the build.
- Smoke tests (§2.4) run against the AOT binary, not just the JIT'd debug build.
- AOT failures discovered during `tui-cs/clet` builds are filed against `tui-cs/Terminal.Gui` with a minimal repro.

### 2.8 Performance tests (`Clet.PerfTests`)

**What this catches:** Cold-start regressions that erode the "feels instant" property AI agents need.

**Cases:**
- `clet --version` cold start: <100ms macOS arm64, <100ms linux-x64, <150ms Windows x64.
- `clet list --json` cold start: same budgets.
- Tracked over time; regression alerts at +25% on a 7-day rolling baseline.

### 2.9 Release pipeline dry-run tests

**What this catches:** Workflow regressions that would otherwise only surface during a real TG release (when the cost is high).

**Cases:**
- Weekly cron: simulate a `repository_dispatch` with a fake version. Build, smoke-test, generate manifests, but stop short of publish.
- Verify all template files render correctly, all artifact uploads succeed, all checksums match.

## 3. Patterns and conventions

### 3.1 General

- xUnit v3 across all projects (`Microsoft.NET.Test.Sdk` not used; xUnit's own host).
- One `[Fact]` per concrete behavior; `[Theory]` for parameterized cases.
- No `Application.Init` outside `Clet.IntegrationTests` and `Clet.UITests`.
- `using var` for any disposable — never leak app or driver state across tests.

### 3.2 §2.3 UI harness shape

The `CletUiHarness` lives in `tests/Clet.UITests/CletUiHarness.cs`. Sketched API (the actual implementation lands in a follow-up PR):

```csharp
public sealed class CletUiHarness : IAsyncDisposable
{
    public static Task<CletUiHarness> StartAsync<T>(
        IClet<T> clet, string? initial, CletRunOptions options,
        int width = 60, int height = 10);

    // Snapshots — captured from the initial render
    public string SnapshotText();          // glyphs only
    public string SnapshotAnsi();          // glyphs + ANSI styling via IDriver.ToAnsi()
    public Cell[,] SnapshotCells();        // full structured grid

    // Assertions
    public void AssertCellsAt(int row, int col, string expected);
    public void AssertMatchesGolden(string fileName); // CLET_REGEN_GOLDENS=1 rewrites
    public void AssertMatchesAnsiGolden(string fileName); // CLET_REGEN_GOLDENS=1 rewrites .ans goldens

    // Lifecycle — completes when the clet's RunAsync returns
    public Task<CletRunResult<T>> GetResultAsync();
}
```

**Initial-render discipline.** The harness captures snapshots from `Iteration` while the clet is
running, then requests stop after the initial layout/draw sequence settles.

No background task. No semaphores. No `Task.Delay`. The test thread owns the clock. Initial rendering tests capture a few `Iteration` frames and then request stop, so ANSI goldens are deterministic and can be inspected with `cat tests/Clet.UITests/Goldens/<name>.ans`.

**FileDialog notes.** `pick-file` and `pick-directory` are the trickiest cases — they enumerate mount points, do async file-system work, and post layout changes that span multiple iterations. Their render tests use temporary roots with stable timestamps so the ANSI goldens do not churn.

### 3.3 Goldens

- Plain text under `tests/Clet.UITests/Goldens/<test-name>.txt`. ANSI variants under `<test-name>.ans` for color/scheme tests.
- Regenerated by `CLET_REGEN_GOLDENS=1 dotnet run --project tests/Clet.UITests`. With the env var set, mismatches rewrite the file *and* fail the run — so a regen requires a deliberate second run to confirm the new golden is what the author intended.
- Reviewed in PR diffs. If a golden churns on every layout PR, it's the wrong assertion shape — switch to substring or cell-region.

### 3.4 Source of `Driver.Contents` vs `IDriver.ToAnsi()`

`Contents` is useful for glyph-only assertions. `IDriver.ToAnsi()` is the preferred source for rendering goldens because it preserves layout plus colors/styles in a `cat`-able stream.

### 3.5 Cross-references

The main spec (`specs/clet-spec.md`) refers to test layers by name (e.g., "smoke tests", "AOT publish tests") rather than by section number, so renumbering this doc doesn't ripple through the main spec. New cross-refs go via [`tests/SPEC.md`](.) markdown links.
