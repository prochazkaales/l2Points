# Upscales the original 32 px jewel icons (scripts/jewel-icons, from l2j.ru) to 128 px for wwwroot.
# The game only ships 32 px icons; Lanczos with a light unsharp mask keeps them smooth instead of blocky
# and gives 2x displays enough pixels for the 64 px gallery icon.
# Usage (needs Pillow): python scripts/upscale-jewel-icons.py scripts/jewel-icons src/L2Points.Web/wwwroot/images/jewels
import sys
from pathlib import Path
from PIL import Image, ImageFilter

SIZE = 128

src, out = Path(sys.argv[1]), Path(sys.argv[2])
for f in sorted(src.glob("*.png")):
    icon = Image.open(f).convert("RGBA").resize((SIZE, SIZE), Image.LANCZOS)
    icon = icon.filter(ImageFilter.UnsharpMask(radius=2, percent=60, threshold=2))
    icon.save(out / f.name, optimize=True)
    print(out / f.name)
