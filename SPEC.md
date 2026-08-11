# NamakPash — Rebuild Specification

A complete description of the app: what it does, how it is built, and every rule and trap that
shaped it. The goal is that this file alone is enough to rebuild the app from an empty folder.

Read sections 1–3 before writing any code. Sections 4–8 are the build order. Section 15 is the
list of things that will otherwise be discovered painfully, one at a time.

---

## 1. What the app is

A minimal, fully offline expense splitter for Android — a small personal Splitwise. You create a
trip, add participants by name, record expenses, and the app tells you who owes whom.

**Hard constraints, all deliberate:**

| | |
|---|---|
| Platform | Android only |
| Network | None. No backend, no accounts, no sync, no analytics |
| Permissions | **Zero** in the release build |
| Currency | Toman only, single currency, whole numbers |
| UI language | Persian (Farsi), full right-to-left |
| Calendar | Jalali (Shamsi) for display, Gregorian UTC for storage |
| Digits | Western (`450,000`), never Persian-Indic (`۴۵۰٬۰۰۰`) |

**Deliberately not built.** Do not add these back without being asked: multiple currencies,
accounts or login, sync or any backend, receipt photos, expense categories, multiple payers on one
expense, a settings screen, recurring expenses, budgets, notifications.

The guiding principle is *few options, fast flows*. Every screen should be usable in seconds. When
in doubt, remove the choice rather than add a preference.

---

## 2. Stack and layout

.NET 10 · .NET MAUI · C# · SQLite (`sqlite-net-pcl`) · `CommunityToolkit.Mvvm` · xUnit.

```
Splitt.Core/        # plain net10.0 class library — NO MAUI reference. This is the point.
  Models/           # Trip, Participant, Expense, ExpenseShare
  Services/         # EqualSplitter, BalanceCalculator, SettlementPlanner,
                    # ReportBuilder, ReportHtmlFormatter
  Helpers/          # MoneyFormat, PersianDate, Bidi
  Data/             # SplittDatabase
  Export/           # TripExporter (JSON in/out)
Splitt.Tests/       # xUnit over Splitt.Core only
Splitt.App/         # MAUI, net10.0-android ONLY
  ViewModels/       # one per page, CommunityToolkit.Mvvm source generators
  Views/            # one XAML page per view model
  Helpers/          # InvertedBoolConverter (keyed "Not" in App.xaml)
  Platforms/Android # HtmlToPdf, MainActivity, MainApplication, manifest overlays
  Resources/        # Styles, Fonts, AppIcon, Splash
```

**`Splitt.Core` must never reference MAUI.** That is what lets every money-critical rule be unit
tested on the host with no emulator. If a helper is needed by both the app and the report, it goes
in Core (this is why `MoneyFormat` and `PersianDate` live there and not in the app).

Project settings that matter:

- `TargetFrameworks` = `net10.0-android` only. iOS/Mac/Windows removed on purpose.
- `SupportedOSPlatformVersion` = `24.0` (Android 7.0). This is an API level, not a version number.
- `ApplicationId` = `ir.narimani.splitt` — **never change it**, or updates stop installing over
  existing copies and users lose their data.
- `ApplicationTitle` = `نمک‌پاش` — the launcher name.

---

## 3. Non-negotiable rules

These are the reasons the tests exist. Every one of them was a real decision; none is incidental.

1. **`decimal` for all money. Never `float` or `double`, anywhere, at any point.**

2. **Amounts persist as TEXT, not REAL.** `sqlite-net` maps `decimal` to `REAL` (a double), which
   silently corrupts values. Models therefore expose an `[Ignore] decimal` property backed by a
   `string` column parsed with `CultureInfo.InvariantCulture`. Same trick in the JSON export.

3. **Balances are always derived, never stored.** `net = paid − owed`, recomputed on every load. An
   edit or delete can then never leave a stale balance behind.

4. **Equal splits sum exactly to the total.** The rounding remainder is handed out one unit at a
   time to the first participants: `100,000 ÷ 3 → 33,334 + 33,333 + 33,333`, never `99,999`.

5. **A settlement is just an `Expense` with `IsSettlement = true`** — the payer is the debtor, and a
   single share belongs to the creditor. This is *why* the balance code needs no special case for
   settlements: recording one shifts the balances through the ordinary path.

6. **Settlement suggestions are deterministic.** Greedy: largest debtor pays largest creditor, ties
   broken on the lower participant id, at most `n − 1` transactions.

7. **The report is derived, never stored, and never exported.** The JSON backup stays
   source-of-truth only — no summary blocks in it. In the report, Paid/Owed count real expenses
   only; settlements are tracked separately as `SettledPaid`/`SettledReceived` so that «پرداخت»
   keeps meaning *trip spending*, while Net includes settlements so it matches the balances tab.
   Identity, test-covered:

   ```
   Net = Paid − Owed + SettledPaid − SettledReceived
   ```

8. **Sharing a report means a PDF**, generated on-device with no PDF library and no network.

9. **The release APK requests zero Android permissions.**

---

## 4. Data model

Four tables, created by an explicit `SplittDatabase.InitializeAsync()` (call it at startup).
All money is TEXT.

```csharp
[Table("Trip")]
class Trip {
    [PrimaryKey, AutoIncrement] int Id;
    string Name = "";
    DateTime CreatedAtUtc;
}

[Table("Participant")]
class Participant {
    [PrimaryKey, AutoIncrement] int Id;
    [Indexed] int TripId;
    string Name = "";
}

[Table("Expense")]
class Expense {
    [PrimaryKey, AutoIncrement] int Id;
    [Indexed] int TripId;
    string Description = "";
    string AmountRaw = "0";        // invariant-culture text
    [Ignore] decimal Amount;       // parses/formats AmountRaw
    int PaidById;                  // Participant.Id
    DateTime DateUtc;              // Gregorian UTC, carries a time of day
    bool IsSettlement;
}

[Table("ExpenseShare")]
class ExpenseShare {
    [PrimaryKey, AutoIncrement] int Id;
    [Indexed] int ExpenseId;
    [Indexed] int ParticipantId;
    string ShareRaw = "0";
    [Ignore] decimal Share;
}
```

**No share row means "did not take part."** That is different from a share of zero, and the report
relies on the distinction. Never write zero-value share rows to represent absence.

`SplittDatabase` is an async `sqlite-net-pcl` repository. Multi-table writes (an expense and its
shares) go in a transaction. The file lives at `FileSystem.AppDataDirectory/splitt.db3`.

Two query rules worth stating:

- Trips: `OrderByDescending(CreatedAtUtc)`.
- Expenses: `OrderByDescending(DateUtc).ThenByDescending(Id)`. The id tie-break is required — rows
  saved before times were recorded all share one timestamp, and without it SQLite returns them in
  rowid order, so days run newest-first while entries within a day run oldest-first.

---

## 5. Core algorithms

### EqualSplitter

```csharp
decimal[] Split(decimal total, int count)
```

`baseShare = floor(total / count)`; the remainder is distributed one unit at a time to the first
participants; any sub-unit remainder goes to the first share. Throws on `count <= 0` or
`total < 0`. Guarantee: the result always sums exactly to `total`.

### BalanceCalculator

```csharp
Dictionary<int, decimal> ComputeNet(participants, expenses, shares)
```

Start every participant at zero, add each expense's amount to its payer, subtract each share from
its participant. Positive is a creditor, negative a debtor. Settlements flow through unchanged.

### SettlementPlanner

```csharp
List<SettlementSuggestion> Plan(IReadOnlyDictionary<int, decimal> netBalances)
```

Drop zero balances. Loop: find the largest creditor and largest debtor (ties → lower id), transfer
`min(credit, debt)`, remove anyone who reaches zero, repeat until no debt or no credit remains.
Returns `(FromParticipantId, ToParticipantId, Amount)` records. At most `n − 1` transactions, and
the same input always produces the same output.

### ReportBuilder

Builds `TripReport(Total, AveragePerPerson, People[])`, where each `PersonReport` carries
`Paid, Owed, SettledPaid, SettledReceived, Net, PaidItems[], ShareItems[]`.

- `PaidItems` — non-settlement expenses this person paid, chronological.
- `ShareItems` — this person's shares of non-settlement expenses, chronological.
- `SettledPaid` / `SettledReceived` — settlement amounts paid and received.
- `Net` comes from `BalanceCalculator`, so the report can never disagree with the balances tab.

Everything is pure and order-independent: the output is sorted by `(DateUtc, Id)` regardless of
input order.

---

## 6. Screens and flows

Shell navigation with string routes and query-string parameters received via `[QueryProperty]`.
Pages load in `OnAppearing`, so returning from an editor refreshes automatically.

```
trips  ──▶  trip-editor            (new / edit a trip and its participants)
  │
  └──▶  trip-detail?tripId=N       (three tabs)
             └──▶  expense-editor?tripId=N[&expenseId=M]
```

Registration: `SplittDatabase` is a **singleton**; every view model and page is **transient** (a
fresh view model per navigation is what makes "new expense defaults to now" work).

### Trips list — `trips`

Cards showing trip name, `N نفر · M هزینه`, and the trip total. FAB `＋ سفر جدید`. Toolbar
`بازیابی` restores a JSON backup. Empty state: 🧳 `هنوز سفری نساخته‌ای` /
`اولین سفر یا دورهمی را بساز و هزینه‌ها را راحت دنگ کن.`

### Trip editor — `trip-editor`

Trip name (`مثلاً: سفر شمال`), participant entry (`نام (مثلاً: امیر)`) with `افزودن`, chips with
`✕` to remove, `ذخیره`. A participant who already appears in an expense cannot be deleted:
`کسانی که در هزینه‌ای سهیم بوده‌اند قابل حذف نیستند.`

### Trip detail — `trip-detail`

Three tabs: **هزینه‌ها**, **تراز و تسویه**, **گزارش**. Toolbar: `ویرایش`, `پشتیبان` (JSON export).

- **هزینه‌ها** — total header, then one card per expense: description, `پرداخت: {payer} · {date}`,
  and a `سهم‌ها:` line listing each participant's share. Settlement cards are tinted, show 🤝 and
  `{from} به {to} · {date}`, and tapping one offers to delete it. FAB `＋ هزینهٔ جدید`.
- **تراز و تسویه** — each person's net with a proportional bar, labelled `طلبکار` / `بدهکار` /
  `تسویه`, then `پیشنهاد تسویه` rows with a `ثبت تسویه` button that writes the settlement expense.
  When nothing is outstanding: `همه تسویه‌اند — چیزی برای پرداخت نمانده.`
- **گزارش** — `جمع هزینه‌ها`, `میانگین هر نفر`, and an expandable card per person showing their
  `پرداخت‌ها` and `سهم‌ها`. Button `ارسال گزارش` renders the PDF and opens the share sheet.

### Expense editor — `expense-editor`

Amount (large, `تومان`), payer chips (`چه کسی پرداخت کرد؟`), description
(`شرح (مثلاً: شام رستوران)`), the date row, split mode `مساوی` / `دستی`
(`بین چه کسانی تقسیم شود؟`) with a checkbox per participant, `ذخیره`, and `حذف هزینه` when editing.

Behaviour that matters:

- Payer defaults to the payer of the most recent expense — a fast-flow shortcut.
- Manual split shows a running hint and refuses to save unless the shares sum **exactly** to the
  total: `جمع سهم‌ها باید دقیقاً برابر مبلغ کل باشد.`
- Editing detects an unequal split by comparing the stored shares against `EqualSplitter`'s output,
  so reopening an expense shows its true state.
- The date dialog (`انتخاب تاریخ`) has three pickers — روز / ماه / سال — plus `امروز`, `تأیید`,
  `انصراف`. Years span `currentYear − 3 … currentYear + 1`.

**Date semantics.** A new expense starts at `DateTime.Now`, keeping the current time of day. The
picker sets only the *day*; the time of day rides along untouched. Editing an existing expense
keeps its original time, so editing never bumps it up the list.

---

## 7. The report

### In-app

Per-person expandable cards, as described above.

### PDF (`ReportHtmlFormatter`, Core, pure)

A4 at 72 dpi: **595 × 842**, margin 40, footer band 46, so **usable content height = 756** and
**content width = 515**. Sections in order:

1. **Header** — eyebrow `گزارش سفر`, trip name, `{N} نفر · {M} هزینه · تاریخ گزارش: {date}`, then
   two cards: `جمع هزینه‌ها` and `میانگین هر نفر`.
2. **خلاصهٔ افراد** — نام / پرداخت / سهم / وضعیت, with a pill for طلبکار / بدهکار / تسویه.
3. **ریز هزینه‌ها** — شرح / تاریخ / پرداخت‌کننده / مبلغ, chronological.
4. **سهم هر نفر از هر هزینه** — the matrix, below.
5. **تسویه‌های ثبت‌شده** — از / به / تاریخ / مبلغ.
6. **پیشنهاد تسویه** — از / به / مبلغ.

Every page carries a footer: `نمک‌پاش` and `صفحهٔ i از n`.

### The matrix

One row per expense, one column per person, each cell that person's share.

- First column is the description with its date and time stacked beneath it — a date alone does not
  identify a row.
- **A dash (`—`) marks someone with no share row.** Not a zero: zero reads as "took part, owed
  nothing".
- **The payer's own cell is tinted**, so the grid shows who fronted the money as well as who owes.
- A **totals row** ends the table. Every column must equal that person's `Owed` in خلاصهٔ افراد, and
  every row must equal the expense amount. That reconciliation is the point of the row — keep it.
- **Settlements are excluded.** A settlement is not anyone's share of a cost, and including one
  would break what a column means.
- **Columns are chunked at 5 people.** Past that, amounts stop fitting 515px, so the table repeats
  for the next group with the description column intact. Nothing is shrunk or truncated, and small
  groups pay nothing for the rule.

### Pagination

The PDF writer draws a laid-out page onto a canvas and **cannot honour CSS page-break rules**, so
pagination is arithmetic in C#: every block declares a fixed height and blocks are packed into
pages.

```
Header 168 · SectionTitle 40 · TableHead 28 · Row 26 · Spacer 16
MatrixHead 30 · MatrixRow 34 · MatrixTotals 30 · Caption 18
```

Rules the packer enforces:

- A section heading is never left as the last thing on a page — it moves down with the two blocks
  that follow it.
- When a table continues onto a new page, its column headings repeat. This keys off an explicit
  `Block.Body` flag, **not** row height (matrix rows are taller — that is exactly what broke the
  original height-based check).
- A spacer never starts a page.
- Rows are single-line and ellipsised, so a row's height is a constant.

`Format(...)` returns `(html, pageCount)`. **The page count sizes the writer's canvas**, so if the
two ever disagree you get blank or truncated pages. A test asserts they match.

---

## 8. Rendering the PDF on Android

`Splitt.App/Platforms/Android/HtmlToPdf.cs`. An offscreen `WebView` lays out the HTML and is drawn
onto an `Android.Graphics.Pdf.PdfDocument` canvas, one page at a time with the canvas translated by
`-pageIndex * 842`. Text stays vector text; Persian shaping and RTL come free from the web engine.

Four requirements, each of which produces a silent failure if missed:

1. **The WebView must be attached to the window.** Detached, it never initialises its renderer and
   draws nothing at all — you get a correctly-paged PDF with no content and no font objects. Add it
   to `Android.Resource.Id.Content` at full size with `TranslationX = 10000` so it renders for real
   without being visible, and remove it afterwards.
2. **`SetLayerType(Software, null)`.** A PdfDocument canvas cannot record hardware layers.
3. **`UseWideViewPort = true` and `SetInitialScale(100)`.** Otherwise the WebView ignores the page
   viewport and lays out in density-independent pixels, so content renders at the screen's density
   (~2.6×) and only a corner lands on the page.
4. **Fonts load from `file:///android_asset/`.** `MauiFont` files land in the APK's `assets/`, so
   `LoadDataWithBaseURL("file:///android_asset/", …)` lets `@font-face` reach Vazirmatn. Pass that
   base URL into `ReportHtmlFormatter.Format`.

**Do not try to use Android's own print adapter**, even though it would paginate for you:
`PrintDocumentAdapter.LayoutResultCallback` and `WriteResultCallback` have no public Java
constructor, so the C# binding exposes only `(nint, JniHandleOwnership)` and they cannot be
subclassed. This is a compile error, not a preference.

The PDF is written to `FileSystem.CacheDirectory` and handed to `Share.Default.RequestAsync` as a
`ShareFileRequest`.

---

## 9. Formatting

### Money — `MoneyFormat`

- `Format(1234567)` → `"1,234,567"` (`"#,0"`, invariant culture).
- `FormatToman(...)` appends `" تومان"`.
- `Parse` tolerates thousands separators, spaces, directional marks, and Persian (`۰-۹`) or
  Arabic-Indic (`٠-٩`) digits; returns `null` on anything else.

### Dates — `PersianDate`

Display only. Storage is always Gregorian UTC; convert with `.ToLocalTime()` at the edge.

- `ToDisplay` → `1405/05/20`
- `ToLongDisplay` → `20 مرداد 1405`
- `ToDisplayWithTime` → `1405/05/20 16:11`, **but the time is omitted when it is exactly local
  midnight**. Expenses saved before times were recorded sit at midnight, and printing `00:00` for
  them would be a fabrication. This is why no migration was ever needed.
- `FromJalali`, `DaysInMonth`, `ToJalali` wrap `System.Globalization.PersianCalendar`.

Month names: فروردین، اردیبهشت، خرداد، تیر، مرداد، شهریور، مهر، آبان، آذر، دی، بهمن، اسفند.

### Bidirectional text — `Bidi`

Two *different* bugs, each needing its own fix. Test both with **Latin** names — Persian names hide
them.

1. **A Persian line starting with user text flips to LTR.** Android and chat apps pick direction
   from the first strong character, so `"Sara به Amir"` lays out left-to-right and an RTL reader
   sees `"Amir به Sara"`. Fix: `Bidi.Rtl(line)` prefixes RLM (U+200F). Applied to settlement rows,
   suggestion rows, and the settlement card meta.
2. **Two numbers separated by a space swap places.** `"1405/03/09 14:32"` renders as
   `"14:32 1405/03/09"`, because the space between two numeric runs takes the paragraph direction
   (UAX #9 rule N1). `Bidi.Rtl` does **not** fix this. Fix: `Bidi.Ltr(run)` wraps the whole token in
   LRI (U+2066) … PDI (U+2069). Applied by `ToDisplayWithTime`.

---

## 10. Design system

`Resources/Styles/Colors.xaml` and `Styles.xaml`. Every colour goes through `AppThemeBinding` so
dark mode works everywhere.

| Token | Light | Dark |
|---|---|---|
| Primary (accent teal) | `#14B8A6` | `#2DD4BF` |
| PrimarySoft | `#CCFBF1` | `#134E48` |
| Creditor (green) | `#16A34A` | `#4ADE80` |
| Debtor (red) | `#DC2626` | `#F87171` |
| Background | `#F4F7F8` | `#0E1416` |
| Surface | `#FFFFFF` | `#1A2226` |
| SurfaceAlt | `#ECF1F3` | `#232D32` |
| Divider | `#E4EAEC` | `#2A363C` |
| TextPrimary | `#17242A` | `#E9EEF0` |
| TextSecondary | `#5E7178` | `#93A6AD` |

- Spacing scale 4 / 8 / 12 / 16 / 24. Cards 16 px rounded.
- Fonts: Vazirmatn, registered as `Vazirmatn`, `VazirmatnMedium`, `VazirmatnSemiBold`,
  `VazirmatnBold`.
- `FlowDirection="RightToLeft"` on the Shell.
- Android status and navigation bar colours come from
  `Platforms/Android/Resources/values/colors.xml` — a *different* file from `Colors.xaml`.

**Icon and splash.** A salt shaker pouring, its grains forking into two bowls. The launcher icon is
`Resources/AppIcon/appicon.svg` (solid teal background) + `appiconfg.svg` (white artwork); the
splash is its own file, `Resources/Splash/splash.svg`, carrying the mark plus the wordmark
`نمک‌پاش` **as path data** — no font is guaranteed at splash time, and a `<text>` element silently
falls back. Two constraints on the icon artwork:

- Android's guaranteed safe area is a **circle**, not a square. Content inside the bounding box can
  still be clipped: check `hypot(x − 228, y − 228) < 139` on a 456 canvas for the extreme points.
  The artwork is scaled to 0.86 for exactly this reason.
- The shaker's cap holes are filled with the background teal rather than left transparent, so
  changing `MauiIcon`'s `Color` means changing them in the SVG too.

---

## 11. Backup: JSON export and import

`TripExporter` writes a single trip:

```json
{
  "SchemaVersion": 1,
  "TripName": "...",
  "CreatedAtUtc": "...",
  "Participants": [ { "Id": 1, "Name": "..." } ],
  "Expenses": [ {
    "Description": "...", "Amount": "1000000", "PaidById": 1,
    "DateUtc": "...", "IsSettlement": false,
    "Shares": [ { "ParticipantId": 1, "Share": "250000" } ]
  } ]
}
```

Amounts travel as **strings** so no JSON reader can degrade them to double. Participant ids are
local to the file: `ImportTripAsync` remaps them through an id map as it inserts. Import always
creates a **new** trip — it never merges into or overwrites an existing one — and the whole insert
runs in a single transaction. It validates ids and amounts and rejects malformed files. **No report
or summary data goes in the backup** — it is source-of-truth only.

Both export and import go through the system share sheet / file picker; the app never touches the
filesystem outside its own sandbox and the cache directory.

---

## 12. Language policy

Get this right in both directions:

- **Persian** — everything the user sees in the running app: labels, buttons, alerts, exception
  messages shown to users, `ApplicationTitle`.
- **English** — everything developer-facing or public: this file, the README, code comments, XML
  doc summaries, test names and comments, commit messages, GitHub release titles and notes.

Persian inside an English comment is fine when it *is* the value being documented
(`/// <summary>1234567 → "1,234,567 تومان"</summary>`), never as prose. Check with:

```bash
grep -nP '(//|///|<!--).*[\x{0600}-\x{06FF}]' $(git ls-files '*.cs' '*.xaml')
grep -nP '[\x{0600}-\x{06FF}]' README.md
```

---

## 13. Build, sign, release

```bash
dotnet test                                          # must stay green
dotnet build Splitt.App -f net10.0-android           # debug
dotnet build Splitt.App -f net10.0-android -t:Run    # build, deploy, launch
```

Release:

```bash
dotnet publish Splitt.App -f net10.0-android -c Release \
  -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=../keys/splitt.keystore \
  -p:AndroidSigningKeyAlias=splitt \
  -p:AndroidSigningKeyPass="$PASS" -p:AndroidSigningStorePass="$PASS"
```

Bump **both** `ApplicationDisplayVersion` and `ApplicationVersion` for every release, or Android
refuses the update.

**Update safety.** An update installs over an existing copy without data loss only if all three
hold. Verify before telling anyone it is safe:

```bash
aapt dump badging  <apk> | grep '^package:'              # same id, higher versionCode
apksigner verify --print-certs <apk> | grep 'SHA-256'    # identical to the previous release
```

Losing the keystore means users must uninstall and lose their data. It is the single irreplaceable
artifact in the project.

**Permissions.** `INTERNET` and `ACCESS_NETWORK_STATE` exist only in
`Platforms/Android/AndroidManifestOverlay.Debug.xml`, merged by an `AndroidManifestOverlay` item
conditioned on `'$(Configuration)' == 'Debug'` (debug deploy and hot reload need them). Verify:

```bash
aapt dump permissions dist/<apk>
# only ir.narimani.splitt.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION should appear
```

**Always run a Release build on a device before shipping** — trimming breaks things that work in
Debug.

---

## 14. Testing

xUnit over `Splitt.Core` only; no emulator needed. What must be covered:

- **EqualSplitter** — even totals, uneven totals summing exactly, `1 ÷ 3`, large counts.
- **BalanceCalculator** — nets sum to zero; settlements shift balances.
- **SettlementPlanner** — always terminates and fully settles, including random and adversarial
  balance sets; at most `n − 1` transactions; deterministic.
- **ReportBuilder** — the `Net = Paid − Owed + SettledPaid − SettledReceived` identity; settlements
  excluded from Paid/Owed.
- **ReportHtmlFormatter** — page count matches the sections emitted (the writer sizes its canvas
  from it); continued tables repeat headings; every page has a footer; the matrix gives each person
  a column and each expense a row; dash for non-participants; payer cell tinted; chunking at 5;
  settlements excluded; HTML escaping of names and descriptions.
- **PersianDate** — known Gregorian↔Jalali pairs including two Nowruz dates, `HH:mm` under a
  Persian culture, midnight omitted.
- **Bidi** — RLM prefix; LTR isolate wrapping.
- **Export/import** — round-trip fidelity; malformed input rejected.

Verify UI changes with an emulator screenshot rather than assuming they render. Driving with
`adb shell input tap X Y` works well for smoke tests.

---

## 15. Traps

Every one of these cost real time. They are not hypothetical.

**MAUI / Android**

- **Never assign to an `Entry.Text` from inside its own `TextChanged` or an `[ObservableProperty]`
  setter callback.** Android's EmojiCompat throws
  `IllegalArgumentException: end should be < than charSequence length` and the app dies. The amount
  field therefore shows raw digits while typing, mirrors a formatted value into a separate preview
  `Label`, and only reformats itself on `Unfocused`.
- **`BoxView Color="Transparent"` renders as a grey block**, because the implicit `BoxView` style
  sets `BackgroundColor` — a different property. Use an empty `Grid` with `HeightRequest` instead.
- `Page.DisplayAlert` is obsolete in .NET 10 — use `DisplayAlertAsync`.
- A `Picker`'s `SelectedIndex` binding is two-way; clearing its `ItemsSource` can write `-1` back
  into the view model.

**Assets**

- **Editing `appiconfg.svg` alone does not regenerate the launcher icon.** Resizetizer reuses cached
  mipmaps, so the build succeeds and the device still shows the old icon. Delete
  `obj/<Config>/net10.0-android/resizetizer` and `adb uninstall` before rebuilding, then confirm the
  timestamp on `resizetizer/r/mipmap-xxhdpi/appicon_foreground.png` actually moved.
- Don't debug the splash via `obj/.../lp/*/jl/res/drawable/maui_splash_image.xml` — that copy is an
  empty `<layer-list />` by design. The real assets are under `resizetizer/sp/drawable*/`, and note
  the separate `drawable-v31` variant is what API 31+ uses.
- The generated splash PNG is white-on-transparent; opening it in a viewer shows a blank rectangle.
  Composite it over the splash colour before concluding anything is broken.

**Testing on a device**

- Release and Debug are signed with different keys, so **installing one over the other wipes app
  data**, and `adb shell run-as` stops working against a Release build (not debuggable). Seed test
  data through the UI for Release runs, not by pushing a database.
- The emulator restores its clock from a snapshot and has no NTP. If a date looks wrong, check
  `adb shell date` against the host **before** suspecting the calendar conversion. Cold boot with
  `-no-snapshot-load` to resync.

---

## 16. Build order

If starting from an empty folder, this order keeps everything testable as it grows:

1. `Splitt.Core` models + `SplittDatabase`, with the TEXT-money pattern from the first commit.
2. `EqualSplitter`, `BalanceCalculator`, `SettlementPlanner` + their tests. No UI yet.
3. `MoneyFormat`, `PersianDate`, `Bidi` + tests.
4. MAUI shell, design system, fonts, RTL, dark mode.
5. Trips list → trip editor → trip detail (expenses tab) → expense editor.
6. Balances tab and settlement recording.
7. `ReportBuilder` + the report tab.
8. JSON export/import.
9. `ReportHtmlFormatter` + `HtmlToPdf` + sharing.
10. Icon, splash, release signing, update-safety checks.
