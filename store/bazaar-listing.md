# Cafe Bazaar listing copy

Persian, because it is what users read. Paste each block into the matching field in the
developer panel. Character limits differ between Bazaar and Myket and change over time —
check the counter in the panel and trim from the end of the long description if needed.

---

## عنوان (app title)

```
نمک‌پاش
```

If the panel wants something more descriptive for search:

```
نمک‌پاش — تقسیم هزینه‌های مشترک
```

---

## توضیح کوتاه (short description, ~80 chars)

```
خرج سفر و دورهمی را دقیق بین دوستان تقسیم کن. کاملاً آفلاین، بدون حساب کاربری.
```

---

## توضیح کامل (full description)

```
نمک‌پاش خرج مشترک را بین چند نفر تقسیم می‌کند و می‌گوید آخرش چه کسی به چه کسی چقدر بدهکار است.

برای سفر، دورهمی، شام رستوران، مهمانی یا قبض‌های خانهٔ مشترک — هر جایی که چند نفر خرج می‌کنند
و آخرش باید حساب‌وکتاب شود.

■ چرا نمک‌پاش

• کاملاً آفلاین است. هیچ سروری در کار نیست و داده‌ات از گوشی بیرون نمی‌رود.
• هیچ دسترسی‌ای نمی‌گیرد — حتی دسترسی به اینترنت.
• حساب کاربری، ثبت‌نام، ایمیل و شمارهٔ تلفن نمی‌خواهد. باز می‌کنی و شروع می‌کنی.
• به تومان، با تاریخ شمسی و رابط کاملاً راست‌به‌چپ.

■ چه کار می‌کند

• رویداد بساز — سفر، دورهمی، رستوران یا قبض‌های خانه — و اسم افراد را وارد کن.
• هزینه‌ها را ثبت کن: چه کسی پرداخت کرد، چقدر، و بین چه کسانی تقسیم شود.
• هر هزینه می‌تواند فقط بین بعضی از افراد تقسیم شود؛ لازم نیست همه شریک باشند.
• تقسیم مساوی یا وارد کردن سهم دستی برای هر نفر.
• تراز هر نفر: چه کسی طلبکار است و چه کسی بدهکار.
• پیشنهاد تسویه: کوتاه‌ترین فهرست پرداخت‌ها تا حساب همه صاف شود.
• گزارش کامل به صورت فایل PDF، آمادهٔ فرستادن در گروه.
• ماتریس هزینه‌ها: یک سطر برای هر هزینه و یک ستون برای هر نفر، تا معلوم باشد هر عدد از کجا آمده.
• پشتیبان‌گیری: هر رویداد را در یک فایل ذخیره کن و هر وقت خواستی برگردان.
• حالت تیره.

■ حساب دقیق

وقتی مبلغ بر تعداد افراد بخش‌پذیر نیست، نمک‌پاش باقی‌مانده را یک تومان یک تومان بین نفرها
پخش می‌کند. مثلاً 100,000 بین 3 نفر می‌شود 33,334 و 33,333 و 33,333 — جمع سهم‌ها دقیقاً
برابر همان مبلغ است و هیچ‌وقت یک تومان گم نمی‌شود.

■ حریم خصوصی

داده‌های تو — نام رویدادها، نام افراد و مبلغ‌ها — فقط در حافظهٔ خود گوشی ذخیره می‌شوند.
چون برنامه هیچ دسترسی‌ای به اینترنت ندارد، حتی اگر بخواهد هم نمی‌تواند چیزی بفرستد.

چون داده فقط روی گوشی توست، اگر برنامه را حذف کنی یا گوشی را عوض کنی از بین می‌رود.
برای همین از هر رویداد پشتیبان بگیر و فایلش را جایی امن نگه دار.
```

---

## دسته‌بندی (category)

Primary: **مالی و بانکداری** — it is a money app and that is where users look for one.
Fallback if the panel objects (some stores expect that category to mean banking):
**ابزارها** or **بهره‌وری**.

---

## کلمات کلیدی (keywords / tags)

```
تقسیم هزینه، دنگ، خرج مشترک، حساب‌وکتاب، تسویه حساب، سفر، دورهمی، هم‌خانه‌ای،
صورتحساب مشترک، مدیریت خرج، بدون اینترنت، آفلاین
```

Note the search terms are deliberately *not* the app's own vocabulary. In the app a container
is a «رویداد», but nobody searches for that — they search «تقسیم هزینه» and «دنگ».

Do not put competitor names (Splitwise and its transliterations) in the description. Some stores
treat that as a listing violation, and it is not worth the risk for the traffic it would bring.

---

## Assets to upload

| Field | File |
|---|---|
| آیکون | `store/icon-512.png` (512×512) |
| تصاویر | `store/screenshots/01-trips.png` … `07-dark-balances.png` (1080×2424) |
| فایل نصبی | `dist/namakpash-v1.3.1.apk` (versionCode 5) |
| حریم خصوصی | the deployed URL of `docs/privacy.html` — required at submission |

Upload the screenshots in numbered order: the list, expenses, balances and report first, the
editor and the two dark-mode shots after. `08-report-full.png` is the whole PDF report and is
taller than a phone screen — use it only where a store allows a long image, not as a phone
screenshot.

---

## Answers the review form usually asks for

- **Does the app need an account?** No.
- **Does it collect personal data?** No. Nothing leaves the device; the app has no network access.
- **Ads / in-app purchase?** None.
- **Target audience:** general.
- **Test account for reviewers:** not applicable — there is no login. Reviewers can open the app
  and create an event straight away.
