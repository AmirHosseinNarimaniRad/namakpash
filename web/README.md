# NamakPash landing page

A static, Persian, right-to-left landing page for the app. Two pages, no build step, no
dependencies, and **zero external requests** — fonts, icon and images are all local, which is
both a performance decision for Iranian connections and a consistency one: the app claims to
talk to nobody, so its website should not phone home either.

```
index.html      the landing page
privacy.html    privacy policy — Cafe Bazaar and Myket require this URL at submission
assets/
  icon.svg      app mark, used as the favicon and as the source for store/icon-512.png
  fonts/        Vazirmatn, subset to Persian + Latin and converted to woff2 (~29 KB each)
  shots/        WebP screenshots for the page
```

Total page weight is roughly 350 KB including fonts and every image.

## Preview locally

Open `index.html` directly, or serve it if you want the paths to behave exactly as deployed:

```bash
python3 -m http.server 8000 --directory web
```

## Going live: the three switches

Everything that is not published yet is a `null` in the `CONFIG` block near the bottom of
`index.html`. The page reads them at load and reshapes itself — no other edits needed.

```js
var CONFIG = {
  version:   "1.3",
  apkUrl:    "https://github.com/AmirHosseinNarimaniRad/namakpash/releases/latest",
  bazaarUrl: null,   // "https://cafebazaar.ir/app/ir.narimani.splitt"
  myketUrl:  null,   // "https://myket.ir/app/ir.narimani.splitt"
  pwaUrl:    null    // "/app/"
};
```

- **`bazaarUrl`** — until set, the Android card offers the direct APK and says Bazaar is coming.
  Once set, Bazaar becomes the primary button and the APK drops to secondary.
- **`myketUrl`** — adds a third download card. Leave `null` to not mention Myket at all.
- **`pwaUrl`** — until set, iOS visitors are told plainly that the web version is not ready.
  Once set, they get the install button and the Safari "Add to Home Screen" steps.

Bump `version` on each release so the footer and the APK note stay honest.

## Platform behaviour

The page does not present Android and PWA as equal choices. Android visitors are steered to the
APK/Bazaar because browser storage is evictable and the app keeps everything on-device with no
backend — an Android user who picks the web version gets weaker data durability for no gain.
iOS visitors are the web version's actual audience.

It also detects in-app browsers (Telegram, Instagram, WhatsApp, …) and shows a banner asking the
user to reopen in Safari. This matters more than it looks: iOS has no "Add to Home Screen" inside
those webviews, and links in this audience are mostly shared through exactly those apps.

## Deploying

Any static host works. GitHub Pages is the cheapest starting point and needs no build:

```bash
# from a repo whose Pages source is the repository root
git subtree push --prefix web origin gh-pages
```

Or copy the `web/` contents into a dedicated repo and enable Pages on it. There is no server
state, so mirroring to a second host later costs nothing.

Note for when the PWA lands: GitHub Pages will not negotiate `Content-Encoding: br`, so a Blazor
bundle's Brotli assets are served uncompressed. That is a reason to reconsider the host for
`/app/`, not for this page.

## Regenerating the assets

Screenshots come from the emulator with a seeded demo database and Android's `sysui_demo` mode for
a clean status bar. Full-resolution PNGs for store listings live in `../store/screenshots/`.

The demo data is deliberately chosen to exercise the awkward cases in one screen: a four-person
trip with an uneven split, a participant excluded from one expense (the dash in the matrix), a
rounding remainder (100,000 ÷ 3 → 33,334 + 33,333 + 33,333) and a recorded settlement that leaves
one person at exactly zero. Two smaller events — a birthday dinner and shared house bills — sit
alongside it so the list shows the app is not only for travel.

Persian cannot be typed through `adb shell input text`, so the demo data is written straight into
the app's SQLite file rather than entered by hand. Note that dates are stored as .NET ticks in a
`bigint` column, and that an expense landing on exactly local midnight renders without a time
(by design — `PersianDate.ToDisplayWithTime` omits a zero time), which looks like a bug in a
screenshot. `run-as` only works on a Debug build.

The report shots (`08`, `09`) and `../store/نمونه-گزارش.pdf` are rendered from the real
`ReportHtmlFormatter` output rather than captured from the phone, so they are the same HTML the
app turns into a PDF. Printing that HTML needs two additions the app itself never wants, because
its writer paints each `.page` onto its own canvas: `@page { size: A4; margin: 0 }` and a
`break-after: page` on `.page`. Without them Chrome prints US Letter and flows the pages together.

Fonts were subset with `pyftsubset`, keeping `--layout-features='*'` — dropping the layout
features breaks Arabic shaping and the text renders as disconnected letters.
