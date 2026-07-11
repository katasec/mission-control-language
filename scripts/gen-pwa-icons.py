#!/usr/bin/env python3
"""Generate Forge PWA icons: white @-mark on the brand purple (#512bd4)."""
import os
from PIL import Image, ImageDraw, ImageFont

PURPLE = (0x51, 0x2b, 0xd4, 255)
WHITE  = (0xf6, 0xf6, 0xf6, 255)
OUT = "/Users/ameerdeen/progs/mission-control-language/src/ForgeUI/wwwroot/icons"
os.makedirs(OUT, exist_ok=True)

# Supersample for crisp edges, then downscale.
SS = 4

def load_font(px):
    for path, idx in [("/System/Library/Fonts/SFNS.ttf", 0),
                      ("/System/Library/Fonts/HelveticaNeue.ttc", 0),
                      ("/System/Library/Fonts/Helvetica.ttc", 1)]:
        try:
            return ImageFont.truetype(path, px, index=idx)
        except Exception:
            continue
    return ImageFont.load_default()

def draw_at(img_size, glyph_frac):
    """Return an RGBA image of just the white @ centered, glyph height = frac*size."""
    s = img_size * SS
    layer = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    fs = int(s * glyph_frac)
    font = load_font(fs)
    # tune size to hit target cap-height via bbox measurement
    bbox = d.textbbox((0, 0), "@", font=font)
    gw, gh = bbox[2] - bbox[0], bbox[3] - bbox[1]
    x = (s - gw) / 2 - bbox[0]
    y = (s - gh) / 2 - bbox[1]
    d.text((x, y), "@", font=font, fill=WHITE)
    return layer

def disc_icon(size, glyph_frac=0.62):
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    margin = int(s * 0.02)
    d.ellipse([margin, margin, s - margin, s - margin], fill=PURPLE)
    img.alpha_composite(draw_at(size, glyph_frac))
    return img.resize((size, size), Image.LANCZOS)

def maskable_icon(size, glyph_frac=0.50):
    # Full-bleed purple square; @ within the central ~66% safe zone.
    s = size * SS
    img = Image.new("RGBA", (s, s), PURPLE)
    img.alpha_composite(draw_at(size, glyph_frac))
    return img.resize((size, size), Image.LANCZOS)

disc_icon(192).save(f"{OUT}/icon-192.png")
disc_icon(512).save(f"{OUT}/icon-512.png")
maskable_icon(512).save(f"{OUT}/icon-512-maskable.png")
# apple-touch-icon: iOS applies its own mask/rounding, wants a full opaque square.
maskable_icon(180, glyph_frac=0.58).save(f"{OUT}/apple-touch-icon.png")
print("wrote:", os.listdir(OUT))
