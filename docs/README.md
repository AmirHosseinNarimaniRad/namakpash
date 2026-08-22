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

## Going live: the switches

Anything not published yet is a `null` in the `CONFIG` block near the bottom of `index.html`. The
page reads them at load and reshapes itself — no other edits needed.

```js
var CONFIG = {
  version:   "1.3.1",
  bazaarUrl: "https://cafebazaar.ir/app/ir.narimani.splitt",
  myketUrl:  null,   // "https://myket.ir/app/ir.narimani.splitt"
  pwaUrl:    null    // "/app/"
};
```

- **`bazaarUrl`** — the only Android channel the page offers. The direct-APK link was removed once
  Bazaar went live: Bazaar delivers updates automatically, which a sideloaded APK cannot. The APK
  still exists on the GitHub releases page for anyone who wants it, but the site no longer points
  there. `apkUrl` is gone from `CONFIG` entirely — do not re-add it without deciding what it is for.
- **`myketUrl`** — adds a third download card. Leave `null` to not mention Myket at all.
- **`pwaUrl`** — until set, iOS visitors are told plainly that the web version is not ready.
  Once set, they get the install button and the Safari "Add to Home Screen" steps.

Bump `version` on each release so the footer stays honest.

## Platform behaviour

The page does not present Android and PWA as equal choices. Android visitors are steered to Bazaar
because browser storage is evictable and the app keeps everything on-device with no backend — an
Android user who picks the web version gets weaker data durability for no gain.
iOS visitors are the web version's actual audience.

It also detects in-app browsers (Telegram, Instagram, WhatsApp, …) and shows a banner asking the
user to reopen in Safari. This matters more than it looks: iOS has no "Add to Home Screen" inside
those webviews, and links in this audience are mostly shared through exactly those apps.

## Deploying

Live at <https://namakpash.namakco.ir>. GitHub Pages serves this folder directly from `main` —
that is why it is named `docs/`, which is the only subfolder Pages will serve without a build
workflow. Pushing to `main` deploys; there is nothing to run.

`CNAME` holds the custom domain and must survive any reorganisation of this folder — Pages reads
it from the published root, and losing it silently reverts the site to `*.github.io`.

An Actions workflow was tried first and abandoned: pushing anything under `.github/workflows/`
needs the `workflow` token scope, which the `gh` login on the build machine does not have. That is
the whole reason this folder is `docs/` — it is the only subfolder Pages will serve without one.

DNS lives at ArvanCloud, delegated from IRNIC. The free plan allows one record *per type*, which
is why the app sits on a subdomain via the single `CNAME` record rather than on the apex:

```
CNAME   namakpash   amirhosseinnarimanirad.github.io
A       @           185.199.108.153
```

A second product subdomain would need a second CNAME record, so it would also need a DNS provider
without that limit. There is no server state, so mirroring to a second host later costs nothing.

Note for when the PWA lands: GitHub Pages will not negotiate `Content-Encoding: br`, so a Blazor
bundle's Brotli assets are served uncompressed. That is a reason to reconsider the host for
`/app/`, not for this page.

## Behaviour worth knowing before editing

**Theme.** Both pages define their palette on `:root`, override it under
`@media (prefers-color-scheme:dark)` guarded by `:root:not([data-theme=light])`, and again under
`:root[data-theme=dark]` so a manual choice wins either way. The sun/moon button writes
`namakpash-theme` to `localStorage`; an inline script in `<head>` applies it before first paint,
otherwise a stored dark theme flashes light on load. With nothing stored the page follows the
system, which is the default for anyone who never touches the button. The key is shared between
`index.html` and `privacy.html`, so the choice carries across both.

**Links.** `linkAttrs()` in `index.html` adds `target="_blank" rel="noopener"` to anything matching
`^https?://`. Store links leave the site, and tapping one used to replace the page, so anyone
installing the app lost it. The protocol test is deliberate: in-page anchors (`#download`, `#ios`)
must not spawn tabs, and a future `myketUrl` picks up the behaviour without another edit.

**Bidi.** A run of Latin or numeric text inside a Persian sentence reorders (UAX#9). `privacy.html`
isolates `<code>` with `direction:ltr; unicode-bidi:isolate`, and one sentence there was rewritten
to carry a single chip rather than two — two LTR islands in one RTL line scrambled the reading
order even with isolation. The same class of bug is documented in `../CLAUDE.md` for the app.

**Backup.** `privacy.html#backup` is the only place that explains what destroys a user's data and
how to keep it. It is written against the real flow: «پشتیبان» inside an event, «بازیابی» on the
event list, one JSON file **per event**. Re-read `TripDetailViewModel.ExportCommand` before
changing any of those words.

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
