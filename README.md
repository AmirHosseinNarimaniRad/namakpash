# NamakPash

A minimal, fully offline expense-splitting app for Android (a small, personal Splitwise), built with .NET MAUI and C#.
Persian (Farsi) UI with full right-to-left layout, Toman amounts, and Jalali dates.

> The name — *NamakPash* ("salt shaker") — is a Persian expression for splitting a shared bill.
> The app's own interface is entirely in Persian; this repository is documented in English.

---

## Features

- **Events** — create an event and add participants by name. An event is anything a bill gets
  split over: a trip, a dinner, a party, a restaurant. No accounts, no login, no sync.
  The Persian UI calls this a رویداد; the model and the SQLite table are still named `Trip`.
- **Expenses** — record who paid, how much, a description, the date, and who shares the cost.
  Split equally by default, or enter custom unequal amounts per person. The time of entry is kept
  alongside the date, so same-day expenses stay in the order you added them, newest first. The date
  picker has a **Today** shortcut.
- **Edit & delete** — balances always recompute from the expense list; nothing is cached.
- **Balances** — each person's net position (creditor / debtor) with color coding and a proportional bar.
- **Settle up** — a greedy debt-simplification algorithm proposes the shortest list of
  "X pays Y amount Z" transactions (at most *n − 1* for *n* people).
- **Record a settlement** — stored as a flagged expense, so the balance math never needs a special case.
- **Report** — a per-person breakdown of the whole event: what each person paid and what their
  share was, expense by expense, plus the event total and per-person average. Every expense card
  also lists each participant's share at a glance.
- **Share as PDF** — one tap renders the report as an A4 PDF and hands it to the Android share
  sheet: event header, totals, a per-person summary, the itemized statement, recorded settlements and
  the transfers still outstanding. Paginated properly, with table headings repeated when a table
  continues onto the next page. Generated on-device with no PDF library and no network.
- **Expense matrix** — the heart of the report: one row per expense, one column per person, each
  cell that person's share. A dash marks someone who sat an expense out; the payer's own cell is
  tinted. Every column totals to that person's share and every row to the expense amount, so the
  report reconciles against itself. Past five people the columns are split into a second table
  rather than shrunk, so nothing is ever truncated.
- **Backup** — export an event to JSON via the share sheet, import it back on any device.
- **Dark mode** and a consistent card-based design system.

## Correctness guarantees

Money handling is the part of an app like this that is worth being strict about, so it is covered by unit tests:

- **`decimal` everywhere.** No `float` or `double` touches a monetary value at any point. SQLite would
  store a `decimal` as `REAL` (a double), so amounts are persisted as invariant-culture **TEXT** and
  parsed back to `decimal` — see `AmountRaw` / `ShareRaw` in `Splitt.Core/Models`.
- **Splits always sum exactly to the total.** When dividing equally, the rounding remainder is
  distributed one unit at a time, so 100,000 ÷ 3 becomes 33,334 + 33,333 + 33,333 — never 99,999.
- **Balances are derived, never stored.** `net = (sum paid) − (sum owed)`, recomputed from the
  expense list on every load, so an edit or delete can't leave a stale balance behind.
- **Settlement always terminates and fully settles.** Verified against random and adversarial balance sets.

## Tech stack

| | |
|---|---|
| Framework | .NET 10 · .NET MAUI (Android only) |
| Language | C# |
| Storage | SQLite via `sqlite-net-pcl`, local file, fully offline |
| Permissions | None — the release APK requests no Android permissions at all, including `INTERNET` |
| MVVM | `CommunityToolkit.Mvvm` |
| Tests | xUnit — 63 tests over the balance, settlement, report and date logic |
| Font | [Vazirmatn](https://github.com/rastikerdar/vazirmatn) |

## Download

Signed APKs are attached to each [release](https://github.com/AmirHosseinNarimaniRad/namakpash/releases).
Android 7.0 (API 24) or newer.

Updates install over an existing copy without touching your data — the package id and signing key
stay the same across releases, so never uninstall first.

## Project structure

```
Splitt.Core/      # No MAUI dependency — runs and tests on the host
  Models/         # Trip, Participant, Expense, ExpenseShare
  Services/       # EqualSplitter, BalanceCalculator, SettlementPlanner,
                  # ReportBuilder, ReportHtmlFormatter (report layout + pagination)
  Helpers/        # MoneyFormat, PersianDate (Jalali), Bidi (RTL text direction)
  Data/           # SplittDatabase (async sqlite-net-pcl repository)
  Export/         # JSON export / import with validation
Splitt.Tests/     # xUnit tests for the money-critical logic
Splitt.App/       # .NET MAUI app, net10.0-android
  Resources/      # Design system (Colors.xaml, Styles.xaml), Vazirmatn font
  ViewModels/     # Trips, TripEditor, TripDetail, ExpenseEditor
  Views/          # Matching XAML pages
  Helpers/        # Value converters
  Platforms/      # Android: HtmlToPdf (WebView -> PdfDocument canvas)
```

The data and logic layer is a plain class library with no UI dependency, which is what lets the
correctness-critical code be unit tested without an emulator.

[`SPEC.md`](SPEC.md) documents the whole app end to end — the rules, algorithms, screens, report
layout, rendering pipeline and the traps behind them — in enough detail to rebuild it from an
empty folder.

## Design notes

- **Accent** teal `#14B8A6`; creditors green, debtors red; 4/8/12/16/24 spacing scale; 16 px rounded cards.
- **Digits** are Western (`450,000`) even in Persian text, including Jalali dates (`1405/03/09`) — a
  deliberate choice, as they are easier to scan for amounts.
- **Dates** are stored as Gregorian UTC and only converted to Jalali for display, via .NET's
  built-in `PersianCalendar`.
- **Text direction** is forced right-to-left on any line that can begin with user-entered text
  (`Bidi.Rtl`). Without it, a Persian sentence starting with a Latin name is laid out
  left-to-right and reads reversed — "Sara pays Amir" becomes "Amir pays Sara".
  A second, unrelated case: two numbers separated by a space (a date and a time) are each laid out
  left-to-right, but the space between them takes the paragraph direction, so they swap. Such a run
  is wrapped in a directional isolate (`Bidi.Ltr`).

## Building

Requires the .NET 10 SDK with the MAUI workload, JDK 17, and the Android SDK.

```bash
dotnet workload install maui        # if not already installed

dotnet test                         # run the unit tests
dotnet build Splitt.App -f net10.0-android            # debug build
dotnet build Splitt.App -f net10.0-android -t:Run     # deploy to a running emulator/device
```

### Release build

Signing material is deliberately **not** in this repository. Generate your own keystore:

```bash
keytool -genkeypair -v -keystore keys/release.keystore -alias splitt \
  -keyalg RSA -keysize 2048 -validity 10000
```

```bash
dotnet publish Splitt.App -f net10.0-android -c Release \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore=../keys/release.keystore \
  -p:AndroidSigningKeyAlias=splitt \
  -p:AndroidSigningKeyPass=YOUR_PASSWORD \
  -p:AndroidSigningStorePass=YOUR_PASSWORD
```

The signed APK lands in `Splitt.App/bin/Release/net10.0-android/publish/`.

> Keep the keystore and its password backed up somewhere safe. Android will refuse to install an
> update signed with a different key, so losing it means users must uninstall and lose their data.

## Installing on a phone

```bash
adb install namakpash-v1.2.apk
```

Or copy the APK to the phone, tap it in a file manager, and allow installation from unknown sources
when prompted.

## Scope

Intentionally kept small. Not planned: multiple currencies, accounts, sync or any backend, receipt
photos, expense categories, or multiple payers on a single expense.

## License

MIT
