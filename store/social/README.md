# Instagram story cards

Three 1080x1920 frames, posted back to back in this order. They tell one joke and then
answer it — frame 1 is the setup, frame 2 is the product, frame 3 is the reason to install.

| File | Role |
|------|------|
| `story-1-hook.png`      | The line everyone has heard at the end of a group outing |
| `story-2-features.png`  | Two real screenshots (dark + light) and three concrete claims |
| `story-3-download.png`  | The zero-permissions fact, then «دانلود ↓» over an empty box |

Rebuild after changing copy, colours, or screenshots:

```bash
scripts/build-story-cards.sh
```

The HTML sources sit next to the PNGs. Fonts, screenshots, and the icon are inlined as
data URIs, so a frame opens correctly from `file://` with no asset paths to resolve.

## Posting

1. Instagram > new story > pick `story-1-hook.png` from the gallery. Post it as is; the frames
   already carry all their text, so do not add text stickers on top.
2. Repeat for frame 2, then frame 3.
3. On frame 3 only, add the link sticker and point it at `https://namakpash.namakco.ir`.
   Drop it **inside the empty box** at the bottom, the one «دانلود ↓» points at. The box is
   drawn in the image on purpose — it frames the sticker so the composition still balances
   once the sticker sits there. The url is not printed anywhere on the frame; the sticker
   carries it.
4. Add the three frames to a Highlight so the story outlives 24 hours.

## Copy notes

**The register here is spoken Persian, not the written register the landing page uses.**
That is deliberate and it is the main thing to preserve. A story competes for attention with
the viewer's actual friends, so «یه نگاه کافیه» belongs here and «یک نگاه کافی است» does not.
The first attempt at these cards was written in landing-page voice — abstract noun phrases like
«سهم هرکس، دقیق و درست» and «هر لحظه معلوم است چه کسی طلبکار است» — and read as a brochure.

Specific choices worth keeping:

- **Frame 1 leads with «باشه، بعداً حساب می‌کنیم»**, the sentence every Iranian has said at the
  end of a shared meal, and pays it off with «هیچ‌وقت هم حساب نشد.» Recognition first, product
  second; the app name does not appear until the bottom of the frame.
- **«دنگ کن» is on frame 2 on purpose.** It is the warm verb the product owns, and it was
  missing from the first attempt entirely.
- **«چهار نفر، دو تا پرداخت» is verified by the screenshot beside it** — that balances shot has
  four participants and exactly two settlement suggestions. Re-check it if the screenshot is
  ever replaced, or the claim stops being true.
- **Frame 3 leads with «حتی اجازهٔ اینترنت هم نمی‌گیره»**, chosen by the user over an
  alternative that closed frame 1's joke with «دیگه «بعداً» نداریم». It is the app's most
  distinctive true fact (zero Android permissions in Release). Keep it; the callback version
  was tried and rejected.
- **Frame 3 carries no button, no url, and no instruction sentence.** They were removed on
  purpose: the link sticker is the call to action, so printing a second fake button next to it
  just competes with the real one. All that is left is «دانلود», a small arrow, and the box.

## Layout constraints

- Everything sits inside a 250px top and bottom margin, which is the area Instagram's own
  UI (profile row, reply bar) covers.
- The document is RTL. Any element that overflows the 1080px width shifts the whole page
  sideways in the render — that is what a black band down one edge means, not a stray margin.
  Keep the background glows inside the `.bg` wrapper, which clips them.
- Frame 3 balances itself with an empty `margin:auto 0` element between the text block and the
  download block. It is invisible on purpose — a visible divider was tried there and rejected.
  Do not give it a border or a background.
- Persian guillemets mirror in RTL, so «…» renders with both marks pointing outward. That is
  correct and matches the landing page; do not "fix" it by swapping the characters.
