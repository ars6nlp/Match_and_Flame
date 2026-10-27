"""Процедурная графика «Очага», часть 3: дизайн-система интерфейса и новый огонёк.

hearth_ui.png    — карточки, кнопки (амбер/крем/мята), полоса прогресса, плашки, бейдж, пузырь, ценник, тень, значки.
hearth_flame2.png — огонёк из слоёв (внешний мягкий край, середина, ядро, язычки, блик), лицо (белок, зрачок, веко, бровь,
                    румянец, 4 рта), тень и отражение на полу, свечение, аксессуары стадий (поленья, луч маяка, лучи солнца).
Всё рисуется примитивами с суперсэмплингом ×4 и уменьшается; слои огонька белые с градиентом яркости — цвет задаёт тонировка.
Запуск: python Tools/retention_art/draw_ui.py <проект>. Дописывает нарезку (и 9-slice border) в cells.json.
"""
import sys, os, json, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_atlas as A
from draw_atlas import Cell, INK, WOOD, COPPER, GOLD, spline
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

OUT = A.OUT
SS = A.SS

# Палитра (совпадает с HearthVisualSettings по умолчанию)
CREAM = (255, 248, 234)
CREAM2 = (247, 231, 203)
COPPER_LINE = (184, 108, 60)
INK_SOFT = (110, 64, 38)


def blur(img, r):
    return img.filter(ImageFilter.GaussianBlur(r))


def mask_poly(w, h, pts, blur_px=0):
    m = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(m).polygon([(x * w * SS, y * h * SS) for x, y in pts], fill=255)
    return blur(m, blur_px * SS) if blur_px else m


def from_mask(mask, color=(255, 255, 255), lum=None):
    """RGBA из маски альфы; lum — массив яркости 0..1 той же формы (градиент для тонировки)."""
    a = np.array(mask).astype(np.float32) / 255.0
    H, W = a.shape
    img = np.zeros((H, W, 4), np.float32)
    for k in range(3):
        img[..., k] = color[k] * (lum if lum is not None else 1.0)
    img[..., 3] = a * 255
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGBA")


def down(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


# ------------------------------------------------------------------ карточки и кнопки (9-slice)

def card():
    c = Cell(112, 112)
    c.rect(0.015, 0.015, 0.985, 0.985, (204, 128, 74), (168, 96, 54), r=0.31, w=0.0, outline=None)
    c.rect(0.06, 0.06, 0.94, 0.94, CREAM, CREAM2, r=0.27, w=0.0, outline=None)
    line = Image.new("RGBA", c.img.size, (0, 0, 0, 0))
    ImageDraw.Draw(line).rounded_rectangle(c.box(0.085, 0.085, 0.915, 0.915), radius=0.245 * 112 * SS, outline=(255, 255, 255, 150), width=SS * 2)
    c.img.alpha_composite(line)
    return c.result()


def inner():
    c = Cell(80, 80)
    c.rect(0.02, 0.02, 0.98, 0.98, (226, 196, 158), (214, 182, 142), r=0.3, w=0.0, outline=None)
    c.rect(0.05, 0.05, 0.95, 0.95, (255, 253, 247), (252, 245, 232), r=0.27, w=0.0, outline=None)
    return c.result()


def button(face_top, face_bottom, lip, outline):
    c = Cell(112, 112)
    c.rect(0.02, 0.06, 0.98, 0.98, lip, lip, r=0.42, w=0.0, outline=None)                     # нижняя кромка (объём)
    c.rect(0.02, 0.02, 0.98, 0.92, face_top, face_bottom, r=0.42, w=0.022, outline=outline + (255,))
    hl = Image.new("RGBA", c.img.size, (0, 0, 0, 0))
    ImageDraw.Draw(hl).rounded_rectangle(c.box(0.1, 0.08, 0.9, 0.42), radius=0.2 * 112 * SS, fill=(255, 255, 255, 80))
    c.img.alpha_composite(blur(hl, SS * 1.5))
    return c.result()


def pill():
    c = Cell(64, 64)
    c.rect(0.02, 0.02, 0.98, 0.98, (255, 255, 255), (255, 255, 255), r=0.48, w=0.0, outline=None)
    return c.result()


def tag():
    c = Cell(96, 64)
    c.rect(0.015, 0.02, 0.985, 0.98, (198, 120, 66), (170, 98, 56), r=0.32, w=0.0, outline=None)
    c.rect(0.045, 0.07, 0.955, 0.93, (255, 251, 240), (248, 236, 212), r=0.28, w=0.0, outline=None)
    return c.result()


def tag_tail():
    c = Cell(28, 18)
    c.poly([(0.04, 0.0), (0.96, 0.0), (0.5, 0.95)], (198, 120, 66), (170, 98, 56), w=0.0, outline=None)
    c.poly([(0.2, 0.0), (0.8, 0.0), (0.5, 0.62)], (250, 240, 218), (248, 236, 212), w=0.0, outline=None)
    return c.result()


def track():
    c = Cell(48, 48)
    c.rect(0.02, 0.02, 0.98, 0.98, (206, 176, 136), (214, 186, 148), r=0.48, w=0.0, outline=None)
    c.rect(0.08, 0.08, 0.92, 0.92, (228, 206, 174), (244, 230, 206), r=0.42, w=0.0, outline=None)
    return c.result()


def fill():
    c = Cell(48, 48)
    c.rect(0.02, 0.02, 0.98, 0.98, (255, 210, 104), (240, 140, 40), r=0.48, w=0.0, outline=None)
    hl = Image.new("RGBA", c.img.size, (0, 0, 0, 0))
    ImageDraw.Draw(hl).rounded_rectangle(c.box(0.14, 0.12, 0.86, 0.42), radius=0.14 * 48 * SS, fill=(255, 255, 255, 120))
    c.img.alpha_composite(blur(hl, SS))
    return c.result()


def badge():
    c = Cell(40, 40)
    c.glow(0.5, 0.5, 0.5, (255, 170, 90), 120)
    c.dot(0.5, 0.5, 0.36, (255, 255, 255))
    c.dot(0.5, 0.5, 0.27, (255, 128, 64))
    c.highlight(0.36, 0.3, 0.56, 0.46, 110)
    return c.result()


def shadow():
    w = h = 112
    m = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(m).rounded_rectangle((14 * SS, 14 * SS, (w - 14) * SS, (h - 14) * SS), radius=30 * SS, fill=150)
    m = blur(m, 9 * SS)
    return down(from_mask(m, (40, 16, 6)), w, h)


def soft():
    yy, xx = np.mgrid[0:64 * SS, 0:64 * SS].astype(np.float32)
    d = np.sqrt((xx - 32 * SS) ** 2 + (yy - 32 * SS) ** 2) / (32 * SS)
    a = np.clip(1 - d, 0, 1) ** 1.8
    return down(from_mask(Image.fromarray((a * 255).astype(np.uint8))), 64, 64)


def streak():
    w, h = 64, 64
    m = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(m).polygon([(22 * SS, 0), (40 * SS, 0), (26 * SS, h * SS), (8 * SS, h * SS)], fill=210)
    return down(from_mask(blur(m, 5 * SS)), w, h)


def circle():
    c = Cell(96, 96)
    c.dot(0.5, 0.5, 0.47, (255, 255, 255))
    return c.result()


def ring():
    c = Cell(96, 96)
    c.d.ellipse(c.box(0.05, 0.05, 0.95, 0.95), outline=(255, 255, 255, 255), width=int(0.075 * 96 * SS))
    return c.result()


def divider():
    w, h = 192, 8
    a = np.zeros((h * SS, w * SS), np.float32)
    xs = np.linspace(-1, 1, w * SS)
    a[2 * SS:6 * SS, :] = (1 - np.abs(xs) ** 2.2)[None, :]
    return down(from_mask(Image.fromarray((a * 255).astype(np.uint8)), (190, 140, 96)), w, h)


def bubble():
    c = Cell(112, 112)
    c.rect(0.02, 0.02, 0.98, 0.98, (190, 118, 70), (176, 106, 62), r=0.31, w=0.0, outline=None)
    c.rect(0.05, 0.05, 0.95, 0.95, (255, 252, 244), (251, 242, 224), r=0.28, w=0.0, outline=None)
    return c.result()


def bubble_tail():
    c = Cell(40, 28)
    c.poly([(0.0, 0.0), (1.0, 0.0), (0.42, 0.98)], (190, 118, 70), (176, 106, 62), w=0.0, outline=None)
    c.poly([(0.1, 0.0), (0.9, 0.0), (0.43, 0.8)], (251, 242, 224), (251, 242, 224), w=0.0, outline=None)
    return c.result()


# ------------------------------------------------------------------ значки 64×64

def ic_coin():
    c = Cell(64, 64)
    c.ell(0.07, 0.07, 0.93, 0.93, (255, 224, 120), (214, 150, 40), w=0.05, outline=(150, 96, 24, 255))
    c.ell(0.2, 0.2, 0.8, 0.8, (255, 210, 96), (236, 172, 56), w=0.03, outline=(176, 114, 32, 255))
    A.flame_shape(c, 0.5, 0.58, 0.2, (255, 246, 196), (255, 150, 50), 0.025)
    c.highlight(0.2, 0.14, 0.5, 0.34, 120)
    return c.result()


def ic_check():
    c = Cell(64, 64)
    c.ell(0.06, 0.06, 0.94, 0.94, (140, 226, 170), (74, 182, 116), w=0.05, outline=(40, 116, 72, 255))
    c.line([(0.27, 0.52), (0.44, 0.69), (0.75, 0.33)], (255, 255, 255), 0.12)
    return c.result()


def ic_info():
    c = Cell(64, 64)
    c.ell(0.06, 0.06, 0.94, 0.94, (255, 238, 206), (244, 214, 168), w=0.06, outline=(150, 82, 40, 255))
    c.dot(0.5, 0.3, 0.075, (138, 66, 30))
    c.rect(0.425, 0.43, 0.575, 0.77, (138, 66, 30), (138, 66, 30), r=0.06, w=0.0, outline=None)
    return c.result()


def ic_swap():
    c = Cell(64, 64)
    col = (150, 82, 40)
    for k in (0, 1):
        a0 = math.pi * (0.15 + k)
        pts = [(0.5 + 0.3 * math.cos(a0 + t * math.pi * 0.7), 0.5 + 0.3 * math.sin(a0 + t * math.pi * 0.7)) for t in np.linspace(0, 1, 12)]
        c.line(pts, col, 0.1)
        ex, ey = pts[-1]
        ang = a0 + math.pi * 0.7 + math.pi / 2
        tip = (ex + 0.13 * math.cos(ang), ey + 0.13 * math.sin(ang))
        side = (ang + math.pi / 2)
        c.poly([tip, (ex + 0.1 * math.cos(side), ey + 0.1 * math.sin(side)), (ex - 0.1 * math.cos(side), ey - 0.1 * math.sin(side))], col, col, w=0.0, outline=None)
    return c.result()


def ic_hand():
    c = Cell(96, 112)
    skin, skin2 = (255, 238, 218), (246, 214, 186)
    c.rect(0.4, 0.04, 0.6, 0.6, skin, skin2, r=0.1, w=0.03)                      # указательный
    for k, x in enumerate((0.6, 0.76)):
        c.rect(x - 0.005, 0.34 + k * 0.04, x + 0.16, 0.66, skin, skin2, r=0.08, w=0.03)
    c.rect(0.24, 0.44, 0.9, 0.92, skin, skin2, r=0.2, w=0.03)                     # ладонь
    c.ell(0.1, 0.5, 0.4, 0.72, skin, skin2, w=0.03)                               # большой палец
    c.rect(0.3, 0.48, 0.84, 0.7, skin, skin2, r=0.1, w=0.0, outline=None)
    return c.result()


def ic_clock():
    c = Cell(64, 64)
    c.ell(0.06, 0.06, 0.94, 0.94, (255, 250, 240), (238, 222, 196), w=0.06, outline=(120, 64, 34, 255))
    for k in range(12):
        a = k * math.pi / 6
        r0 = 0.3 if k % 3 else 0.26
        c.line([(0.5 + r0 * math.cos(a), 0.5 + r0 * math.sin(a)), (0.5 + 0.35 * math.cos(a), 0.5 + 0.35 * math.sin(a))], (150, 100, 70), 0.03)
    c.line([(0.5, 0.5), (0.5, 0.25)], INK, 0.06)
    c.line([(0.5, 0.5), (0.68, 0.58)], INK, 0.06)
    c.dot(0.5, 0.5, 0.05, (200, 70, 50))
    return c.result()


def ic_fan():
    c = Cell(64, 64)
    for k in range(4):
        a = k * math.pi / 2 + 0.4
        cx, cy = 0.5 + 0.2 * math.cos(a), 0.5 + 0.2 * math.sin(a)
        pts = []
        for t in range(16):
            u = t / 15 * 2 * math.pi
            px, py = 0.2 * math.cos(u), 0.1 * math.sin(u)
            pts.append((cx + px * math.cos(a) - py * math.sin(a), cy + px * math.sin(a) + py * math.cos(a)))
        c.poly(pts, (150, 210, 250), (90, 160, 220), w=0.035)
    c.dot(0.5, 0.5, 0.09, (230, 236, 244), INK, 0.035)
    return c.result()


def ic_water():
    c = Cell(64, 64)
    pts = spline([(0.5, 0.06), (0.7, 0.4), (0.78, 0.62), (0.66, 0.86), (0.5, 0.92), (0.34, 0.86), (0.22, 0.62), (0.3, 0.4)], 8)
    c.poly(pts, (130, 200, 255), (50, 120, 220), w=0.05, outline=(30, 70, 140, 255))
    c.highlight(0.36, 0.48, 0.48, 0.7, 150)
    return c.result()


def match_icon(head, head2):
    c = Cell(64, 64)
    c.poly([(0.2, 0.86), (0.28, 0.92), (0.72, 0.34), (0.64, 0.28)], (240, 204, 150), (206, 160, 100), w=0.035)
    c.ell(0.55, 0.08, 0.85, 0.4, head, head2, w=0.04)
    c.highlight(0.6, 0.12, 0.72, 0.24, 160)
    return c.result()


def ic_pendulum():
    c = Cell(64, 64)
    c.rect(0.18, 0.06, 0.82, 0.16, WOOD[0], WOOD[2], r=0.04, w=0.03)
    c.line([(0.5, 0.14), (0.66, 0.66)], GOLD[2], 0.06)
    c.dot(0.68, 0.72, 0.15, GOLD[0], INK, 0.04)
    for t in (0.3, 0.45):
        c.line([(0.5 - t * 0.6, 0.5 + t * 0.6), (0.5 - t * 0.5, 0.6 + t * 0.5)], (200, 170, 140), 0.03)
    return c.result()


def ic_book():
    c = Cell(64, 64)
    c.rect(0.16, 0.1, 0.86, 0.92, (210, 96, 62), (160, 64, 40), r=0.08, w=0.045)
    c.rect(0.2, 0.84, 0.86, 0.92, (255, 246, 226), (238, 222, 196), r=0.02, w=0.0, outline=None)
    c.rect(0.16, 0.1, 0.27, 0.92, (170, 70, 44), (130, 50, 30), r=0.04, w=0.0, outline=None)
    A.flame_shape(c, 0.56, 0.5, 0.17, (255, 238, 160), (255, 140, 50), 0.03)
    c.poly([(0.68, 0.1), (0.78, 0.1), (0.78, 0.32), (0.73, 0.27), (0.68, 0.32)], (255, 214, 96), (230, 170, 50), w=0.025)
    return c.result()


def ic_growth():
    c = Cell(64, 64)
    A.flame_shape(c, 0.42, 0.6, 0.3, (255, 236, 150), (255, 110, 40), 0.03)
    c.poly([(0.78, 0.1), (0.94, 0.32), (0.84, 0.32), (0.84, 0.5), (0.72, 0.5), (0.72, 0.32), (0.62, 0.32)], (140, 226, 170), (74, 182, 116), w=0.035, outline=(40, 116, 72, 255))
    return c.result()


def ic_flame_empty():
    c = Cell(64, 64)
    A.flame_shape(c, 0.5, 0.6, 0.34, (250, 240, 226), (236, 220, 200), 0.04)
    return c.result()


def ic_gift2():
    c = Cell(64, 64)
    c.rect(0.12, 0.36, 0.88, 0.9, (255, 120, 100), (210, 70, 60), r=0.06, w=0.04)
    c.rect(0.08, 0.26, 0.92, 0.42, (255, 150, 120), (230, 96, 80), r=0.05, w=0.04)
    c.rect(0.44, 0.26, 0.56, 0.9, (255, 220, 110), (230, 170, 50), w=0.0, outline=None)
    c.ell(0.26, 0.08, 0.5, 0.3, (255, 220, 110), (230, 170, 50), w=0.035)
    c.ell(0.5, 0.08, 0.74, 0.3, (255, 220, 110), (230, 170, 50), w=0.035)
    return c.result()


def ic_arrow_right():
    c = Cell(64, 64)
    c.poly([(0.2, 0.42), (0.56, 0.42), (0.56, 0.24), (0.86, 0.5), (0.56, 0.76), (0.56, 0.58), (0.2, 0.58)], (190, 140, 96), (170, 120, 80), w=0.0, outline=None)
    return c.result()


# ------------------------------------------------------------------ огонёк из слоёв

OUTER_KEY = [(0.50, 0.985), (0.82, 0.95), (0.96, 0.78), (0.97, 0.60), (0.93, 0.47), (0.95, 0.30), (0.82, 0.37), (0.74, 0.29),
             (0.60, 0.02), (0.50, 0.16), (0.40, 0.26), (0.30, 0.35), (0.10, 0.23), (0.12, 0.42), (0.04, 0.60), (0.05, 0.80), (0.18, 0.95)]
MID_KEY = [(0.5, 0.98), (0.8, 0.93), (0.93, 0.76), (0.92, 0.56), (0.8, 0.38), (0.66, 0.22), (0.50, 0.03), (0.44, 0.2), (0.3, 0.34),
           (0.12, 0.5), (0.07, 0.7), (0.18, 0.92)]


def flame_layer(w, h, key, edge_blur, tip_fade=0.35, lum_top=0.86, lum_bottom=1.0, scale=1.0, cy=0.8):
    """Белый слой пламени: мягкий край, прозрачный кончик, яркость к низу выше (для тонировки)."""
    pts = [(0.5 + (x - 0.5) * scale, cy + (y - cy) * scale) for x, y in spline(key, 12)]
    m = mask_poly(w, h, pts, edge_blur)
    H, W = h * SS, w * SS
    yy = np.mgrid[0:H, 0:W][0].astype(np.float32) / H
    top = min(y for _, y in pts)
    fade = np.clip((yy - top) / max(1e-3, tip_fade), 0, 1) ** 0.9
    a = np.array(m).astype(np.float32) / 255.0 * fade
    lum = lum_top + (lum_bottom - lum_top) * np.clip((yy - top) / max(1e-3, 1 - top), 0, 1)
    img = np.zeros((H, W, 4), np.float32)
    img[..., 0] = img[..., 1] = img[..., 2] = 255 * lum
    img[..., 3] = 255 * a
    return down(Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGBA"), w, h)


def flame_core():
    key = [(0.5, 0.97), (0.78, 0.9), (0.88, 0.72), (0.8, 0.5), (0.64, 0.3), (0.52, 0.06), (0.4, 0.3), (0.2, 0.5), (0.12, 0.72), (0.22, 0.9)]
    return flame_layer(104, 140, key, 3.0, tip_fade=0.4, lum_top=0.92, lum_bottom=1.0)


def flame_tongue():
    key = [(0.5, 0.98), (0.8, 0.84), (0.82, 0.6), (0.66, 0.36), (0.6, 0.14), (0.66, 0.02), (0.44, 0.12), (0.3, 0.34), (0.18, 0.6), (0.2, 0.84)]
    return flame_layer(56, 96, key, 1.6, tip_fade=0.55, lum_top=0.8, lum_bottom=1.0)


def flame_highlight():
    w, h = 48, 96
    m = Image.new("L", (w * SS, h * SS), 0)
    pts = spline([(0.42, 0.9), (0.62, 0.7), (0.66, 0.42), (0.56, 0.12), (0.44, 0.36), (0.34, 0.66)], 10)
    ImageDraw.Draw(m).polygon([(x * w * SS, y * h * SS) for x, y in pts], fill=170)
    return down(from_mask(blur(m, 3 * SS)), w, h)


def radial(w, h, power=1.6, color=(255, 255, 255), alpha=255):
    yy, xx = np.mgrid[0:h * SS, 0:w * SS].astype(np.float32)
    d = np.sqrt(((xx + 0.5) / (w * SS) - 0.5) ** 2 / 0.25 + ((yy + 0.5) / (h * SS) - 0.5) ** 2 / 0.25)
    a = np.clip(1 - d, 0, 1) ** power * alpha
    return down(from_mask(Image.fromarray(a.astype(np.uint8)), color), w, h)


def eye_white():
    c = Cell(64, 56)
    c.ell(0.06, 0.06, 0.94, 0.94, (255, 255, 255), (238, 236, 232), w=0.055, outline=(92, 46, 24, 255))
    return c.result()


def pupil():
    c = Cell(40, 40)
    c.dot(0.5, 0.5, 0.46, (52, 28, 18))
    c.dot(0.5, 0.56, 0.3, (82, 44, 28))
    c.dot(0.66, 0.32, 0.15, (255, 255, 255))
    c.dot(0.34, 0.7, 0.07, (255, 255, 255))
    return c.result()


def lid():
    """Верхнее веко: белое (тонируется цветом тела), снизу — тёмная линия ресниц. Масштаб по Y = насколько закрыт глаз."""
    c = Cell(64, 40)
    pts = [(0.03, 0.98)] + [(0.5 - 0.47 * math.cos(t), 0.98 - 0.94 * math.sin(t)) for t in np.linspace(0, math.pi, 24)] + [(0.97, 0.98)]
    c.poly(pts, (255, 255, 255), (250, 246, 240), w=0.0, outline=None)
    c.line([(0.03, 0.95), (0.5, 0.99), (0.97, 0.95)], (92, 46, 24), 0.07)
    return c.result()


def brow():
    c = Cell(48, 20)
    c.line([(0.5 - 0.38 * math.cos(t), 0.78 - 0.5 * math.sin(t)) for t in np.linspace(0.2, math.pi - 0.35, 12)], (92, 46, 24), 0.15)   # дуга, внешний край ниже
    return c.result()


def blush():
    return radial(48, 28, power=1.3, color=(255, 120, 140), alpha=210)


def mouth(kind):
    D = (92, 40, 26)
    if kind == "smile":
        c = Cell(64, 40)
        c.line([(0.5 - 0.34 * math.cos(t), 0.3 + 0.42 * math.sin(t)) for t in np.linspace(0.15, math.pi - 0.15, 12)], D, 0.1)   # мягкая дуга
    elif kind == "o":
        c = Cell(40, 44)
        c.ell(0.2, 0.14, 0.8, 0.86, (140, 46, 46), (96, 28, 28), w=0.08, outline=D + (255,))
        c.ell(0.32, 0.5, 0.68, 0.8, (255, 140, 150), (236, 108, 120), w=0.0, outline=None)
    elif kind == "joy":
        c = Cell(72, 52)
        pts = spline([(0.1, 0.18), (0.9, 0.18), (0.76, 0.66), (0.5, 0.9), (0.24, 0.66)], 10)
        c.poly(pts, (150, 50, 46), (100, 30, 28), w=0.06, outline=D + (255,))
        c.ell(0.3, 0.56, 0.7, 0.86, (255, 140, 150), (236, 108, 120), w=0.0, outline=None)
        c.rect(0.2, 0.16, 0.8, 0.3, (255, 255, 255), (245, 245, 245), r=0.04, w=0.0, outline=None)
    else:  # sleep
        c = Cell(48, 24)
        c.line([(0.12, 0.5), (0.3, 0.35), (0.5, 0.55), (0.7, 0.35), (0.88, 0.5)], D, 0.11)
    return c.result()


def acc_logs():
    c = Cell(160, 56)
    for k, (x0, y0, x1, y1, rot) in enumerate(((0.06, 0.42, 0.62, 0.86, 0), (0.38, 0.4, 0.94, 0.84, 0))):
        c.rect(x0, y0, x1, y1, (150, 92, 52), (104, 60, 32), r=0.06, w=0.02)
        c.ell(x1 - 0.08, y0 + 0.02, x1 + 0.02, y1 - 0.02, (226, 178, 120), (190, 140, 90), w=0.015)
    c.rect(0.25, 0.18, 0.75, 0.58, (138, 82, 46), (96, 54, 30), r=0.06, w=0.02)
    c.ell(0.21, 0.2, 0.31, 0.56, (226, 178, 120), (190, 140, 90), w=0.015)
    for x in (0.3, 0.55, 0.78):
        c.dot(x, 0.86, 0.035, (255, 150, 60))
    return c.result()


def acc_beam():
    w, h = 256, 64
    m = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(m).polygon([(0, 26 * SS), (w * SS, 0), (w * SS, h * SS), (0, 38 * SS)], fill=200)
    a = np.array(blur(m, 4 * SS)).astype(np.float32)
    xs = np.linspace(1, 0, w * SS)[None, :]
    a = a * xs ** 1.2
    return down(from_mask(Image.fromarray(a.astype(np.uint8))), w, h)


def acc_rays():
    w = h = 160
    m = Image.new("L", (w * SS, h * SS), 0)
    d = ImageDraw.Draw(m)
    cx = cy = w * SS / 2
    for k in range(12):
        a = k * math.pi / 6
        r0, r1, half = 0.3 * w * SS, 0.49 * w * SS, 0.11
        d.polygon([(cx + r0 * math.cos(a - half), cy + r0 * math.sin(a - half)), (cx + r1 * math.cos(a), cy + r1 * math.sin(a)),
                   (cx + r0 * math.cos(a + half), cy + r0 * math.sin(a + half))], fill=230)
    return down(from_mask(blur(m, 2 * SS)), w, h)


# ------------------------------------------------------------------ упаковка

def pack(items, size=512, pad=2):
    """MaxRects (лучшее совпадение по короткой стороне); items: (name, img, border)."""
    items = sorted(items, key=lambda t: (-max(t[1].size), -(t[1].size[0] * t[1].size[1])))
    free = [(0, 0, size, size)]
    sheet = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    cells = []
    for name, img, border in items:
        w, h = img.size[0] + pad, img.size[1] + pad
        best = None
        for fx, fy, fw, fh in free:
            if w <= fw and h <= fh:
                score = (min(fw - w, fh - h), max(fw - w, fh - h))
                if best is None or score < best[0]:
                    best = (score, fx, fy)
        if best is None:
            raise SystemExit(f"не поместилось: {name}")
        _, x, y = best
        placed = (x, y, w, h)
        nf = []
        for f in free:
            fx, fy, fw, fh = f
            if x >= fx + fw or x + w <= fx or y >= fy + fh or y + h <= fy:
                nf.append(f)
                continue
            if x > fx: nf.append((fx, fy, x - fx, fh))
            if x + w < fx + fw: nf.append((x + w, fy, fx + fw - x - w, fh))
            if y > fy: nf.append((fx, fy, fw, y - fy))
            if y + h < fy + fh: nf.append((fx, y + h, fw, fy + fh - y - h))
        free = [a for a in nf if not any(a != b and a[0] >= b[0] and a[1] >= b[1] and a[0] + a[2] <= b[0] + b[2] and a[1] + a[3] <= b[1] + b[3] for b in nf)]
        sheet.alpha_composite(img, (x, y))
        cell = {"name": name, "x": x, "y": y, "w": img.size[0], "h": img.size[1]}
        if border:
            cell["border"] = border
        cells.append(cell)
    return sheet, cells


def main():
    cells = json.load(open(os.path.join(OUT, "cells.json")))
    ui = [
        ("ui_card", card(), 44), ("ui_inner", inner(), 30),
        ("ui_btn_amber", button((255, 210, 116), (247, 150, 46), (196, 104, 30), (150, 78, 24)), 50),
        ("ui_btn_cream", button((255, 251, 242), (243, 225, 194), (205, 170, 130), (176, 108, 60)), 50),
        ("ui_btn_mint", button((178, 242, 202), (104, 206, 148), (60, 150, 100), (46, 120, 80)), 50),
        ("ui_shadow", shadow(), 50), ("ui_bubble", bubble(), 44),
        ("ui_pill", pill(), 31), ("ui_tag", tag(), 30), ("ui_track", track(), 23), ("ui_fill", fill(), 23),
        ("ui_circle", circle(), 0), ("ui_ring", ring(), 0), ("ui_soft", soft(), 0), ("ui_streak", streak(), 0),
        ("ui_badge", badge(), 0), ("ui_divider", divider(), 0), ("ui_tag_tail", tag_tail(), 0), ("ui_bubble_tail", bubble_tail(), 0),
        ("ic_coin", ic_coin(), 0), ("ic_check", ic_check(), 0), ("ic_info", ic_info(), 0), ("ic_swap", ic_swap(), 0),
        ("ic_hand", ic_hand(), 0), ("ic_clock", ic_clock(), 0), ("ic_fan", ic_fan(), 0), ("ic_water", ic_water(), 0),
        ("ic_bluematch", match_icon((120, 180, 255), (50, 110, 220)), 0), ("ic_goldmatch", match_icon((255, 226, 110), (220, 160, 40)), 0),
        ("ic_pendulum", ic_pendulum(), 0), ("ic_book", ic_book(), 0), ("ic_growth", ic_growth(), 0), ("ic_flame_empty", ic_flame_empty(), 0),
        ("ic_gift2", ic_gift2(), 0), ("ic_arrow", ic_arrow_right(), 0),
    ]
    sheet, uc = pack(ui)
    sheet.save(os.path.join(OUT, "hearth_ui.png"))
    cells["hearth_ui"] = uc

    fl = [
        ("fl2_outer", flame_layer(192, 240, OUTER_KEY, 3.6, tip_fade=0.42, lum_top=0.82, lum_bottom=1.0), 0),
        ("fl2_mid", flame_layer(152, 196, MID_KEY, 2.4, tip_fade=0.45, lum_top=0.88, lum_bottom=1.0), 0),
        ("fl2_core", flame_core(), 0), ("fl2_tongue", flame_tongue(), 0), ("fl2_hl", flame_highlight(), 0),
        ("fl2_glow", radial(128, 128, power=1.7), 0), ("fl2_shadow", radial(128, 40, power=1.2, color=(30, 12, 6), alpha=200), 0),
        ("fl2_reflect", radial(128, 48, power=1.4), 0),
        ("fc2_white", eye_white(), 0), ("fc2_pupil", pupil(), 0), ("fc2_lid", lid(), 0), ("fc2_brow", brow(), 0), ("fc2_blush", blush(), 0),
        ("fc2_m_smile", mouth("smile"), 0), ("fc2_m_o", mouth("o"), 0), ("fc2_m_joy", mouth("joy"), 0), ("fc2_m_sleep", mouth("sleep"), 0),
        ("acc_logs", acc_logs(), 0), ("acc_beam", acc_beam(), 0), ("acc_rays", acc_rays(), 0),
    ]
    sheet, fc = pack(fl)
    sheet.save(os.path.join(OUT, "hearth_flame2.png"))
    cells["hearth_flame2"] = fc
    json.dump(cells, open(os.path.join(OUT, "cells.json"), "w"), indent=1)
    print("ok", len(uc), len(fc))


if __name__ == "__main__":
    main()
