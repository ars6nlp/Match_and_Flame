"""Процедурная графика механик «капля» и «вентилятор» (рисуется с 4-кратным запасом и уменьшается).

python Tools/art/draw_mechanics.py <project>
Пишет PNG в Assets/Art/Mechanics/ (все до 256 px) и лист-превью Builds/preview/mechanics_sheet.png.
Стиль — как у спички: тонкая тёмно-коричневая обводка, светлый блик сверху, тень снизу, глянцевые точки.
"""
import os, sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops

S = 4  # суперсэмплинг
OUTLINE = (58, 26, 16, 255)        # тёмно-коричневая обводка, как у спички
COPPER = ((240, 168, 106), (200, 116, 58), (122, 58, 26))  # светлый, основной, тёмный
CREAM = ((255, 236, 186), (242, 207, 128), (176, 128, 64))
RED = ((232, 92, 72), (190, 52, 36), (120, 26, 18))
WATER = ((214, 240, 255), (120, 190, 236), (40, 104, 170))
WATER_OUT = (24, 60, 104, 255)
CHAR = (42, 15, 9)


def new(w, h):
    return Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))


def mask(w, h, fn):
    m = Image.new("L", (w * S, h * S), 0)
    fn(ImageDraw.Draw(m), S)
    return m


def gradient_fill(m, light, mid, dark, angle=90, center=0.4):
    """Вертикальный градиент light (верх) -> mid -> dark (низ) по маске."""
    W, H = m.size
    ys = np.linspace(0, 1, H)[:, None] * np.ones((1, W))
    if angle != 90:
        xs = np.linspace(0, 1, W)[None, :] * np.ones((H, 1))
        a = math.radians(angle)
        ys = (ys * math.sin(a) + xs * math.cos(a)) / (abs(math.sin(a)) + abs(math.cos(a)))
    # маска по вертикали в пределах самой фигуры
    arr = np.array(m) > 0
    rows = np.where(arr.any(axis=1))[0]
    if len(rows):
        y0, y1 = rows[0] / H, rows[-1] / H
        ys = np.clip((ys - y0) / max(1e-6, y1 - y0), 0, 1)
    out = np.zeros((H, W, 4), np.float32)
    l, md, d = (np.array(c, np.float32) for c in (light, mid, dark))
    t1 = np.clip(ys / center, 0, 1)[..., None]
    t2 = np.clip((ys - center) / (1 - center), 0, 1)[..., None]
    col = np.where(ys[..., None] < center, l + (md - l) * t1, md + (d - md) * t2)
    out[..., :3] = col
    out[..., 3] = np.array(m, np.float32)
    return Image.fromarray(out.clip(0, 255).astype(np.uint8), "RGBA")


def outline(m, px, color=OUTLINE):
    k = int(round(px * S)) * 2 + 1
    grown = m.filter(ImageFilter.MaxFilter(k))
    layer = Image.new("RGBA", m.size, color)
    layer.putalpha(grown)
    return layer


def rim_shade(m, color, px, alpha, up=False):
    """Тёмная (или светлая) полоса у нижнего (верхнего) края фигуры внутри маски."""
    shift = int(px * S) * (1 if up else -1)
    moved = ImageChops.offset(m, 0, shift)
    band = ImageChops.subtract(m, moved).filter(ImageFilter.GaussianBlur(S * 1.2))
    band = ImageChops.multiply(band, m)
    layer = Image.new("RGBA", m.size, color)
    layer.putalpha(band.point(lambda v: int(v * alpha)))
    return layer


def gloss(size, box, alpha=0.75, blur=1.0):
    g = Image.new("L", size, 0)
    ImageDraw.Draw(g).ellipse([v * S for v in box[:4]], fill=int(255 * alpha))
    if blur:
        g = g.filter(ImageFilter.GaussianBlur(S * blur))
    layer = Image.new("RGBA", size, (255, 255, 255, 255))
    layer.putalpha(g)
    return layer


def clip(layer, m):
    a = ImageChops.multiply(layer.split()[3], m)
    layer = layer.copy()
    layer.putalpha(a)
    return layer


def part(img, m, cols, outline_px=1.5, gloss_boxes=(), shade=True, angle=90, center=0.4):
    """Фигура: обводка, градиент, тень снизу, блики."""
    img.alpha_composite(outline(m, outline_px))
    img.alpha_composite(gradient_fill(m, *cols, angle=angle, center=center))
    if shade:
        img.alpha_composite(rim_shade(m, (0, 0, 0, 255), 4, 0.35))
        img.alpha_composite(rim_shade(m, (255, 255, 255, 255), 3, 0.30, up=True))
    for b in gloss_boxes:
        img.alpha_composite(clip(gloss(m.size, b[:4], b[4] if len(b) > 4 else 0.7), m))


def finish(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


# ---------------------------------------------------------------- Капля

def drop():
    w, h = 64, 64
    img = new(w, h)
    def shape(d, s):
        d.ellipse([14 * s, 22 * s, 50 * s, 58 * s], fill=255)
        d.polygon([(16 * s, 36 * s), (32 * s, 4 * s), (48 * s, 36 * s)], fill=255)
    m = mask(w, h, shape).filter(ImageFilter.GaussianBlur(S * 0.6)).point(lambda v: 255 if v > 128 else 0)
    img.alpha_composite(outline(m, 1.6, WATER_OUT))
    img.alpha_composite(gradient_fill(m, *WATER, center=0.35))
    img.alpha_composite(rim_shade(m, (10, 40, 90, 255), 5, 0.35))
    img.alpha_composite(clip(gloss(m.size, (20, 28, 30, 44), 0.9, 0.8), m))
    img.alpha_composite(clip(gloss(m.size, (36, 46, 42, 52), 0.6, 0.6), m))
    return finish(img, w, h)


# ---------------------------------------------------------------- Кран (медь, красный вентиль), точка выхода — низ по центру

def faucet():
    w, h = 192, 176
    img = new(w, h)
    # труба из стены слева
    pipe = mask(w, h, lambda d, s: d.rounded_rectangle([0, 40 * s, 70 * s, 66 * s], radius=6 * s, fill=255))
    part(img, pipe, COPPER, gloss_boxes=[(6, 43, 60, 49, 0.45)])
    flange = mask(w, h, lambda d, s: d.rounded_rectangle([4 * s, 34 * s, 16 * s, 72 * s], radius=4 * s, fill=255))
    part(img, flange, COPPER)
    # корпус
    body = mask(w, h, lambda d, s: d.rounded_rectangle([52 * s, 30 * s, 140 * s, 88 * s], radius=18 * s, fill=255))
    part(img, body, COPPER, gloss_boxes=[(62, 36, 104, 48, 0.55), (118, 40, 128, 50, 0.5)])
    # шток и вентиль
    stem = mask(w, h, lambda d, s: d.rectangle([88 * s, 12 * s, 104 * s, 32 * s], fill=255))
    part(img, stem, COPPER, shade=False)
    handle = mask(w, h, lambda d, s: d.rounded_rectangle([60 * s, 4 * s, 132 * s, 18 * s], radius=7 * s, fill=255))
    part(img, handle, RED, gloss_boxes=[(66, 6, 96, 10, 0.6)])
    cap = mask(w, h, lambda d, s: d.ellipse([86 * s, 2 * s, 106 * s, 20 * s], fill=255))
    part(img, cap, RED, gloss_boxes=[(90, 4, 98, 9, 0.7)])
    # носик вниз
    spout = mask(w, h, lambda d, s: d.polygon([(78 * s, 86 * s), (114 * s, 86 * s), (108 * s, 150 * s), (84 * s, 150 * s)], fill=255))
    part(img, spout, COPPER, angle=0, center=0.35, gloss_boxes=[(86, 92, 92, 140, 0.5)])
    ring = mask(w, h, lambda d, s: d.rounded_rectangle([78 * s, 144 * s, 114 * s, 162 * s], radius=5 * s, fill=255))
    part(img, ring, COPPER, gloss_boxes=[(82, 146, 104, 150, 0.5)])
    hole = mask(w, h, lambda d, s: d.ellipse([88 * s, 156 * s, 104 * s, 164 * s], fill=255))
    dark = Image.new("RGBA", hole.size, CHAR + (255,)); dark.putalpha(hole)
    img.alpha_composite(dark)
    return finish(img, w, h)


# ---------------------------------------------------------------- Зонтик-укрытие

def umbrella():
    w, h = 192, 112
    img = new(w, h)
    def canopy(d, s):
        d.pieslice([8 * s, 10 * s, 184 * s, 150 * s], 180, 360, fill=255)
    m = mask(w, h, canopy)
    # фестоны снизу: вырезаем полукруги
    cut = Image.new("L", m.size, 0)
    cd = ImageDraw.Draw(cut)
    for i in range(4):
        cx = 30 + i * 44
        cd.ellipse([(cx - 22) * S, 72 * S, (cx + 22) * S, 96 * S], fill=255)
    m = ImageChops.subtract(m, cut)
    # полосы: кремовая/красная
    stripes = Image.new("RGBA", m.size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(stripes)
    for i in range(4):
        x0 = 8 + i * 44
        cols = CREAM if i % 2 == 0 else RED
        seg = Image.new("L", m.size, 0)
        # клин от верхушки, продлённый за край купола (иначе крайние доли остаются пустыми)
        k = 3.0
        lx = 96 + (x0 - 96) * k if i > 0 else -600
        rx = 96 + (x0 + 44 - 96) * k if i < 3 else 800
        pts = [(96 * S, 6 * S)]
        if i == 0: pts.append((-600 * S, -100 * S))
        pts += [(lx * S, (6 + 78 * k) * S), (rx * S, (6 + 78 * k) * S)]
        if i == 3: pts.append((800 * S, -100 * S))
        ImageDraw.Draw(seg).polygon(pts, fill=255)
        seg = ImageChops.multiply(seg, m)
        stripes.alpha_composite(gradient_fill(seg, *cols, center=0.45))
    img.alpha_composite(outline(m, 1.6))
    img.alpha_composite(stripes)
    img.alpha_composite(rim_shade(m, (0, 0, 0, 255), 6, 0.3))
    img.alpha_composite(clip(gloss(m.size, (40, 22, 100, 36), 0.45, 2), m))
    # спицы
    ribs = Image.new("RGBA", m.size, (0, 0, 0, 0))
    rd = ImageDraw.Draw(ribs)
    for i in range(5):
        rd.line([(96 * S, 10 * S), ((96 + (8 + i * 44 - 96) * 3.0) * S, (6 + 78 * 3.0) * S)], fill=(90, 40, 20, 140), width=S * 2)
    img.alpha_composite(clip(ribs, m))
    knob = mask(w, h, lambda d, s: d.ellipse([88 * s, 0, 104 * s, 14 * s], fill=255))
    part(img, knob, COPPER, gloss_boxes=[(92, 2, 98, 6, 0.7)])
    handle = mask(w, h, lambda d, s: d.rounded_rectangle([92 * s, 70 * s, 100 * s, 106 * s], radius=4 * s, fill=255))
    part(img, handle, ((214, 160, 110), (170, 110, 64), (110, 66, 34)), shade=False)
    return finish(img, w, h)


# ---------------------------------------------------------------- Вентилятор: корпус (кольцо + раструб вправо), лопасти отдельно

def fan_body():
    w, h = 224, 176
    img = new(w, h)
    cx, cy, r = 84, 84, 70
    # раструб
    duct = mask(w, h, lambda d, s: d.polygon([(118 * s, 48 * s), (208 * s, 30 * s), (218 * s, 34 * s), (218 * s, 134 * s), (208 * s, 138 * s), (118 * s, 120 * s)], fill=255))
    part(img, duct, COPPER, gloss_boxes=[(130, 44, 200, 54, 0.4)])
    vents = Image.new("RGBA", duct.size, (0, 0, 0, 0))
    vd = ImageDraw.Draw(vents)
    for i in range(4):
        y = 58 + i * 17
        vd.rounded_rectangle([150 * S, y * S, 206 * S, (y + 6) * S], radius=3 * S, fill=CHAR + (200,))
    img.alpha_composite(clip(vents, duct))
    lip = mask(w, h, lambda d, s: d.rounded_rectangle([204 * s, 26 * s, 222 * s, 142 * s], radius=6 * s, fill=255))
    part(img, lip, COPPER, gloss_boxes=[(208, 30, 214, 90, 0.4)])
    # подставка
    stand = mask(w, h, lambda d, s: d.polygon([(70 * s, 140 * s), (98 * s, 140 * s), (110 * s, 166 * s), (58 * s, 166 * s)], fill=255))
    part(img, stand, ((150, 92, 56), (110, 62, 36), (70, 36, 20)))
    foot = mask(w, h, lambda d, s: d.rounded_rectangle([40 * s, 160 * s, 128 * s, 174 * s], radius=6 * s, fill=255))
    part(img, foot, ((150, 92, 56), (110, 62, 36), (70, 36, 20)), gloss_boxes=[(48, 161, 110, 164, 0.35)])
    # корпус-кольцо
    ring = mask(w, h, lambda d, s: d.ellipse([(cx - r) * s, (cy - r) * s, (cx + r) * s, (cy + r) * s], fill=255))
    part(img, ring, COPPER, gloss_boxes=[(cx - 52, cy - 64, cx + 10, cy - 50, 0.55)])
    inner = mask(w, h, lambda d, s: d.ellipse([(cx - r + 12) * s, (cy - r + 12) * s, (cx + r - 12) * s, (cy + r - 12) * s], fill=255))
    hole = gradient_fill(inner, (70, 32, 18), (48, 20, 12), (30, 12, 8), center=0.5)
    img.alpha_composite(outline(inner, 1.2))
    img.alpha_composite(hole)
    # решётка поверх отверстия (тонкие спицы — лопасти видны между ними)
    grill = Image.new("RGBA", inner.size, (0, 0, 0, 0))
    gd = ImageDraw.Draw(grill)
    for a in range(0, 180, 30):
        dx, dy = math.cos(math.radians(a)) * (r - 12), math.sin(math.radians(a)) * (r - 12)
        gd.line([((cx - dx) * S, (cy - dy) * S), ((cx + dx) * S, (cy + dy) * S)], fill=(200, 140, 90, 70), width=S)
    img.alpha_composite(clip(grill, inner))
    return finish(img, w, h)


def fan_blades():
    w, h = 128, 128
    img = new(w, h)
    c = 64
    for k in range(4):
        a = math.radians(k * 90 + 20)
        blade = Image.new("L", (w * S, h * S), 0)
        bd = ImageDraw.Draw(blade)
        # лепесток: эллипс, вытянутый от центра
        ex, ey = c + math.cos(a) * 30, c + math.sin(a) * 30
        pts = []
        for t in range(0, 360, 10):
            tt = math.radians(t)
            lx, ly = math.cos(tt) * 28, math.sin(tt) * 13
            rx = ex + lx * math.cos(a) - ly * math.sin(a)
            ry = ey + lx * math.sin(a) + ly * math.cos(a)
            pts.append((rx * S, ry * S))
        bd.polygon(pts, fill=255)
        part(img, blade, CREAM, outline_px=1.4, gloss_boxes=[(ex - 10, ey - 8, ex + 2, ey - 2, 0.5)], angle=int(math.degrees(a)) % 180 or 1)
    hub = mask(w, h, lambda d, s: d.ellipse([(c - 14) * s, (c - 14) * s, (c + 14) * s, (c + 14) * s], fill=255))
    part(img, hub, COPPER, gloss_boxes=[(c - 9, c - 10, c, c - 4, 0.7)])
    return finish(img, w, h)


def lamp():
    w, h = 32, 32
    img = new(w, h)
    m = mask(w, h, lambda d, s: d.ellipse([3 * s, 3 * s, 29 * s, 29 * s], fill=255))
    part(img, m, ((255, 255, 255), (225, 225, 225), (150, 150, 150)), gloss_boxes=[(8, 7, 16, 13, 0.9)])
    return finish(img, w, h)


# ---------------------------------------------------------------- Иконки подсказок

def badge(w=96, h=96):
    img = new(w, h)
    m = mask(w, h, lambda d, s: d.ellipse([4 * s, 4 * s, 92 * s, 92 * s], fill=255))
    part(img, m, CREAM, outline_px=2, gloss_boxes=[(22, 12, 60, 26, 0.5)])
    return img


def icon_drop():
    img = badge()
    def shape(d, s):
        d.ellipse([30 * s, 40 * s, 66 * s, 76 * s], fill=255)
        d.polygon([(32 * s, 54 * s), (48 * s, 16 * s), (64 * s, 54 * s)], fill=255)
    m = mask(96, 96, shape)
    img.alpha_composite(outline(m, 1.6, WATER_OUT))
    img.alpha_composite(gradient_fill(m, *WATER, center=0.35))
    img.alpha_composite(clip(gloss(m.size, (37, 46, 46, 60), 0.9, 0.8), m))
    return finish(img, 96, 96)


def icon_fan():
    img = badge()
    c = 40
    for k in range(3):
        a = math.radians(k * 120 + 30)
        blade = Image.new("L", (96 * S, 96 * S), 0)
        ex, ey = c + math.cos(a) * 14, 48 + math.sin(a) * 14
        pts = []
        for t in range(0, 360, 12):
            tt = math.radians(t)
            lx, ly = math.cos(tt) * 14, math.sin(tt) * 7
            pts.append(((ex + lx * math.cos(a) - ly * math.sin(a)) * S, (ey + lx * math.sin(a) + ly * math.cos(a)) * S))
        ImageDraw.Draw(blade).polygon(pts, fill=255)
        part(img, blade, COPPER, outline_px=1.4, shade=False)
    lines = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(lines)
    for i, y in enumerate((34, 48, 62)):
        ld.line([(60 * S, y * S), ((80 - (i % 2) * 6) * S, y * S)], fill=(60, 120, 190, 255), width=int(S * 4))
    img.alpha_composite(lines)
    return finish(img, 96, 96)


def main():
    project = sys.argv[1]
    out = os.path.join(project, "Assets", "Art", "Mechanics")
    os.makedirs(out, exist_ok=True)
    sprites = {
        "water_drop": drop(), "faucet": faucet(), "umbrella": umbrella(),
        "fan_body": fan_body(), "fan_blades": fan_blades(), "fan_lamp": lamp(),
        "icon_water": icon_drop(), "icon_fan": icon_fan(),
    }
    for name, im in sprites.items():
        assert im.width <= 256 and im.height <= 256, name
        im.save(os.path.join(out, name + ".png"), optimize=True)
    # Лист-превью на фоне доски, ×2
    pad = 16
    sheet_w = sum(im.width for im in sprites.values()) + pad * (len(sprites) + 1)
    sheet_h = max(im.height for im in sprites.values()) + pad * 2
    sheet = Image.new("RGBA", (sheet_w, sheet_h), (95, 48, 34, 255))
    x = pad
    for im in sprites.values():
        sheet.alpha_composite(im, (x, pad))
        x += im.width + pad
    os.makedirs(os.path.join(project, "Builds", "preview"), exist_ok=True)
    sheet.resize((sheet_w * 2, sheet_h * 2), Image.LANCZOS).save(os.path.join(project, "Builds", "preview", "mechanics_sheet.png"))
    print("ok", {k: v.size for k, v in sprites.items()})


if __name__ == "__main__":
    main()
