#!/usr/bin/env bash
# Build the Instagram story cards (1080x1920) into store/social/.
# Generates the HTML with assets inlined, then screenshots each frame headlessly.
set -euo pipefail
cd "$(dirname "$0")/.."
python3 scripts/build-story-cards.py
CHROME="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
for n in story-1-hook story-2-features story-3-download; do
  "$CHROME" --headless=new --disable-gpu \
    --window-size=1080,1920 --hide-scrollbars --force-device-scale-factor=1 \
    --virtual-time-budget=5000 \
    --screenshot="store/social/$n.png" "file://$PWD/store/social/$n.html" 2>/dev/null
  echo "rendered store/social/$n.png"
done
