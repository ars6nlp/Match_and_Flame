"""Продолжение фона огонька меню за пределы кадра (стена вверх, пол вниз) из самих кадров FLAME_MAIN.

Кадры 1280x720 непрозрачные: стена, пол и огонёк. Огонёк в меню снова небольшой, значит кадр не накрывает весь экран.
Чтобы вокруг кадра не было пустоты, сверху и снизу ставятся «зеркальные» полосы: пиксель на стыке совпадает с краем кадра.

menu_wall_tile.png  — [стена; стена отражённая], тайлится вверх от верхнего края кадра (общая для всех скинов:
                      верх кадров у скинов отличается в среднем на ~2/255).
menu_floor_<skin>.png — [пол отражённый; пол], тайлится вниз от нижнего края кадра (у каждого скина свой свет на полу).
Ширина 1024 — как у импортированных кадров (Android override max 1024), чтобы резкость на стыке совпадала.
"""
import math
import os
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
SRC = os.path.join(ROOT, "Assets", "Art", "FLAME_MAIN")
OUT = os.path.join(ROOT, "Assets", "Art", "MenuFlame")
FRAME = "ezgif-frame-025.png"      # спокойный кадр без вспышек
WALL_ROWS = (0, 320)                # две верхние доски стены: ниже (~330–400) капельки над огоньком, при тайлинге они бы повторялись
FLOOR_ROWS = (612, 720)             # пол ниже огонька и его тени (основание ~588, тень до ~600)
OUT_W = 1024

os.makedirs(OUT, exist_ok=True)


def fit(img):
    w, h = img.size
    return img.resize((OUT_W, round(h * OUT_W / w)), Image.LANCZOS)


def clean_sparkles(floor):
    """Блёстки «✦» в правом нижнем углу кадра в тайле повторялись бы — закрываем соседним полом слева."""
    w, h = floor.size
    patch = floor.crop((1000, 0, 1120, h))
    floor.paste(patch, (1120, 0))
    floor.paste(floor.crop((1040, 0, 1080, h)), (1240, 0))
    return floor


base = Image.open(os.path.join(SRC, "flame", FRAME)).convert("RGB")
wall = base.crop((0, WALL_ROWS[0], base.width, WALL_ROWS[1]))
tile = Image.new("RGB", (wall.width, wall.height * 2))
tile.paste(wall, (0, 0))
tile.paste(ImageOps.flip(wall), (0, wall.height))
fit(tile).save(os.path.join(OUT, "menu_wall_tile.png"))

for skin in ("flame", "green", "blue", "purple"):
    img = Image.open(os.path.join(SRC, skin, FRAME)).convert("RGB")
    floor = clean_sparkles(img.crop((0, FLOOR_ROWS[0], img.width, FLOOR_ROWS[1])))
    t = Image.new("RGB", (floor.width, floor.height * 2))
    t.paste(ImageOps.flip(floor), (0, 0))   # картинка рисуется отражённой (scale y = -1), см. MenuFlameLayout
    t.paste(floor, (0, floor.height))
    # Отражённая перспектива досок даёт «ёлочки» — приглушаем середину тайла (1 → 0.55 → 1): у обоих стыков яркость полная
    shade = Image.new("L", (1, t.height))
    for y in range(t.height):
        k = 1.0 - 0.45 * math.sin(math.pi * y / (t.height - 1))
        shade.putpixel((0, y), int(255 * k))
    t = ImageChops.multiply(t, Image.merge("RGB", [shade.resize(t.size)] * 3))
    fit(t).save(os.path.join(OUT, f"menu_floor_{skin}.png"))
# Тень на продолжении пола: градиент от прозрачного (у стыка с кадром) до 0.85; ниже — сплошная тень тем же цветом (MenuFlameLayout)
shade = Image.new("RGBA", (8, 256))
for y in range(256):
    a = 0.85 * (y / 255.0) ** 0.6
    for x in range(8):
        shade.putpixel((x, y), (18, 9, 4, int(255 * a)))
shade.save(os.path.join(OUT, "menu_floor_shade.png"))
# Коробок спичек на полу у правого края: закрывает водяной знак Gemini (✦), вшитый в кадры FLAME_MAIN
# (x 1136–1184, y 575–625 в кадре 1280x720; одинаково во всех кадрах и скинах). Положение подобрано так,
# чтобы непрозрачная часть коробка закрывала знак с запасом 3 px. Спрайт занимает в кадре прямоугольник
# COVER_RECT — по нему MenuFlameLayout/сцена ставят якоря дочернего Image (масштаб — вместе с кадром).
BOX_SRC = os.path.join(ROOT, "Assets", "Art", "Background", "prosto.png")
BOX_W, BOX_X, BOX_Y = 120, 1068, 562          # ширина коробка и левый верхний угол, px кадра
COVER_RECT = (1066, 558, 1192, 678)           # x0, y0, x1, y1 в px кадра (коробок + тень)
SCALE = 2                                      # текстура в 2 раза подробнее кадра
src = Image.open(BOX_SRC).convert("RGBA")
box = src.crop(src.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox())
bw = BOX_W * SCALE
box = box.resize((bw, round(box.height * bw / box.width)), Image.LANCZOS)
cw, ch = (COVER_RECT[2] - COVER_RECT[0]) * SCALE, (COVER_RECT[3] - COVER_RECT[1]) * SCALE
cover = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
ox, oy = (BOX_X - COVER_RECT[0]) * SCALE, (BOX_Y - COVER_RECT[1]) * SCALE
shadow = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
ImageDraw.Draw(shadow).ellipse((ox + 8 * SCALE, oy + box.height - 22 * SCALE, ox + bw + 4 * SCALE, oy + box.height + 6 * SCALE), fill=(10, 5, 2, 120))
cover.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(5 * SCALE)))
cover.alpha_composite(box, (ox, oy))
cover.save(os.path.join(OUT, "menu_matchbox.png"))
print("cover anchors (frame-normalized): min", (COVER_RECT[0] / 1280, 1 - COVER_RECT[3] / 720), "max", (COVER_RECT[2] / 1280, 1 - COVER_RECT[1] / 720))
print("ok", OUT)
