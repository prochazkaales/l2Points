# Renders the light variant of src/L2Points.Web/wwwroot/favicon.svg into PNG/ICO fallbacks (same 64-unit geometry; keep in sync).
# Usage (needs Pillow): python scripts/generate-favicons.py src/L2Points.Web/wwwroot
import sys
from PIL import Image, ImageDraw

out = sys.argv[1]
S = 16  # supersampling: 64 units -> 1024 px


def render(size, square=False):
    big = 64 * S
    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    p = lambda x, y: (x * S, y * S)
    if square:
        d.rectangle([p(0, 0), p(64, 64)], fill="#6d4ae0")
    else:
        d.rounded_rectangle([p(1, 1), p(63, 63)], radius=14 * S, fill="#6d4ae0")
    d.polygon([p(13, 22.5), p(24, 10.5), p(40, 10.5), p(51, 22.5)], fill="#ffd27a")
    d.polygon([p(13, 22.5), p(51, 22.5), p(32, 53.5)], fill="#f5a524")
    line = (0x6d, 0x4a, 0xe0, 90)
    overlay = Image.new("RGBA", img.size, (0, 0, 0, 0))
    o = ImageDraw.Draw(overlay)
    w = int(1.6 * S)
    o.line([p(24, 10.5), p(26.7, 22.5), p(32, 53.5), p(37.3, 22.5), p(40, 10.5)], fill=line, width=w, joint="curve")
    o.line([p(26.7, 22.5), p(37.3, 22.5)], fill=line, width=w)
    img = Image.alpha_composite(img, overlay)
    return img.resize((size, size), Image.LANCZOS)


render(32).save(f"{out}/favicon-32.png")
# Apple touch icon: iOS rounds corners itself and dislikes transparency, so fill the full square.
render(180, square=True).convert("RGB").save(f"{out}/apple-touch-icon.png")
render(256).save(f"{out}/favicon.ico", sizes=[(16, 16), (32, 32), (48, 48)])
