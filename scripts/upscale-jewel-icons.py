# Upscales the original 32 px jewel icons (scripts/jewel-icons, from l2j.ru) 4x to 128 px for wwwroot.
# The game only ships 32 px icons; the Real-ESRGAN x4plus model sharpens edges and restores texture,
# so the 64 px gallery icon stays crisp on 2x displays. Not run in CI: rerun only when an icon changes.
# Needs realesrgan-ncnn-vulkan (https://github.com/xinntao/Real-ESRGAN/releases, v0.2.5.0) and Pillow.
# Usage: python scripts/upscale-jewel-icons.py <path to realesrgan-ncnn-vulkan.exe> scripts/jewel-icons src/L2Points.Web/wwwroot/images/jewels
import subprocess
import sys
import tempfile
from pathlib import Path
from PIL import Image

exe, src, out = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3])

with tempfile.TemporaryDirectory() as tmp:
    subprocess.run([str(exe), "-i", str(src), "-o", tmp, "-n", "realesrgan-x4plus", "-f", "png"], check=True)
    for f in sorted(src.glob("*.png")):
        Image.open(Path(tmp) / f.name).convert("RGBA").save(out / f.name, optimize=True)
        print(out / f.name)
