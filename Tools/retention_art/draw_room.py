"""Процедурная графика «Очага», часть 2: лицо огонька из частей (эмоции и взгляд), комната (стена, пол, окно со сменой
времени суток, шторы), свет (пятно, виньетка), праздничный декор, маятник часов, пар, замок глав, огоньки серии.
Запуск: python Tools/retention_art/draw_room.py <проект>. Выход: Assets/Art/Retention/hearth_face.png, hearth_room.png
и дописывает нарезку в cells.json."""
import sys, os, json, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import draw_atlas as A
from draw_atlas import Cell, INK, WOOD, COPPER, GOLD, spline, FLAME_KEY
from PIL import Image
import numpy as np

OUT = A.OUT


def radial(w, h, inner, outer, color, inner_a, outer_a, cx=0.5, cy=0.5, power=1.6):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt(((xx + 0.5) / w - cx) ** 2 / 0.25 + ((yy + 0.5) / h - cy) ** 2 / 0.25)
    t = np.clip((d - inner) / max(1e-3, outer - inner), 0, 1) ** power
    a = (inner_a * (1 - t) + outer_a * t)
    img = np.zeros((h, w, 4), np.uint8)
    img[..., 0], img[..., 1], img[..., 2] = color
    img[..., 3] = np.clip(a, 0, 255).astype(np.uint8)
    return Image.fromarray(img, "RGBA")


# ------------------------------------------------------------------ лицо (белое/тёмное, без тонировки)

def face_part(kind):
    c = Cell(64, 64)
    D = (60, 34, 22)
    if kind == "eye":
        c.ell(0.24, 0.12, 0.76, 0.88, (78, 48, 32), (40, 22, 14), w=0.0, outline=None)
        c.dot(0.6, 0.32, 0.12, (255, 255, 255))
        c.dot(0.38, 0.68, 0.055, (255, 255, 255))
    elif kind == "eye_closed":
        c.line([(0.16, 0.48), (0.5, 0.62), (0.84, 0.48)], D, 0.09)
    elif kind == "eye_happy":
        c.line([(0.16, 0.62), (0.5, 0.36), (0.84, 0.62)], D, 0.09)
    elif kind == "eye_wide":
        c.ell(0.18, 0.06, 0.82, 0.94, (255, 255, 255), (235, 235, 235), w=0.05, outline=(70, 40, 26, 255))
        c.dot(0.5, 0.52, 0.2, (60, 34, 22))
        c.dot(0.58, 0.42, 0.06, (255, 255, 255))
    elif kind == "mouth_smile":
        c.line([(0.2, 0.4), (0.5, 0.62), (0.8, 0.4)], D, 0.08)
    elif kind == "mouth_open":
        c.poly(spline([(0.2, 0.32), (0.8, 0.32), (0.66, 0.72), (0.5, 0.82), (0.34, 0.72)], 8), (120, 40, 40), (80, 24, 24), w=0.05, outline=(60, 30, 20, 255))
        c.ell(0.36, 0.56, 0.64, 0.8, (255, 130, 140), (230, 100, 110), w=0.0, outline=None)
    elif kind == "mouth_o":
        c.ell(0.36, 0.3, 0.64, 0.7, (110, 40, 40), (80, 24, 24), w=0.05, outline=(60, 30, 20, 255))
    elif kind == "mouth_flat":
        c.line([(0.3, 0.5), (0.7, 0.5)], D, 0.07)
    elif kind == "cheek":
        c.glow(0.5, 0.5, 0.48, (255, 150, 160), 200)
    elif kind == "brow":
        c.line([(0.18, 0.6), (0.5, 0.42), (0.82, 0.56)], D, 0.08)
    elif kind == "zzz":
        for k, (x, y, s) in enumerate(((0.3, 0.7, 0.18), (0.55, 0.45, 0.14), (0.75, 0.22, 0.1))):
            c.line([(x - s, y - s), (x + s, y - s), (x - s, y + s), (x + s, y + s)], (240, 240, 255), 0.05)
    elif kind == "heart":
        A.icon(c, "heart", 0.5, 0.5, 0.42)
    elif kind == "note":
        c.dot(0.38, 0.72, 0.12, (255, 240, 200), INK, 0.03)
        c.line([(0.48, 0.72), (0.48, 0.18), (0.78, 0.26)], (255, 240, 200), 0.06)
    return c.result()


def flame_shape():
    """Тело без лица (тонируется), с лёгким бликом."""
    c = Cell(256, 320)
    pts = spline(FLAME_KEY, 10)
    c.poly(pts, (255, 255, 252), (228, 226, 218), w=0.018, outline=(140, 128, 118, 255))
    inner = [(0.5 + (x - 0.5) * 0.66, 0.78 + (y - 0.78) * 0.62) for x, y in pts]
    c.poly(inner, (255, 255, 255), (250, 250, 246), w=0.0, outline=None)
    for x in (0.36, 0.64):
        c.ell(x - 0.08, 0.86, x + 0.08, 0.96, (236, 234, 228), (214, 210, 200), w=0.014, outline=(140, 128, 118, 255))
    return c.result()


# ------------------------------------------------------------------ комната

def wall_tile():
    """Стена из досок: 4 доски на тайл, разный тон, волокна и сучки; тайлится по обеим осям."""
    c = Cell(128, 128)
    rnd = random.Random(4)
    base = [(128, 72, 48), (118, 66, 44), (124, 70, 46), (112, 62, 41)]
    for i in range(4):
        x = i * 0.25
        top = base[i]
        bot = top                                                                      # без градиента по y — тайл без полос
        c.rect(x, 0, x + 0.25, 1, top, bot, w=0.0, outline=None)
        c.line([(x + 0.004, 0), (x + 0.004, 1)], (84, 44, 28), 0.014)                 # шов
        c.line([(x + 0.018, 0), (x + 0.018, 1)], (146, 88, 60), 0.006)                # блик кромки
        for k in range(4):                                                             # волокна (вертикальные, без выхода за край по y)
            gx = x + 0.05 + k * 0.05 + rnd.uniform(-0.01, 0.01)
            pts = [(gx + 0.008 * math.sin(t * 6.3 + k + i), t) for t in np.linspace(0, 1, 9)]
            c.line(pts, (104, 56, 36), 0.005)
        if rnd.random() < 0.6:
            ky = rnd.uniform(0.2, 0.8)
            c.ell(x + 0.08, ky - 0.03, x + 0.15, ky + 0.03, (96, 50, 32), (88, 46, 30), w=0.008, outline=(80, 40, 26, 255))
    return c.result()


def floor_tile():
    """Пол из досок: два ряда, стыки вразбежку, волокна, светлая кромка сверху и тёмная щель."""
    c = Cell(128, 64)
    rnd = random.Random(7)
    for k in range(2):
        y = k * 0.5
        top, bot = ((160, 96, 56), (138, 80, 46)) if k == 0 else ((150, 88, 52), (130, 74, 42))
        c.rect(0, y, 1, y + 0.5, top, bot, w=0.0, outline=None)
        c.line([(0, y + 0.012), (1, y + 0.012)], (92, 52, 30), 0.03)                  # щель
        c.line([(0, y + 0.05), (1, y + 0.05)], (182, 118, 72), 0.012)                 # блик
        off = 0.5 * k + 0.12
        for jx in (off % 1.0,):
            c.line([(jx, y + 0.02), (jx, y + 0.5)], (96, 54, 32), 0.022)
            c.line([(jx + 0.012, y + 0.04), (jx + 0.012, y + 0.48)], (172, 110, 66), 0.008)
        for g in range(3):
            gy = y + 0.14 + g * 0.11 + rnd.uniform(-0.02, 0.02)
            c.line([(t, gy + 0.012 * math.sin(t * 9 + g + k)) for t in np.linspace(0, 1, 12)], (128, 74, 42), 0.008)
    return c.result()


def wainscot_tile():
    """Панель нижней части стены (тайлится по x): рама и утопленная филёнка с фаской."""
    c = Cell(128, 80)
    c.rect(0, 0, 1, 1, (102, 56, 34), (88, 48, 28), w=0.0, outline=None)
    c.rect(0, 0, 1, 0.1, (138, 82, 50), (118, 68, 40), w=0.0, outline=None)          # верхний поясок
    c.line([(0, 0.1), (1, 0.1)], (70, 36, 22), 0.02)
    c.rect(0.08, 0.22, 0.92, 0.88, (84, 46, 28), (96, 54, 32), r=0.03, w=0.012, outline=(66, 34, 20, 255))
    c.line([(0.1, 0.24), (0.9, 0.24)], (128, 76, 46), 0.012)                          # фаска (свет сверху)
    c.line([(0.1, 0.86), (0.9, 0.86)], (60, 30, 18), 0.012)
    c.line([(0.0, 0.0), (0.0, 1.0)], (70, 36, 22), 0.02)
    return c.result()


def brick_tile():
    """Кирпичная кладка дымохода (тайлится по обеим осям): 6 рядов вразбежку, разный тон, швы."""
    c = Cell(128, 128)
    c.rect(0, 0, 1, 1, (118, 84, 70), (110, 78, 64), w=0.0, outline=None)            # раствор
    rnd = random.Random(11)
    rows = 6
    for r in range(rows):
        y0, y1 = r / rows + 0.012, (r + 1) / rows - 0.012
        off = 0.0 if r % 2 == 0 else 0.25
        for k in range(-1, 3):
            x0 = off + k * 0.5 + 0.01
            x1 = x0 + 0.48
            if x1 < 0 or x0 > 1:
                continue
            t = rnd.uniform(-12, 12)
            top = (int(170 + t), int(98 + t * 0.6), int(76 + t * 0.4))
            bot = (int(150 + t), int(84 + t * 0.6), int(64 + t * 0.4))
            c.rect(max(0, x0), y0, min(1, x1), y1, top, bot, r=0.01, w=0.0, outline=None)
            c.line([(max(0, x0) + 0.01, y0 + 0.02), (min(1, x1) - 0.01, y0 + 0.02)], (190, 122, 96), 0.01)
    return c.result()


def light_beam():
    """Луч света из окна (белый, мягкий; поворачивается и тонируется в игре)."""
    W, H = 128, 128
    yy, xx = np.mgrid[0:H * 4, 0:W * 4].astype(np.float32)
    u = (xx / (W * 4) - 0.5) * 2
    v = yy / (H * 4)
    width = 0.35 + 0.55 * v
    a = np.clip(1 - np.abs(u) / width, 0, 1) ** 1.5 * np.clip(1 - v, 0, 1) ** 0.8 * np.clip(v * 6, 0, 1)
    img = np.zeros((H * 4, W * 4, 4), np.uint8)
    img[..., :3] = 255
    img[..., 3] = (a * 150).astype(np.uint8)
    return Image.fromarray(img, "RGBA").resize((W, H), Image.LANCZOS)


def window_frame():
    c = Cell(160, 192)
    pts = [(0.08, 0.96), (0.08, 0.42)]
    for i in range(21):
        a = math.pi + math.pi * i / 20
        pts.append((0.5 + 0.42 * math.cos(a), 0.42 + 0.36 * math.sin(a)))
    pts += [(0.92, 0.42), (0.92, 0.96)]
    c.poly(pts, WOOD[0], WOOD[2], w=0.03)
    # прозрачное стекло: вырезаем внутренность
    inner = [(0.17, 0.89), (0.17, 0.44)]
    for i in range(21):
        a = math.pi + math.pi * i / 20
        inner.append((0.5 + 0.33 * math.cos(a), 0.44 + 0.27 * math.sin(a)))
    inner += [(0.83, 0.44), (0.83, 0.89)]
    from PIL import ImageDraw
    mask = Image.new("L", c.img.size, 0)
    ImageDraw.Draw(mask).polygon([c.p(x, y) for x, y in inner], fill=255)
    arr = np.array(c.img)
    m = np.array(mask) > 0
    arr[m, 3] = 0
    c.img = Image.fromarray(arr, "RGBA")
    c.d = ImageDraw.Draw(c.img)   # рисуем дальше по новому холсту
    c.line([(0.5, 0.17), (0.5, 0.89)], WOOD[1], 0.035)
    c.line([(0.17, 0.6), (0.83, 0.6)], WOOD[1], 0.035)
    c.rect(0.02, 0.9, 0.98, 0.98, WOOD[0], WOOD[2], r=0.02, w=0.02)
    return c.result()


def sky():
    c = Cell(32, 64)
    c.rect(0, 0, 1, 1, (255, 255, 255), (190, 190, 190), w=0.0, outline=None)
    return c.result()


def sun():
    c = Cell(64, 64)
    c.glow(0.5, 0.5, 0.5, (255, 240, 180), 200)
    c.dot(0.5, 0.5, 0.2, (255, 244, 200))
    return c.result()


def moon():
    c = Cell(64, 64)
    c.glow(0.5, 0.5, 0.5, (220, 230, 255), 120)
    c.dot(0.5, 0.5, 0.22, (250, 246, 220))
    c.dot(0.58, 0.44, 0.19, (0, 0, 0))
    arr = np.array(c.img)
    black = (arr[..., 0] < 10) & (arr[..., 1] < 10) & (arr[..., 2] < 10)
    arr[black, 3] = 0
    c.img = Image.fromarray(arr, "RGBA")
    return c.result()


def star():
    c = Cell(16, 16)
    c.glow(0.5, 0.5, 0.5, (255, 255, 240), 255)
    return c.result()


def curtains():
    c = Cell(192, 192)
    for side in (0, 1):
        x0 = 0.02 if side == 0 else 0.7
        pts = [(x0, 0.08), (x0 + 0.28, 0.08), (x0 + 0.22 if side == 0 else x0 + 0.06, 0.5), (x0 + 0.26 if side == 0 else x0 + 0.02, 0.96), (x0, 0.96)] if side == 0 else \
              [(x0, 0.08), (x0 + 0.28, 0.08), (x0 + 0.28, 0.96), (x0 + 0.02, 0.96), (x0 + 0.06, 0.5)]
        c.poly(pts, (190, 60, 56), (140, 36, 36), w=0.02)
        for k in range(3):
            xx = x0 + 0.06 + k * 0.07
            c.line([(xx, 0.1), (xx + (0.01 if side == 0 else -0.01), 0.94)], (150, 40, 40), 0.01)
    c.rect(0.0, 0.03, 1.0, 0.09, COPPER[0], COPPER[2], r=0.03, w=0.015)
    c.dot(0.02, 0.06, 0.035, COPPER[1], INK, 0.01)
    c.dot(0.98, 0.06, 0.035, COPPER[1], INK, 0.01)
    return c.result()


def pendulum():
    c = Cell(32, 96)
    c.line([(0.5, 0.0), (0.5, 0.72)], GOLD[2], 0.08)
    c.dot(0.5, 0.82, 0.32, GOLD[0], INK, 0.05)
    return c.result()


def steam():
    c = Cell(32, 32)
    c.glow(0.5, 0.5, 0.5, (255, 255, 255), 200)
    return c.result()


def lock():
    c = Cell(64, 64)
    c.line([(0.32, 0.5), (0.32, 0.3), (0.5, 0.14), (0.68, 0.3), (0.68, 0.5)], (170, 170, 180), 0.08)
    c.rect(0.2, 0.46, 0.8, 0.92, GOLD[0], GOLD[2], r=0.08, w=0.04)
    c.dot(0.5, 0.64, 0.07, INK)
    c.line([(0.5, 0.66), (0.5, 0.8)], INK, 0.05)
    return c.result()


def flame_dot(on):
    c = Cell(48, 48)
    if on:
        c.glow(0.5, 0.55, 0.5, (255, 160, 60), 170)
        A.flame_shape(c, 0.5, 0.62, 0.32, (255, 236, 150), (255, 110, 40), 0.03)
    else:
        A.flame_shape(c, 0.5, 0.62, 0.32, (110, 80, 66), (80, 56, 46), 0.03)
    return c.result()


def seasonal(kind):
    c = Cell(128, 64)
    if kind == "garland":
        pts = [(0.02 + i * 0.08, 0.3 + 0.18 * math.sin(i / 12 * math.pi)) for i in range(13)]
        c.line(pts, (60, 120, 60), 0.05)
        for i, (x, y) in enumerate(pts[1:-1]):
            c.dot(x, y + 0.12, 0.05, [(255, 80, 80), (255, 220, 90), (90, 170, 255), (120, 220, 120)][i % 4], INK, 0.01)
    elif kind == "pumpkins":
        for x, s in ((0.25, 0.32), (0.62, 0.24), (0.85, 0.18)):
            A.icon(c, "pumpkin", x, 0.6, s * 0.9)
    elif kind == "hearts":
        for x, s in ((0.2, 0.18), (0.5, 0.26), (0.8, 0.18)):
            A.icon(c, "heart", x, 0.55, s)
    elif kind == "flowers":
        for x, s in ((0.18, 0.18), (0.42, 0.22), (0.68, 0.2), (0.88, 0.15)):
            A.icon(c, "flower", x, 0.55, s)
    elif kind == "leaves":
        for x, s in ((0.15, 0.2), (0.38, 0.17), (0.6, 0.22), (0.84, 0.18)):
            A.icon(c, "leaf", x, 0.55, s)
    elif kind == "sun":
        A.icon(c, "sun", 0.5, 0.5, 0.34)
    elif kind == "nauryz":
        for x, s in ((0.25, 0.22), (0.75, 0.22)):
            A.icon(c, "nauryz", x, 0.5, s)
    elif kind == "feather":
        A.icon(c, "phoenix", 0.5, 0.45, 0.32)
    return c.result()


def bubble():
    c = Cell(96, 96)
    c.rect(0.04, 0.04, 0.96, 0.96, (255, 250, 238), (248, 238, 218), r=0.3, w=0.035, outline=(150, 100, 70, 255))
    return c.result()


def main():
    cells = json.load(open(os.path.join(OUT, "cells.json")))
    # Лицо и мелочи: 8×8 по 64
    sheet = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    fc = []
    parts = ["eye", "eye_closed", "eye_happy", "eye_wide", "mouth_smile", "mouth_open", "mouth_o", "mouth_flat", "cheek", "brow", "zzz", "heart", "note"]
    for i, k in enumerate(parts):
        x, y = (i % 8) * 64, (i // 8) * 64
        sheet.alpha_composite(face_part(k), (x, y)); fc.append({"name": "fc_" + k, "x": x, "y": y, "w": 64, "h": 64})
    extra = [("s_lock", lock()), ("s_flame_on", flame_dot(True).resize((64, 64))), ("s_flame_off", flame_dot(False).resize((64, 64))), ("rm_steam", steam().resize((64, 64)))]
    for j, (n, im) in enumerate(extra):
        k = 16 + j
        x, y = (k % 8) * 64, (k // 8) * 64
        sheet.alpha_composite(im, (x, y)); fc.append({"name": n, "x": x, "y": y, "w": 64, "h": 64})
    sheet.alpha_composite(flame_shape(), (0, 192)); fc.append({"name": "fl_shape", "x": 0, "y": 192, "w": 256, "h": 320})
    sheet.alpha_composite(sun(), (256, 192)); fc.append({"name": "rm_sun", "x": 256, "y": 192, "w": 64, "h": 64})
    sheet.alpha_composite(moon(), (320, 192)); fc.append({"name": "rm_moon", "x": 320, "y": 192, "w": 64, "h": 64})
    sheet.alpha_composite(star().resize((32, 32)), (384, 192)); fc.append({"name": "rm_star", "x": 384, "y": 192, "w": 32, "h": 32})
    sheet.alpha_composite(sky(), (448, 192)); fc.append({"name": "rm_sky", "x": 448, "y": 192, "w": 32, "h": 64})
    sheet.alpha_composite(pendulum(), (480, 192)); fc.append({"name": "rm_pendulum", "x": 480, "y": 192, "w": 32, "h": 96})
    sheet.save(os.path.join(OUT, "hearth_face.png"))
    cells["hearth_face"] = fc

    # Комната: стена, пол, панель, кирпич, окно, шторы, свет, луч, виньетка, праздники (упаковка MaxRects из draw_ui)
    import draw_ui
    items = [("rm_wall", wall_tile(), 0), ("rm_floor", floor_tile(), 0), ("rm_wainscot", wainscot_tile(), 0), ("rm_brick", brick_tile(), 0),
             ("rm_window", window_frame(), 0), ("dec_curtains", curtains().resize((96, 96)), 0),
             ("rm_light", radial(96, 96, 0.0, 1.0, (255, 170, 80), 210, 0, power=1.3), 0),
             ("rm_vignette", radial(96, 96, 0.55, 1.05, (30, 10, 4), 0, 235, power=1.4), 0),
             ("rm_pool", radial(128, 64, 0.0, 1.0, (255, 190, 110), 170, 0, power=1.2), 0),
             ("rm_beam", light_beam().resize((96, 96), Image.LANCZOS), 0)]
    for k in ["garland", "pumpkins", "hearts", "flowers", "leaves", "sun", "nauryz", "feather"]:
        items.append(("sea_" + k, seasonal(k), 0))
    sheet, rc = draw_ui.pack(items)
    sheet.save(os.path.join(OUT, "hearth_room.png"))
    cells["hearth_room"] = rc
    json.dump(cells, open(os.path.join(OUT, "cells.json"), "w"), indent=1)
    print("ok")


if __name__ == "__main__":
    main()
