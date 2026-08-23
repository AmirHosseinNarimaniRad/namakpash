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

**All 64 test cases pass in the browser. `Splitt.Core` needed no changes at all — `Data/` included.**

| Probe | Result |
|---|---|
| Runtime | .NET 10.0.10 · `browser-wasm` |
| Globalization / ICU | invariant mode **off**; `fa-IR` resolves to Persian (Iran) |
| `PersianCalendar` | `2026-08-23` → `1405/06/01` → back — Jalali works, Western digits intact |
| `decimal` arithmetic | `0.1 + 0.2 == 0.3` — real decimals, not doubles |
| Amount TEXT round-trip | `1234567.89` → `"1234567.89"` → `1234567.89` under InvariantCulture |
| Equal split remainder | `33334 + 33333 + 33333 = 100000` (invariant #4 holds) |
| SQLite (sqlite-net) | creates the schema and runs statements — with `WasmBuildNative` on |

Balances, settlement planning, the report builder, the PDF page-break arithmetic, Bidi, Persian
dates and the database round-trip all pass untouched.

## What this settles

- **The Blazor-over-TypeScript argument holds.** `decimal` survives, and the money logic, its
  invariants and its tests carry over with zero edits.
- **Globalization is not a problem.** The plan flagged a risk that Blazor might run
  globalization-invariant and break `PersianCalendar`. It does not; ICU data is present by default.
- **SQLite runs in the browser.** `sqlite-net` ships a `browser-wasm` build of `e_sqlite3`, and
  `<WasmBuildNative>true</WasmBuildNative>` links it in. `SplittDatabase` creates its schema and
  serves queries unmodified, so `Data/` does not have to be rewritten for the browser.
- The sqlite-net attributes sit on the models (`Trip`, `Expense`, `ExpenseShare`, `Participant`),
  not only in `Data/` — wider coupling than the plan assumed, and now harmless.

Requirements: the `wasm-tools` workload (`sudo dotnet workload install wasm-tools`) and
`<WasmBuildNative>true</WasmBuildNative>` in the csproj. Without both, the runtime throws
`DllNotFoundException: e_sqlite3` the moment a statement runs — the build only warns.

## What is still open

**Persistence, which is now the whole problem.** SQLite works, but Emscripten's filesystem lives in
memory: the database at `/tmp/…db3` is gone on reload. Something has to sync the file to OPFS or
IndexedDB, and *that* — not the query layer — is what the storage design has to solve. It is also
where the durability risk in the PWA plan concentrates, since browser storage is evictable in a way
an APK's private SQLite file is not.

Untested here and worth knowing before any UI work: download size with native relinking on, and
whether the same setup survives AOT (`RunAOTCompilation`).

This project is a spike. It is not the PWA and is not meant to grow into it.
