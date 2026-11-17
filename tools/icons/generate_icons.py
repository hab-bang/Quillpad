"""
Draws the Quillpad logo (a notepad with "Q_") and writes it into every app logo asset.

    python tools/icons/generate_icons.py              rewrite the logo assets in src/Notepads/Assets
    python tools/icons/generate_icons.py --preview X  only write a preview sheet of all variants to X

Each asset keeps its file name, size and logo position: the script finds where the current logo
sits in the file and draws the new one in the same box. The file name selects the variant:
  - "theme-light", "appicon_b" and the Game Bar "icon.light" files get the dark page,
  - "-dev" files get the cyan DEV badge,
  - "appicon_ws" / "appicon_bs" and the tiles get a soft drop shadow.

Needs Pillow, and Segoe UI Semibold (Windows) for the DEV label.
"""
import argparse
import os
import re

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ASSETS = os.path.join(os.path.dirname(__file__), '..', '..', 'src', 'Notepads', 'Assets')
SUPERSAMPLE = 4

THEMES = {
    'light': dict(page=(232, 232, 232), ring=(108, 108, 108), glyph=(82, 82, 82),
                  fold=((140, 140, 140), (200, 200, 200)), strip=[(171, 171, 171), (140, 140, 140), (71, 71, 71)]),
    'dark': dict(page=(61, 61, 61), ring=(108, 108, 108), glyph=(255, 255, 255),
                 fold=((150, 150, 150), (225, 225, 225)), strip=[(92, 92, 92), (122, 122, 122), (168, 168, 168)]),
}
BADGE = dict(fill=(78, 230, 255), text=(45, 45, 45))
FONT_PATH = os.path.join(os.environ.get('WINDIR', r'C:\Windows'), 'Fonts', 'seguisb.ttf')


def gradient_fill(size, box, start, end):
    """A diagonal gradient image the size of the canvas, from start (top-left of box) to end (bottom-right)."""
    x0, y0, x1, y1 = box
    grad = Image.new('RGBA', size)
    px = grad.load()
    span = max(1.0, (x1 - x0) + (y1 - y0))
    for y in range(int(y0), int(y1) + 1):
        for x in range(int(x0), int(x1) + 1):
            if 0 <= x < size[0] and 0 <= y < size[1]:
                t = min(1.0, max(0.0, ((x - x0) + (y - y0)) / span))
                px[x, y] = tuple(int(start[i] + (end[i] - start[i]) * t) for i in range(3)) + (255,)
    return grad


def draw_logo(w, theme, dev=False, shadow=False):
    """Returns the logo as RGBA, with the notepad page w pixels wide."""
    c = THEMES[theme]
    pad = int(0.08 * w) if shadow else 2
    badge_extra = int(0.17 * w) if dev else 0
    width, height = w + 2 * pad + badge_extra, int(1.13 * w) + 2 * pad
    img = Image.new('RGBA', (width, height))
    d = ImageDraw.Draw(img)
    ox, oy = pad, pad

    # Strip of three tones behind the bottom of the page
    seg = [0.31, 0.38, 0.31]
    x = ox
    strip_top, strip_bottom = oy + int(0.95 * w), oy + int(1.13 * w)
    strip = Image.new('RGBA', img.size)
    sd = ImageDraw.Draw(strip)
    for frac, color in zip(seg, c['strip']):
        sd.rectangle([x, strip_top, x + frac * w, strip_bottom], fill=color + (255,))
        x += frac * w
    mask = Image.new('L', img.size)
    ImageDraw.Draw(mask).rounded_rectangle([ox, strip_top, ox + w, strip_bottom], radius=0.05 * w, fill=255)
    img.paste(strip, (0, 0), mask)

    # Page
    d.rounded_rectangle([ox, oy + 0.08 * w, ox + w, oy + 1.05 * w], radius=0.05 * w, fill=c['page'] + (255,))

    # Four rings across the top edge
    rw, rh = 0.105 * w, 0.17 * w
    for cx in (0.18, 0.395, 0.605, 0.82):
        x0 = ox + cx * w - rw / 2
        d.rounded_rectangle([x0, oy, x0 + rw, oy + rh], radius=rw / 2, fill=c['ring'] + (255,))

    # "Q_": a solid ring, a shaded diagonal tail (like the fold of the old "N") and a cursor bar
    glyph = Image.new('RGBA', img.size)
    gd = ImageDraw.Draw(glyph)
    cx, cy, r, t = ox + 0.335 * w, oy + 0.55 * w, 0.2 * w, 0.085 * w
    gd.ellipse([cx - r, cy - r, cx + r, cy + r], outline=c['glyph'] + (255,), width=int(round(t)))
    # The tail starts inside the bowl and ends just past the ring, at 45 degrees to the bottom right
    ux = uy = 0.7071
    nx, ny = -uy, ux
    inner, outer, half = 0.35 * r, 1.32 * r, 0.5 * t
    tail = [(cx + ux * inner + nx * half, cy + uy * inner + ny * half),
            (cx + ux * inner - nx * half, cy + uy * inner - ny * half),
            (cx + ux * outer - nx * half, cy + uy * outer - ny * half),
            (cx + ux * outer + nx * half, cy + uy * outer + ny * half)]
    tail_mask = Image.new('L', img.size)
    ImageDraw.Draw(tail_mask).polygon(tail, fill=255)
    xs, ys = [p[0] for p in tail], [p[1] for p in tail]
    glyph.paste(gradient_fill(img.size, (min(xs), min(ys), max(xs), max(ys)), *c['fold']), (0, 0), tail_mask)
    baseline = cy + r
    gd.rectangle([ox + 0.62 * w, baseline - 0.07 * w, ox + 0.86 * w, baseline], fill=c['glyph'] + (255,))

    # A soft shadow under the glyph, as on the old logo
    glyph_shadow = Image.new('RGBA', img.size, (0, 0, 0, 0))
    glyph_shadow.putalpha(glyph.getchannel('A').point(lambda v: v * 0.25))
    glyph_shadow = glyph_shadow.filter(ImageFilter.GaussianBlur(0.012 * w))
    img.alpha_composite(glyph_shadow, (0, int(0.01 * w)))
    img.alpha_composite(glyph)

    if dev:
        bx0, by0, bx1, by1 = ox + 0.5 * w, oy + 0.72 * w, ox + w + badge_extra, oy + 1.0 * w
        d.rounded_rectangle([bx0, by0, bx1, by1], radius=0.04 * w, fill=BADGE['fill'] + (255,))
        font = ImageFont.truetype(FONT_PATH, int(0.2 * w))
        d.text(((bx0 + bx1) / 2, (by0 + by1) / 2), 'DEV', fill=BADGE['text'] + (255,), font=font, anchor='mm')

    if shadow:
        alpha = img.getchannel('A').point(lambda v: v * 0.35)
        drop = Image.new('RGBA', img.size, (0, 0, 0, 0))
        drop.putalpha(alpha)
        drop = drop.filter(ImageFilter.GaussianBlur(0.03 * w))
        out = Image.new('RGBA', img.size)
        out.alpha_composite(drop, (0, int(0.02 * w)))
        out.alpha_composite(img)
        img = out
    return img


def solid_bbox(img):
    return img.getchannel('A').point(lambda v: 255 if v > 200 else 0).getbbox()


def variant_for(name):
    base = os.path.basename(name)
    dark = 'theme-light' in base or base.startswith('appicon_b') or base.startswith('icon.light')
    dev = '-dev' in base
    shadow = base.startswith(('appicon_ws', 'appicon_bs', 'Square150x150Logo', 'Wide310x150Logo',
                              'LargeTile', 'SmallTile', 'SplashScreen'))
    return ('dark' if dark else 'light'), dev, shadow


def render_into(path):
    old = Image.open(path).convert('RGBA')
    box = solid_bbox(old)
    if not box:
        return False
    theme, dev, shadow = variant_for(path)
    bw, bh = box[2] - box[0], box[3] - box[1]

    probe = draw_logo(400, theme, dev, shadow)
    pb = solid_bbox(probe)
    scale = min(bw / (pb[2] - pb[0]), bh / (pb[3] - pb[1]))

    big = draw_logo(max(8, int(round(400 * scale * SUPERSAMPLE))), theme, dev, shadow)
    small = big.resize((max(1, round(big.width / SUPERSAMPLE)), max(1, round(big.height / SUPERSAMPLE))), Image.LANCZOS)
    sb = solid_bbox(small) or (0, 0, small.width, small.height)
    x = box[0] + (bw - (sb[2] - sb[0])) // 2 - sb[0]
    y = box[1] + (bh - (sb[3] - sb[1])) // 2 - sb[1]

    out = Image.new('RGBA', old.size)
    out.paste(small, (x, y), small)
    out.save(path, optimize=True)
    return True


LOGO_FILE = re.compile(r'^(Square44x44Logo|Square150x150Logo|Wide310x150Logo|SmallTile|LargeTile|StoreLogo|'
                       r'SplashScreen|appicon_[wb]s?)([.-].*)?\.png$')


def logo_files():
    for name in sorted(os.listdir(ASSETS)):
        if LOGO_FILE.match(name):
            yield os.path.join(ASSETS, name)
    game_bar = os.path.join(ASSETS, 'GameBar', 'Icons')
    for name in sorted(os.listdir(game_bar)):
        if name.endswith('.png'):
            yield os.path.join(game_bar, name)


def preview(out_path):
    tiles = [draw_logo(240, theme, dev, shadow)
             for theme in ('light', 'dark') for dev in (False, True) for shadow in (False, True)]
    cell = max(max(t.width, t.height) for t in tiles) + 20
    sheet = Image.new('RGBA', (cell * 4, cell * 2 + cell // 2), (246, 246, 246, 255))
    for i, t in enumerate(tiles):
        sheet.alpha_composite(t, ((i % 4) * cell + 10, (i // 4) * cell + 10))
    for j, size in enumerate((16, 24, 32, 48, 64)):
        icon = draw_logo(size * SUPERSAMPLE, 'light').resize((size, int(size * 1.13) + 1), Image.LANCZOS)
        sheet.alpha_composite(icon, (10 + j * 80, cell * 2 + 10))
    sheet.save(out_path)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--preview', metavar='PNG', help='write a preview sheet instead of the assets')
    args = parser.parse_args()
    if args.preview:
        preview(args.preview)
        print('preview written to', args.preview)
        return
    written = sum(render_into(path) for path in logo_files())
    print('logo assets written:', written)


if __name__ == '__main__':
    main()
