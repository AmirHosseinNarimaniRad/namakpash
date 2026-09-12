"""Build the Instagram story cards (1080x1920) into store/social/.

Copy is deliberately spoken Persian, not the written register the landing page uses:
a story competes with people's friends, not with other product pages.
"""
import base64, os, pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT  = ROOT / "store/social"
OUT.mkdir(parents=True, exist_ok=True)

def b64(p, mime):
    return "data:%s;base64,%s" % (mime, base64.b64encode((ROOT/p).read_bytes()).decode())

FONT_R = b64("docs/assets/fonts/Vazirmatn-Regular.woff2",  "font/woff2")
FONT_S = b64("docs/assets/fonts/Vazirmatn-SemiBold.woff2", "font/woff2")
FONT_B = b64("docs/assets/fonts/Vazirmatn-Bold.woff2",     "font/woff2")

SHOT_BALANCES = b64("store/screenshots/03-balances.png",    "image/png")
SHOT_DARK     = b64("store/screenshots/06-dark-expenses.png","image/png")

ICON_SVG = (ROOT/"docs/assets/icon.svg").read_text().replace(
    'width="456" height="456"', 'width="100%" height="100%"')

BASE = """<meta charset="utf-8">
<style>
@font-face{font-family:Vazirmatn;src:url(%(FR)s) format("woff2");font-weight:400;font-display:block}
@font-face{font-family:Vazirmatn;src:url(%(FS)s) format("woff2");font-weight:600;font-display:block}
@font-face{font-family:Vazirmatn;src:url(%(FB)s) format("woff2");font-weight:700;font-display:block}
:root{--teal:#14B8A6;--teal-lite:#2DD4BF;--teal-deep:#0F766E;
      --ink:#0B1113;--fg:#EAF2F3;--fg-dim:#9DB3B8}
*{margin:0;padding:0;box-sizing:border-box;min-width:0}
html,body{width:1080px;height:1920px;overflow:hidden}
body{font-family:Vazirmatn,system-ui,sans-serif;direction:rtl;text-align:center;
     background:var(--ink);color:var(--fg);-webkit-font-smoothing:antialiased}
/* the off-canvas glows must stay inside this clipped layer: in an RTL document any
   element past the 1080px edge shifts the whole page sideways in the render */
.bg{position:absolute;inset:0;overflow:hidden;pointer-events:none}
.glow{position:absolute;border-radius:50%%;filter:blur(130px)}
.g1{width:880px;height:880px;background:rgba(20,184,166,.28);top:-250px;right:-230px}
.g2{width:740px;height:740px;background:rgba(15,118,110,.32);bottom:-210px;left:-210px}
.grain{position:absolute;inset:0;opacity:.5;
  background-image:radial-gradient(rgba(255,255,255,.045) 1px,transparent 1px);
  background-size:5px 5px}
/* everything important stays inside Instagram's safe area */
.stage{position:absolute;inset:0;overflow:hidden;
  display:flex;flex-direction:column;align-items:center;padding:250px 72px}
.icon{width:%(IS)spx;height:%(IS)spx;border-radius:%(IR)spx;overflow:hidden;flex:none;
  box-shadow:0 30px 70px rgba(0,0,0,.55),0 0 0 1px rgba(255,255,255,.07)}
.brand{font-weight:700;letter-spacing:-1px}
.ltr{direction:ltr;unicode-bidi:isolate}
.teal{color:var(--teal-lite)}
.dim{color:var(--fg-dim)}
</style>
"""

# --------------------------------------------- 1 · the line everyone has heard
F1 = BASE % dict(FR=FONT_R, FS=FONT_S, FB=FONT_B, IS=104, IR=26) + """
<style>
.quote{font-size:94px;font-weight:700;line-height:1.44;letter-spacing:-2px}
</style>
<div class="bg"><div class="glow g1"></div><div class="glow g2"></div><div class="grain"></div></div>
<div class="stage" style="justify-content:center">

  <div class="dim" style="font-size:36px;font-weight:600">جملهٔ آخرِ هر دورهمی</div>

  <div class="quote" style="margin-top:52px">«باشه، بعداً<br>حساب می‌کنیم»</div>

  <div class="rule" style="margin:74px 0"></div>

  <div class="teal" style="font-size:50px;font-weight:700;line-height:1.6">
    هیچ‌وقت هم حساب نشد.
  </div>

  <div style="margin-top:170px;display:flex;align-items:center;gap:20px">
    <div class="icon">%(ICON)s</div>
    <div class="brand" style="font-size:56px">نمک‌پاش</div>
  </div>

</div>
""" % dict(ICON=ICON_SVG)

# --------------------------------------------- 2 · what it actually does
FEATURES = [
    ("کی طلبکاره، کی بدهکار",   "یه نگاه کافیه — با هر خرج جدید از نو حساب می‌شه"),
    ("چهار نفر، دو تا پرداخت",  "خودش کمترین جابه‌جایی پول رو پیدا می‌کنه"),
    ("گزارش PDF",              "بفرست تو گروه، بحث تمومه"),
]
rows = "".join("""
    <div class="feat"><span class="tick">✓</span>
      <span class="ftext"><b>%s</b><i>%s</i></span></div>""" % (t, s) for t, s in FEATURES)

F2 = BASE % dict(FR=FONT_R, FS=FONT_S, FB=FONT_B, IS=88, IR=22) + """
<style>
.phone{position:absolute;bottom:0;width:392px;height:710px;overflow:hidden;
  border-radius:36px;background:#000;border:5px solid rgba(255,255,255,.18);
  box-shadow:0 40px 90px rgba(0,0,0,.65)}
.phone img{display:block;width:100%%;height:100%%;object-fit:cover;object-position:top}
.feat{display:flex;align-items:flex-start;gap:22px;text-align:right;width:100%%}
.tick{flex:none;width:52px;height:52px;border-radius:50%%;background:rgba(20,184,166,.18);
  border:1px solid rgba(45,212,191,.42);color:var(--teal-lite);font-size:27px;font-weight:700;
  display:flex;align-items:center;justify-content:center}
.ftext{display:flex;flex-direction:column;gap:5px;padding-top:1px}
.ftext b{font-size:39px;font-weight:700;line-height:1.3}
.ftext i{font-size:29px;font-style:normal;color:var(--fg-dim);font-weight:400;line-height:1.45}
</style>
<div class="bg"><div class="glow g1"></div><div class="glow g2"></div><div class="grain"></div></div>
<div class="stage" style="justify-content:space-between">

  <div>
    <div style="display:flex;align-items:center;gap:18px;justify-content:center">
      <div class="icon">%(ICON)s</div>
      <div class="brand" style="font-size:52px">نمک‌پاش</div>
    </div>
    <div style="font-size:52px;font-weight:700;line-height:1.5;margin-top:30px">
      همون‌جا سرِ میز، <span class="teal">دنگ کن</span>
    </div>
  </div>

  <div style="position:relative;width:100%%;height:730px">
    <div class="phone" style="left:0;bottom:48px;transform:rotate(-5deg);opacity:.9">
      <img src="%(SHOT_A)s">
    </div>
    <div class="phone" style="right:0;transform:rotate(4deg);z-index:2">
      <img src="%(SHOT_B)s">
    </div>
  </div>

  <div style="display:flex;flex-direction:column;gap:30px;width:100%%">%(ROWS)s</div>

</div>
""" % dict(ICON=ICON_SVG, SHOT_A=SHOT_DARK, SHOT_B=SHOT_BALANCES, ROWS=rows)

# --------------------------------------------- 3 · the fact that sells it
# The link is added as an Instagram sticker inside .stickerbox when posting, so this frame
# deliberately carries no url and no button of its own.
F3 = BASE % dict(FR=FONT_R, FS=FONT_S, FB=FONT_B, IS=156, IR=38) + """
<style>
.arrow{font-size:46px;line-height:1;color:var(--teal-lite);margin-top:12px}
.stickerbox{width:100%%;height:224px;border-radius:36px;
  border:2px solid rgba(255,255,255,.16);background:rgba(255,255,255,.035)}
</style>
<div class="bg"><div class="glow g1"></div><div class="glow g2"></div><div class="grain"></div></div>
<div class="stage">

  <div class="icon" style="margin-top:58px">%(ICON)s</div>
  <div class="brand" style="font-size:60px;margin-top:34px">نمک‌پاش</div>

  <div style="font-size:68px;font-weight:700;line-height:1.5;margin-top:64px">
    حتی <span class="teal">اجازهٔ اینترنت</span><br>هم نمی‌گیره
  </div>
  <div class="dim" style="font-size:39px;font-weight:600;line-height:1.65;margin-top:32px">
    هیچ داده‌ای از گوشیت بیرون نمی‌ره.<br>رایگان، بدون ثبت‌نام، بدون تبلیغ.
  </div>

  <div style="margin:auto 0"></div>

  <div style="display:flex;flex-direction:column;align-items:center;width:100%%">
    <div style="font-size:46px;font-weight:700">دانلود</div>
    <div class="arrow">↓</div>
    <div class="stickerbox" style="margin-top:28px"></div>
  </div>

</div>
""" % dict(ICON=ICON_SVG)

for name, html in (("story-1-hook", F1), ("story-2-features", F2), ("story-3-download", F3)):
    (OUT / (name + ".html")).write_text(html, encoding="utf-8")
print("wrote 3 html files to", OUT)
