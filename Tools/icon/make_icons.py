"""Иконки Android и графика магазина из готовой иконки Assets/Art/logo (1).png (2048x2048, белые углы вокруг скругления).

Assets/Art/Icon/icon_adaptive_bg.png  432x432 — тёплый тёмный радиальный фон в палитре картинки
Assets/Art/Icon/icon_adaptive_fg.png  432x432 — картинка в центральных 264 px с мягким круглым краем (прозрачность)
Assets/Art/Icon/icon_round.png        192x192 — круг (фон + картинка)
Assets/Art/Icon/icon_legacy.png       192x192 — скруглённый квадрат (картинка, углы прозрачные)
Builds/store/icon_512.png             512x512 без прозрачности (маску Play накладывает сам)
Builds/store/feature_1024x500.png     логотип + огонёк на тёплом фоне, без текста-обещаний
"""
import math
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
SRC = os.path.join(ROOT, "Assets", "Art", "logo (1).png")
LOGO_EN = os.path.join(ROOT, "Assets", "Art", "logo.png")
OUT = os.path.join(ROOT, "Assets", "Art", "Icon")
STORE = os.path.join(ROOT, "Builds", "store")
os.makedirs(OUT, exist_ok=True)
os.makedirs(STORE, exist_ok=True)

art = Image.open(SRC).convert("RGBA")
N = art.width                      # 2048
CORNER = int(N * 0.155)            # радиус скругления исходной иконки (~318 px)
CENTER_DARK = (46, 30, 30)         # фон картинки у краёв
GLOW = (112, 58, 36)               # тёплое свечение к центру


def rounded_mask(size, radius):
    m = Image.new("L", (size, size), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, size - 1, size - 1), radius=radius, fill=255)
    return m


def radial_bg(size, inner=GLOW, outer=CENTER_DARK, center=(0.5, 0.5)):
    bg = Image.new("RGB", (size, size))
    px = bg.load()
    cx, cy = center[0] * size, center[1] * size
    rmax = math.hypot(size, size) / 2
    for y in range(size):
        for x in range(size):
            t = min(1.0, math.hypot(x - cx, y - cy) / (rmax * 0.95)) ** 1.3
            px[x, y] = tuple(int(inner[i] * (1 - t) + outer[i] * t) for i in range(3))
    return bg


# Картинка без белых углов
clean = art.copy()
clean.putalpha(rounded_mask(N, CORNER))

# 1) Adaptive: фон
bg432 = radial_bg(432)
bg432.save(os.path.join(OUT, "icon_adaptive_bg.png"))

# 2) Adaptive: передний слой — картинка 264x264 в центре, мягкий круглый край (растворяется в фоне)
FG_CONTENT = 264
fg = Image.new("RGBA", (432, 432), (0, 0, 0, 0))
piece = clean.resize((FG_CONTENT, FG_CONTENT), Image.LANCZOS)
soft = Image.new("L", (FG_CONTENT, FG_CONTENT), 0)
d = ImageDraw.Draw(soft)
d.ellipse((6, 6, FG_CONTENT - 7, FG_CONTENT - 7), fill=255)
soft = soft.filter(ImageFilter.GaussianBlur(10))
alpha = Image.composite(piece.getchannel("A"), Image.new("L", piece.size, 0), soft)
piece.putalpha(alpha)
off = (432 - FG_CONTENT) // 2
fg.alpha_composite(piece, (off, off))
fg.save(os.path.join(OUT, "icon_adaptive_fg.png"))

# 3) Round: круг из фона и переднего слоя (видимая часть адаптивной иконки — центральные 72 из 108)
full = bg432.convert("RGBA")
full.alpha_composite(fg)
vis = full.crop((72, 72, 360, 360)).resize((192, 192), Image.LANCZOS)
circle = Image.new("L", (192, 192), 0)
ImageDraw.Draw(circle).ellipse((0, 0, 191, 191), fill=255)
vis.putalpha(circle)
vis.save(os.path.join(OUT, "icon_round.png"))

# 4) Legacy: скруглённый квадрат 192 (картинка целиком, углы прозрачные)
clean.resize((192, 192), Image.LANCZOS).save(os.path.join(OUT, "icon_legacy.png"))

# 5) Play Console 512: во весь квадрат, без прозрачности (картинка чуть крупнее — скругление уходит за край)
scale = 1.13
big = art.convert("RGB").resize((int(512 * scale), int(512 * scale)), Image.LANCZOS)
o = (big.width - 512) // 2
big.crop((o, o, o + 512, o + 512)).save(os.path.join(STORE, "icon_512.png"))

# 6) Feature graphic 1024x500: тёплый фон, логотип слева, огонёк справа
fgx = radial_bg(1024, center=(0.72, 0.5)).resize((1024, 500))
fgx = radial_bg(1024, center=(0.72, 0.55)).crop((0, 262, 1024, 762)).convert("RGBA")
logo = Image.open(LOGO_EN).convert("RGBA")
logo = logo.crop(logo.getbbox())
lw = 560
logo = logo.resize((lw, int(logo.height * lw / logo.width)), Image.LANCZOS)
fgx.alpha_composite(logo, (40, (500 - logo.height) // 2))
hero = clean.resize((440, 440), Image.LANCZOS)
hsoft = Image.new("L", (440, 440), 0)
ImageDraw.Draw(hsoft).ellipse((20, 20, 419, 419), fill=255)
hsoft = hsoft.filter(ImageFilter.GaussianBlur(22))
hero.putalpha(Image.composite(hero.getchannel("A"), Image.new("L", hero.size, 0), hsoft))
fgx.alpha_composite(hero, (1024 - 440 - 20, 30))
fgx.convert("RGB").save(os.path.join(STORE, "feature_1024x500.png"))
print("ok")
