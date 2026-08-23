#!/usr/bin/env bash
# Publishes the Blazor PWA into docs/app/, which is what GitHub Pages serves at
# https://namakpash.namakco.ir/app/. Run from the repo root; commit the result.
set -euo pipefail

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

dotnet publish Splitt.Web -c Release -o "$STAGE"

rm -rf docs/app
cp -R "$STAGE/wwwroot/app" docs/app

# Pages neither negotiates brotli nor serves these sidecars, so they would be dead weight in git.
find docs/app -name '*.br' -delete
find docs/app -name '*.gz' -delete

# Jekyll skips every path beginning with an underscore, which would silently drop _framework/
# and leave a white screen with no error worth reading.
touch docs/.nojekyll

echo "published $(du -sh docs/app | cut -f1) into docs/app"
