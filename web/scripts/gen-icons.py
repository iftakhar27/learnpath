"""Draws the LearnPath app icons (brand indigo, white "L", amber accent dot) into public/icons and src/app.
Run: python3 scripts/gen-icons.py   (needs Pillow and the Inter font)"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
FONT = "/usr/share/fonts/opentype/inter/Inter-ExtraBold.otf"
INDIGO, WHITE, AMBER = (79, 70, 229), (255, 255, 255), (245, 158, 11)


def icon(size: int, safe: float = 1.0, radius: float = 0.0) -> Image.Image:
    """safe < 1 shrinks the mark into the centre (maskable icons are cropped to a circle by Android)."""
    scale = 4  # draw large, then downsample for smooth edges
    s = size * scale
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=int(s * radius), fill=INDIGO)
    font = ImageFont.truetype(FONT, int(s * 0.62 * safe))
    box = d.textbbox((0, 0), "L", font=font)
    w, h = box[2] - box[0], box[3] - box[1]
    dot = s * 0.11 * safe
    x = (s - (w + dot * 1.15)) / 2 - box[0]
    y = (s - h) / 2 - box[1]
    d.text((x, y), "L", font=font, fill=WHITE)
    cx, cy = x + box[0] + w + dot * 0.75, y + box[1] + h - dot / 2
    d.ellipse([cx - dot / 2, cy - dot / 2, cx + dot / 2, cy + dot / 2], fill=AMBER)
    return img.resize((size, size), Image.LANCZOS)


out = ROOT / "public" / "icons"
out.mkdir(parents=True, exist_ok=True)
icon(192).save(out / "icon-192.png")
icon(512).save(out / "icon-512.png")
icon(512, safe=0.72).save(out / "icon-maskable-512.png")
icon(180).convert("RGB").save(ROOT / "src" / "app" / "apple-icon.png")  # iOS adds its own rounded corners
icon(512, radius=0.22).save(ROOT / "src" / "app" / "icon.png")
icon(48, radius=0.22).save(ROOT / "src" / "app" / "favicon.ico", sizes=[(16, 16), (32, 32), (48, 48)])
print("icons written")
