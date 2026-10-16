"""Render the SVG sources at 2x pixel density with Chromium through playwright-cli."""

import json
from pathlib import Path
import subprocess
import tempfile


def run(*args):
    result = subprocess.run(
        ["playwright-cli", "-s=xemele-branding", *args],
        cwd=work,
        check=True,
        capture_output=True,
        text=True,
    )
    if "### Error" in result.stdout:
        raise RuntimeError(result.stdout)


pixel_density = 2
root = Path(__file__).resolve().parent
with tempfile.TemporaryDirectory(prefix="xemele-branding-") as work:
    run("open", "--browser", "chromium")
    try:
        for source, output, size in [
            ("banner.svg", "banner.png", (1200, 360)),
            ("logo.svg", "logo.png", (512, 512)),
            ("logo.svg", "icon.png", (128, 128)),
        ]:
            svg = (root / source).read_text(encoding="utf-8")
            width, height = size
            code = (
                "async sourcePage => {"
                "const context = await sourcePage.context().browser().newContext({"
                f"viewport:{{width:{width},height:{height}}},deviceScaleFactor:{pixel_density}}});"
                "try {"
                "const page = await context.newPage();"
                f"await page.setContent({json.dumps(svg)});"
                "await page.addStyleTag({content:'body{margin:0}svg{display:block;width:100%;height:100%}'});"
                "await page.evaluate(() => document.fonts.ready);"
                f"await page.locator('svg').screenshot({{path:{json.dumps(str(root / output))},"
                "omitBackground:true,scale:'device'});"
                "} finally {await context.close();}}"
            )
            run("run-code", code)
            print(
                f"Rendered {output}: {width * pixel_density} x {height * pixel_density}"
                f" ({pixel_density}x pixel density)"
            )
    finally:
        run("close")
