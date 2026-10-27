"""Быстрая проверка эскиза расстановки по таблице связей Builds/link-table.txt (LinkTable в Unity).
Спичка: (x, y, rot) — центр видимой палочки на сетке 0.25, rot — куда идёт огонь.
Использование: from sketch import *; check(sticks, player=(x,y,rot)) — печатает зазоры, связи, нарушения, кто сгорит."""
import math, re, os
HERE = os.path.dirname(os.path.abspath(__file__))
TABLE = os.path.join(HERE, "..", "..", "Builds", "link-table.txt")
SEG, R = 0.665, 0.11          # половина отрезка капсулы и радиус (LayoutCheck)
VIS_OFF = -0.055              # центр капсулы относительно видимого центра вдоль оси (StickFrom/To: −0.61…0.72 от коллайдера)

def load():
    t, sec = {}, None
    for line in open(TABLE, encoding="utf-8"):
        m = re.match(r"A rot (\d+), B rot (\d+):", line)
        if m: sec = (int(m.group(1)), int(m.group(2))); continue
        m = re.match(r"\s+(-?[\d,]+),\s*(-?[\d,]+) gap ([\d,-]+) (A>B|   ) (B>A|   ) ?(!?)", line)
        if m and sec:
            f = lambda v: round(float(v.replace(",", ".")) * 8) / 8
            t[(sec[0], sec[1], f(m.group(1)), f(m.group(2)))] = (m.group(4) == "A>B", m.group(5) == "B>A", m.group(6) == "!")
    return t
T = load()

def rot2(v, deg):
    a = math.radians(deg); c, s = round(math.cos(a)), round(math.sin(a))
    return (c * v[0] - s * v[1], s * v[0] + c * v[1])

def relation(A, B):
    """(A поджигает B, B поджигает A, неустойчиво) по таблице; вне таблицы — нет связи."""
    ra, rb = A[2] % 360, B[2] % 360
    d = (B[0] - A[0], B[1] - A[1])
    if ra >= 180: d = rot2(d, 180); ra -= 180; rb = (rb - 180) % 360
    key = (ra, rb, round(d[0] * 8) / 8, round(d[1] * 8) / 8)
    return T.get(key, (False, False, False))

def seg(S):
    d = (math.cos(math.radians(S[2])), math.sin(math.radians(S[2])))
    c = (S[0] + VIS_OFF * 0, S[1])
    # отрезок капсулы −0.61…0.72 от коллайдера = −0.665…+0.665 от видимого центра
    return ((S[0] - SEG * d[0], S[1] - SEG * d[1]), (S[0] + SEG * d[0], S[1] + SEG * d[1]))

def pseg(p, a, b):
    ab = (b[0] - a[0], b[1] - a[1]); l2 = ab[0] ** 2 + ab[1] ** 2
    t = 0 if l2 == 0 else max(0, min(1, ((p[0] - a[0]) * ab[0] + (p[1] - a[1]) * ab[1]) / l2))
    return math.hypot(p[0] - a[0] - t * ab[0], p[1] - a[1] - t * ab[1])

def cross(a, b, c, d):
    def cr(o, p, q): return (p[0] - o[0]) * (q[1] - o[1]) - (p[1] - o[1]) * (q[0] - o[0])
    d1, d2, d3, d4 = cr(c, d, a), cr(c, d, b), cr(a, b, c), cr(a, b, d)
    return ((d1 > 0 > d2) or (d1 < 0 < d2)) and ((d3 > 0 > d4) or (d3 < 0 < d4))

def gap(A, B):
    a1, a2 = seg(A); b1, b2 = seg(B)
    if cross(a1, a2, b1, b2): return -1
    return min(pseg(a1, b1, b2), pseg(a2, b1, b2), pseg(b1, a1, a2), pseg(b2, a1, a2)) - 2 * R

def check(sticks, names=None, start=None, quiet=False):
    names = names or [chr(97 + i) for i in range(len(sticks))]
    bad, links = [], []
    for i in range(len(sticks)):
        for j in range(i + 1, len(sticks)):
            g = gap(sticks[i], sticks[j])
            if g >= 0.7: continue
            ab, ba, un = relation(sticks[i], sticks[j])
            if un: bad.append(f"{names[i]}-{names[j]} gap {g:.2f} НЕУСТОЙЧИВО")
            if ab or ba: links.append((names[i], names[j], g, ab, ba))
            else: bad.append(f"{names[i]}-{names[j]} gap {g:.2f} без связи")
    if not quiet:
        for l in links: print(f"  link {l[0]}{'>' if l[3] else ''}{'<' if l[4] else ''}{l[1]} gap {l[2]:.2f}")
        for b in bad: print("  BAD", b)
        xs = [p for s in sticks for p in (seg(s)[0][0], seg(s)[1][0])]; ys = [p for s in sticks for p in (seg(s)[0][1], seg(s)[1][1])]
        print(f"  bbox x {min(xs)-R:.2f}..{max(xs)+R:.2f} y {min(ys)-R:.2f}..{max(ys)+R:.2f}")
    if start is not None:
        lit, order = {start}, [start]
        while order:
            k = order.pop(0)
            for j in range(len(sticks)):
                if j not in lit and relation(sticks[k], sticks[j])[0]: lit.add(j); order.append(j)
        if not quiet: print("  from", names[start], "burns", "".join(sorted(names[i] for i in lit)), f"{len(lit)}/{len(sticks)}")
        return bad, links, lit
    return bad, links

# ---------------------------------------------------------------- Перебор

def offsets(ra, rb, lo=0.2, hi=0.3, oneway=True):
    """Сдвиги B относительно A (поворот ra → rb), где A поджигает B (и B не поджигает A, если oneway), зазор lo…hi."""
    out = []
    for (a, b, dx, dy), (ab, ba, un) in T.items():
        for k in range(4):
            if (a + 180 * (k // 2) + 90 * 0) % 360 != ra: pass
    # ra в {0, 90}: прямо; {180, 270}: поворот на 180
    base = ra % 180
    flip = ra >= 180
    for (a, b, dx, dy), (ab, ba, un) in T.items():
        if a != base or un or not ab or (oneway and ba): continue
        rb2 = (b + 180) % 360 if flip else b
        if rb2 != rb % 360: continue
        d = (-dx, -dy) if flip else (dx, dy)
        g = gap((0, 0, ra), (d[0], d[1], rb))
        if lo <= g <= hi: out.append(d)
    return sorted(out)

BOX = (-1.75, 1.75, -2.9, 3.0)

def inside(s, box=BOX):
    (a, b) = seg(s)
    return min(a[0], b[0]) - R >= box[0] and max(a[0], b[0]) + R <= box[1] and min(a[1], b[1]) - R >= box[2] and max(a[1], b[1]) + R <= box[3]

def chain(steps, first, extra_ok=None, box=BOX, limit=200):
    """steps: [(parent_index, rot, filter_fn or None)], first: (x, y, rot). Возвращает варианты без лишних близких пар."""
    res = []
    def dfs(cur):
        if len(res) >= limit: return
        if len(cur) == len(steps) + 1:
            res.append(list(cur)); return
        p, rot, flt = steps[len(cur) - 1]
        A = cur[p]
        for d in offsets(A[2], rot):
            B = (A[0] + d[0], A[1] + d[1], rot)
            if not inside(B, box): continue
            if flt and not flt(B, cur): continue
            ok = True
            for j, C in enumerate(cur):
                g = gap(B, C)
                if g >= 0.7: continue
                ab, ba, un = relation(C, B)
                if j == p and ab and not ba and not un: continue
                if extra_ok and extra_ok(j, len(cur), C, B): continue
                ok = False; break
            if ok: dfs(cur + [B])
    dfs([first])
    return res
