"""Процедурные 9-slice спрайты интерфейса (палитра VisualTheme): карточка панели, кнопки, «таблетка», мягкая тень.

python Tools/art/draw_ui.py <project>  →  Assets/Art/UI/*.png (≤ 160 px) и Builds/preview/ui_sheet.png
Края для 9-slice задаёт UiFixSetup (spriteBorder).
"""
import os, sys
from PIL import Image, ImageDraw, ImageFilter
sys.path.insert(0, os.path.dirname(__file__))
from draw_mechanics import S, new, mask, gradient_fill, outline, rim_shade, gloss, clip, finish

CHAR = (42, 15, 9, 255)


def rounded(w, h, r, inset=0):
    return mask(w, h, lambda d, s: d.rounded_rectangle([inset * s, inset * s, (w - inset) * s - 1, (h - inset) * s - 1], radius=r * s, fill=255))


def card():
    w = h = 160
    img = new(w, h)
    m = rounded(w, h, 44, 3)
    img.alpha_composite(outline(m, 2.5, CHAR))
    img.alpha_composite(gradient_fill(m, (176, 102, 60), (128, 66, 38), (82, 40, 24), center=0.45))
    border = Image.new("L", m.size, 0)
    ImageDraw.Draw(border).rounded_rectangle([12 * S, 12 * S, (w - 12) * S, (h - 12) * S], radius=34 * S, outline=255, width=3 * S)
    layer = Image.new("RGBA", m.size, (242, 207, 128, 255)); layer.putalpha(border.point(lambda v: int(v * 0.55)))
    img.alpha_composite(layer)
    img.alpha_composite(rim_shade(m, (255, 255, 255, 255), 5, 0.18, up=True))
    img.alpha_composite(rim_shade(m, (0, 0, 0, 255), 6, 0.35))
    return finish(img, w, h)


def button(cols):
    w = h = 160
    img = new(w, h)
    m = rounded(w, h, 48, 4)
    img.alpha_composite(outline(m, 2.5, CHAR))
    img.alpha_composite(gradient_fill(m, *cols, center=0.5))
    img.alpha_composite(rim_shade(m, (0, 0, 0, 255), 7, 0.35))
    img.alpha_composite(clip(gloss(m.size, (22, 12, 138, 44), 0.42, 2.0), m))
    return finish(img, w, h)


def pill():
    w, h = 160, 96
    img = new(w, h)
    m = rounded(w, h, 44, 3)
    img.alpha_composite(outline(m, 2.0, (224, 106, 31, 255)))
    fill = Image.new("RGBA", m.size, (42, 15, 9, 230)); fill.putalpha(m.point(lambda v: int(v * 0.9)))
    img.alpha_composite(fill)
    img.alpha_composite(clip(gloss(m.size, (16, 8, 144, 30), 0.18, 2.0), m))
    return finish(img, w, h)


def shadow():
    w = h = 160
    img = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
    m = rounded(w, h, 48, 22).filter(ImageFilter.GaussianBlur(10 * S))
    layer = Image.new("RGBA", m.size, (0, 0, 0, 255)); layer.putalpha(m.point(lambda v: int(v * 0.75)))
    img.alpha_composite(layer)
    return finish(img, w, h)


def main():
    project = sys.argv[1]
    out = os.path.join(project, "Assets", "Art", "UI")
    os.makedirs(out, exist_ok=True)
    sprites = {
        "ui_card": card(),
        "ui_button": button(((255, 214, 120), (240, 140, 50), (186, 74, 26))),
        "ui_button_wood": button(((186, 116, 70), (138, 76, 44), (92, 46, 26))),
        "ui_button_light": button(((255, 255, 255), (236, 232, 226), (182, 176, 168))),  # тонируется Image.color
        "ui_pill": pill(),
        "ui_shadow": shadow(),
    }
    for n, im in sprites.items():
        im.save(os.path.join(out, n + ".png"), optimize=True)
    sheet = Image.new("RGBA", (sum(i.width for i in sprites.values()) + 20 * 6, 200), (230, 210, 180, 255))
    x = 20
    for im in sprites.values():
        sheet.alpha_composite(im, (x, 20)); x += im.width + 20
    os.makedirs(os.path.join(project, "Builds", "preview"), exist_ok=True)
    sheet.resize((sheet.width * 2, sheet.height * 2)).save(os.path.join(project, "Builds", "preview", "ui_sheet.png"))
    print("ok", {k: v.size for k, v in sprites.items()})


if __name__ == "__main__":
    main()
