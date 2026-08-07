# NamakPash

A minimal, fully offline expense-splitting app for Android (a small, personal Splitwise), built with .NET MAUI and C#.
Persian (Farsi) UI with full right-to-left layout, Toman amounts, and Jalali dates.

> The name — *NamakPash* ("salt shaker") — is a Persian expression for splitting a shared bill.
> The app's own interface is entirely in Persian; this repository is documented in English.

---

## Features

- **Trips** — create a trip and add participants by name. No accounts, no login, no sync.
- **Expenses** — record who paid, how much, a description, the date, and who shares the cost.
  Split equally by default, or enter custom unequal amounts per person.
- **Edit & delete** — balances always recompute from the expense list; nothing is cached.
- **Balances** — each person's net position (creditor / debtor) with color coding and a proportional bar.
- **Settle up** — a greedy debt-simplification algorithm proposes the shortest list of
  "X pays Y amount Z" transactions (at most *n − 1* for *n* people).
- **Record a settlement** — stored as a flagged expense, so the balance math never needs a special case.
- **Report** — a per-person breakdown of the whole trip: what each person paid and what their
  share was, expense by expense, plus the trip total and per-person average. Every expense card
  also lists each participant's share at a glance.
- **Share as text** — one tap turns the report into a chat-ready Persian text message
  (totals, per-person summary, itemized statement, settlements) via the Android share sheet.
- **Backup** — export a trip to JSON via the share sheet, import it back on any device.
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
| Tests | xUnit — 40 tests over the balance, settlement and report logic |
| Font | [Vazirmatn](https://github.com/rastikerdar/vazirmatn) |

## Project structure

```
Splitt.Core/      # No MAUI dependency — runs and tests on the host
  Models/         # Trip, Participant, Expense, ExpenseShare
  Services/       # EqualSplitter, BalanceCalculator, SettlementPlanner
  Data/           # SplittDatabase (async sqlite-net-pcl repository)
  Export/         # JSON export / import with validation
Splitt.Tests/     # xUnit tests for the money-critical logic
Splitt.App/       # .NET MAUI app, net10.0-android
  Resources/      # Design system (Colors.xaml, Styles.xaml), Vazirmatn font
  ViewModels/     # Trips, TripEditor, TripDetail, ExpenseEditor
  Views/          # Matching XAML pages
  Helpers/        # MoneyFormat, PersianDate (Jalali), converters
```

The data and logic layer is a plain class library with no UI dependency, which is what lets the
correctness-critical code be unit tested without an emulator.

## Design notes

- **Accent** teal `#14B8A6`; creditors green, debtors red; 4/8/12/16/24 spacing scale; 16 px rounded cards.
- **Digits** are Western (`450,000`) even in Persian text, including Jalali dates (`1405/03/09`) — a
  deliberate choice, as they are easier to scan for amounts.
- **Dates** are stored as Gregorian UTC and only converted to Jalali for display, via .NET's
  built-in `PersianCalendar`.

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
adb install namakpash-v1.0.apk
```

Or copy the APK to the phone, tap it in a file manager, and allow installation from unknown sources
when prompted.

## Scope

Intentionally kept small. Not planned: multiple currencies, accounts, sync or any backend, receipt
photos, expense categories, or multiple payers on a single expense.

## License

MIT
