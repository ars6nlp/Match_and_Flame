"""Контактный лист PNG: python Tools/preview/contact.py out.png height file1.png file2.png ... (подписи — имена файлов).
COLS=N в окружении — раскладка сеткой по N картинок в ряд (по умолчанию все в один ряд)."""
import sys, os
from PIL import Image, ImageDraw
out, height, files = sys.argv[1], int(sys.argv[2]), sys.argv[3:]
cols = int(os.environ.get("COLS", "0")) or len(files)
ims = []
for f in files:
    im = Image.open(f).convert("RGB")
    w = int(im.width * height / im.height)
    im = im.resize((w, height), Image.LANCZOS)
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, w, 16), fill=(0, 0, 0))
    d.text((3, 2), os.path.splitext(os.path.basename(f))[0], fill=(255, 255, 255))
    ims.append(im)
rows = [ims[i:i + cols] for i in range(0, len(ims), cols)]
W = max(sum(i.width for i in r) + 6 * (len(r) - 1) for r in rows)
sheet = Image.new("RGB", (W, len(rows) * (height + 6) - 6), (30, 30, 30))
for ri, r in enumerate(rows):
    x = 0
    for i in r:
        sheet.paste(i, (x, ri * (height + 6))); x += i.width + 6
sheet.save(out)
