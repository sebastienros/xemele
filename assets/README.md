# Branding

The original SVG sources are `logo.svg` and `banner.svg`. The angle-bracket mark
uses the purple/indigo/blue visual family of
[CurlyRpc](https://github.com/sebastienros/curlyrpc/tree/main/assets) and
[Apex](https://github.com/sebastienros/apex/tree/main/assets), with a cyan XML
slash instead of CurlyRpc's duplex arrows.

The banner follows CurlyRpc's two-line layout: a white `X` and light-indigo
`emele` wordmark above "XML & HTML parsing and validation tools for .NET".

With Python 3, `playwright-cli`, and its Chromium browser available:

```sh
python3 assets/render.py
```

This renders all PNGs at 2x pixel density through Playwright: `banner.png`
(2400 x 720 pixels for 1200 x 360 display), `logo.png` (1024 x 1024 pixels for
512 x 512 display), and `icon.png` (256 x 256 pixels for 128 x 128 display).
Rounded corners are transparent. The icon uses the same SVG as the logo. The
wordmark uses system fonts, so exact letter shapes can vary by platform;
committed PNGs were rendered with Chromium on macOS.
