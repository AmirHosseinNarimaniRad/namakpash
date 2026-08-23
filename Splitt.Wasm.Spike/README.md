# Splitt.Core on WebAssembly — feasibility spike

Answers one question before any PWA work starts: **can `Splitt.Core` run in a browser unchanged?**

It compiles the real test sources from `Splitt.Tests` into a Blazor WebAssembly app and executes
them in the browser. Nothing is reimplemented — the same `.cs` files run. The desktop test host
(`Microsoft.NET.Test.Sdk` + vstest) is a process runner that does not exist under WASM, so
`Spike/TestRunner.cs` discovers `[Fact]`/`[Theory]` by reflection and invokes them; only the
plain-managed parts of xUnit (attributes and `Assert`) are referenced.

```bash
dotnet run --project Splitt.Wasm.Spike        # then open http://localhost:5199
```

## Result (2026-08-23, .NET 10.0.10, browser-wasm)

**63 of 64 test cases pass in the browser. `Splitt.Core` needed no changes at all.**

| Probe | Result |
|---|---|
| Runtime | .NET 10.0.10 · `browser-wasm` |
| Globalization / ICU | invariant mode **off**; `fa-IR` resolves to Persian (Iran) |
| `PersianCalendar` | `2026-08-23` → `1405/06/01` → back — Jalali works, Western digits intact |
| `decimal` arithmetic | `0.1 + 0.2 == 0.3` — real decimals, not doubles |
| Amount TEXT round-trip | `1234567.89` → `"1234567.89"` → `1234567.89` under InvariantCulture |
| Equal split remainder | `33334 + 33333 + 33333 = 100000` (invariant #4 holds) |
| SQLite (sqlite-net) | **fails** — `DllNotFoundException: e_sqlite3` |

The single failing test is `ExportImportTests.Database_ImportRoundTrip_PreservesBalances`, the one
test that opens a `SplittDatabase`. Every other test — balances, settlement planning, the report
builder, the PDF page-break arithmetic, Bidi, Persian dates — passes untouched.

## What this settles

- **The Blazor-over-TypeScript argument holds.** `decimal` survives, and the money logic, its
  invariants and its tests carry over with zero edits.
- **Globalization is not a problem.** The plan flagged a risk that Blazor might run
  globalization-invariant and break `PersianCalendar`. It does not; ICU data is present by default.
- **The sqlite-net coupling is wider than the plan assumed but does not block the build.** The
  attributes sit on the models (`Trip`, `Expense`, `ExpenseShare`, `Participant`), not only in
  `Data/`, so the package comes along for the ride — and that is fine. It only fails when a
  statement actually runs.

## What is still open

`sqlite-net` ships a `browser-wasm` build of `e_sqlite3`, and the build says so:

```
warning: @(NativeFileReference) is not empty, but the native references won't be linked in,
because neither $(WasmBuildNative), nor $(RunAOTCompilation) are 'true'.
NativeFileReference=…/runtimes/browser-wasm/nativeassets/net9.0/e_sqlite3.a
```

Native relinking needs the `wasm-tools` workload, which is not installed on this machine:

```bash
sudo dotnet workload install wasm-tools     # dotnet lives in /usr/local/share/dotnet (root-owned)
```

With it, `<WasmBuildNative>true</WasmBuildNative>` should link SQLite in and the last test may
pass too. **That would not by itself solve persistence**: Emscripten's filesystem is in memory, so
the database file still has to be synced to OPFS or IndexedDB by hand. Reusing `Data/` verbatim is
therefore a real option to evaluate, not a foregone conclusion — the alternative remains rewriting
`Splitt.Core/Data/` (171 lines) against browser storage directly.

This project is a spike. It is not the PWA and is not meant to grow into it.
