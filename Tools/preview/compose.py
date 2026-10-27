"""Сборка PNG-превью уровней из JSON, выгруженного LevelPreview.cs.

python Tools/preview/compose.py <project> level_17 level_18 ... [--out sheet.png] [--aspect 20:9] [--overlay overlay.json]

Кадр — как у камеры GameScene: ортографический размер из scene.json, портрет; ширина по --aspect (по умолчанию 16:9).
Без аргументов уровней — все level_*.json по отдельности.
"""
import json, os, sys, glob
from PIL import Image, ImageDraw, ImageFont, ImageChops

SCALE = 96  # пикселей превью на мировую единицу (камера size 5 -> 960 px высоты)


def load(project, name):
    with open(os.path.join(project, "Builds", "preview", name + ".json"), encoding="utf-8") as f:
        return json.load(f)


_cache = {}


def sprite_image(project, s):
    key = (s["file"], tuple(s["rect"]))
    if key in _cache:
        return _cache[key]
    src = Image.open(os.path.join(project, s["file"])).convert("RGBA")
    k = src.width / s["tex"][0]  # исходник может быть больше импортированной текстуры (Max Size)
    x, y, w, h = s["rect"]
    box = (round(x * k), round(src.height - (y + h) * k), round((x + w) * k), round(src.height - y * k))
    img = src.crop(box)
    _cache[key] = (img, k)
    return img, k


def tint(img, color):
    r, g, b, a = img.split()
    cr, cg, cb, ca = color
    r = r.point(lambda v: int(v * cr)); g = g.point(lambda v: int(v * cg)); b = b.point(lambda v: int(v * cb))
    a = a.point(lambda v: int(v * ca))
    return Image.merge("RGBA", (r, g, b, a))


def draw_sprite(canvas, project, s, cam, color=None, offset=(0.0, 0.0)):
    img, k = sprite_image(project, s)
    W, H = canvas.size
    m00, m01, m03, m10, m11, m13 = s["m"]
    m03 += m00 * offset[0] + m01 * offset[1]
    m13 += m10 * offset[0] + m11 * offset[1]
    px, py = s["pivot"]; ppu = s["ppu"]
    fx = -1 if s["flip"][0] else 1; fy = -1 if s["flip"][1] else 1
    h_img = img.height
    # (i, j) пиксель кропа -> u = i/k, v = (h - j)/k -> local -> world -> screen
    # local.x = fx*(u - px)/ppu ; local.y = fy*(v - py)/ppu
    ax = fx / (k * ppu); bx = -fx * px / ppu                   # local.x = ax*i + bx
    ay = -fy / (k * ppu); by = fy * (h_img / k - py) / ppu     # local.y = ay*j + by
    # world = M*local + t
    # screen.x = S*(wx - camx) + W/2 ; screen.y = H/2 - S*(wy - camy)
    S = SCALE
    A11 = S * m00 * ax; A12 = S * m01 * ay; C1 = S * (m00 * bx + m01 * by + m03 - cam[0]) + W / 2
    A21 = -S * m10 * ax; A22 = -S * m11 * ay; C2 = -S * (m10 * bx + m11 * by + m13 - cam[1]) + H / 2
    det = A11 * A22 - A12 * A21
    if abs(det) < 1e-9:
        return
    ia, ib, id_, ie = A22 / det, -A12 / det, -A21 / det, A11 / det
    ic = -(ia * C1 + ib * C2); if_ = -(id_ * C1 + ie * C2)
    col = color if color is not None else s["color"]
    if color is None and s.get("golden"):
        col = [1.0, 0.85, 0.2, col[3]]  # как MatchLogic.Start
    src = tint(img, col)
    layer = src.transform((W, H), Image.AFFINE, (ia, ib, ic, id_, ie, if_), resample=Image.BILINEAR)
    if s.get("additive") and color is None:
        r, g, b, a = layer.split()
        rgb = Image.merge("RGB", (r, g, b))
        rgb = ImageChops.multiply(rgb, Image.merge("RGB", (a, a, a)))
        base = canvas.convert("RGB")
        canvas.paste(ImageChops.add(base, rgb).convert("RGBA"))
    else:
        canvas.alpha_composite(layer)


def render(project, scene, level, aspect=9 / 16, overlay=None, title=None):
    cam = scene["camera"]
    H = int(2 * cam["size"] * SCALE)
    W = int(H * aspect)
    bg = tuple(int(c * 255) for c in cam["bg"][:3]) + (255,)
    canvas = Image.new("RGBA", (W, H), bg)
    # Фоновое свечение/виньетка задаются в рантайме (LevelAmbience) — в превью без них
    sprites = [s for s in scene["sprites"] if "Art/Generated/" not in s["file"]] + (level["sprites"] if level else [])
    sprites = sorted(sprites, key=lambda s: (s["order"], -s["z"]))
    for s in sprites:
        if s.get("shadow"):
            sh = s["shadow"]
            draw_sprite(canvas, project, s, (cam["x"], cam["y"]), color=[0, 0, 0, sh["color"][3] * s["color"][3]],
                        offset=(sh["dx"], sh["dy"]))
        draw_sprite(canvas, project, s, (cam["x"], cam["y"]))
    d = ImageDraw.Draw(canvas)
    if overlay:
        for o in overlay:
            pts = [(W / 2 + (x - cam["x"]) * SCALE, H / 2 - (y - cam["y"]) * SCALE) for x, y in o["pts"]]
            col = tuple(o.get("color", [255, 255, 0, 255]))
            if o.get("kind") == "poly":
                d.line(pts + [pts[0]], fill=col, width=2)
            elif o.get("kind") == "dot":
                for p in pts:
                    d.ellipse((p[0] - 4, p[1] - 4, p[0] + 4, p[1] + 4), outline=col, width=2)
            else:
                d.line(pts, fill=col, width=2)
            if o.get("label"):
                d.text(pts[0], o["label"], fill=col)
    if title:
        d.rectangle((0, 0, W, 18), fill=(0, 0, 0, 160))
        d.text((4, 3), title, fill=(255, 255, 255, 255))
    return canvas


def main():
    args = sys.argv[1:]
    project = args.pop(0)
    out = None; aspect = 9 / 16; overlay = None
    names = []
    while args:
        a = args.pop(0)
        if a == "--out": out = args.pop(0)
        elif a == "--aspect":
            w, h = args.pop(0).split(":"); aspect = float(h) / float(w)
        elif a == "--overlay":
            with open(args.pop(0), encoding="utf-8") as f: overlay = json.load(f)
        else: names.append(a)
    scene = load(project, "scene")
    if not names:
        names = sorted(os.path.splitext(os.path.basename(p))[0] for p in glob.glob(os.path.join(project, "Builds", "preview", "level_*.json")))
    images = []
    for n in names:
        lvl = load(project, n)
        img = render(project, scene, lvl, aspect, (overlay or {}).get(n) if isinstance(overlay, dict) else None, title=n)
        images.append(img)
        if not out:
            img.save(os.path.join(project, "Builds", "preview", n + ".png"))
    if out:
        cols = min(len(images), 5)
        rows = (len(images) + cols - 1) // cols
        w, h = images[0].size
        sheet = Image.new("RGBA", (cols * w + (cols - 1) * 6, rows * h + (rows - 1) * 6), (20, 20, 20, 255))
        for i, im in enumerate(images):
            sheet.paste(im, ((i % cols) * (w + 6), (i // cols) * (h + 6)))
        sheet.save(os.path.join(project, "Builds", "preview", out))


if __name__ == "__main__":
    main()
