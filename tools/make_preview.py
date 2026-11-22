"""生成创意工坊预览图（1024x1024）。

六个图标用真品像素放大（NEAREST），不重新绘制，保证预览和游戏里长得一样。
版式：标题 + 六宫格（图标 / 名称）+ 页脚。

质感靠这几层堆出来：
  1. 竖向渐变底 + 极淡的 32px 网格（和游戏 16px 格子同源，放大一倍避免太密）
  2. 左上青、右下品红两团柔光，呼应招牌箱的霓虹
  3. 图标先投一层柔和阴影，再叠像素本身
  4. 标题先画一遍模糊的青色描影，再压白字，做出灯牌那点辉光

依赖：D:\\Anaconda3\\python.exe（Pillow）。图标由 tools/make_icon.py 与 AI 生图产出。
"""
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import os

SIZE = 1024
ICON_SCALE = 6            # 32x48 -> 192x288；48x32 -> 288x192
BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(os.path.dirname(os.path.dirname(BASE)), "dist", "workshop_preview.png")

# (图标文件名, 显示名)
ROWS = [
    [("handbook", "鉴定手册"), ("lottery", "彩票手册"), ("production", "生产手册")],
    [("phonebook", "电话簿"), ("receipt", "回执手册"), ("signbox", "招牌箱")],
]

TITLE = "更好的手册"
SUBTITLE = "六件收纳工具，各自等人送来"
FOOTER = "Better Handbooks  ·  Workshop 1.1.0"

BG_TOP = (30, 33, 44)
BG_BOTTOM = (10, 11, 16)

INK = (238, 236, 230)
INK_DIM = (150, 158, 178)
INK_FAINT = (104, 112, 132)
ACCENT = (79, 214, 200)
ACCENT_WARM = (255, 95, 162)

FONT_BOLD = r"C:\Windows\Fonts\msyhbd.ttc"
FONT_REG = r"C:\Windows\Fonts\msyh.ttc"

MARGIN = 72
CELL_W, CELL_H = 280, 300
GAP_X = 20
ROW1_Y = 246
ROW_GAP = 34
LABEL_GAP = 12


def font(path, size):
    try:
        return ImageFont.truetype(path, size)
    except Exception:
        return ImageFont.load_default()


def vertical_gradient(size, top, bottom):
    strip = Image.new("RGB", (1, size[1]))
    for y in range(size[1]):
        t = y / max(1, size[1] - 1)
        strip.putpixel((0, y), (
            int(top[0] + (bottom[0] - top[0]) * t),
            int(top[1] + (bottom[1] - top[1]) * t),
            int(top[2] + (bottom[2] - top[2]) * t),
        ))
    return strip.resize(size, Image.BILINEAR)


def add_grid(canvas, step=64, alpha=4):
    """极淡的像素网格，让背景不那么空（太密会在大图上形成横向条带）。"""
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for x in range(0, canvas.width + 1, step):
        d.line([(x, 0), (x, canvas.height)], fill=(255, 255, 255, alpha))
    for y in range(0, canvas.height + 1, step):
        d.line([(0, y), (canvas.width, y)], fill=(255, 255, 255, alpha))
    canvas.alpha_composite(layer)


def add_ground_shadow(canvas, cx, baseline, width, alpha=52):
    """图标脚下压一层很淡的椭圆阴影，东西才像"放着"而不是"飘着"。"""
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    h = max(8, int(width * 0.09))
    d.ellipse([cx - width // 2, baseline - h, cx + width // 2, baseline + h],
              fill=(0, 0, 0, alpha))
    layer = layer.filter(ImageFilter.GaussianBlur(max(5, h * 0.9)))
    canvas.alpha_composite(layer)


def add_glow(canvas, center, radius, color, alpha):
    """一团柔光：画个实心圆再高斯模糊，权当廉价的体积光。"""
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    cx, cy = center
    d.ellipse([cx - radius, cy - radius, cx + radius, cy + radius],
              fill=(color[0], color[1], color[2], alpha))
    layer = layer.filter(ImageFilter.GaussianBlur(radius * 0.55))
    canvas.alpha_composite(layer)


def add_vignette(canvas, strength=110):
    """四角压暗，视线往中间收。"""
    mask = Image.new("L", canvas.size, 0)
    d = ImageDraw.Draw(mask)
    d.ellipse([-canvas.width * 0.25, -canvas.height * 0.25,
               canvas.width * 1.25, canvas.height * 1.25], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(canvas.width * 0.12))
    dark = Image.new("RGBA", canvas.size, (0, 0, 0, strength))
    inverse = Image.eval(mask, lambda v: 255 - v)
    canvas.paste(dark, (0, 0), inverse)


def load_icon(name):
    p = os.path.join(BASE, "Textures", "Items", name + ".png")
    im = Image.open(p).convert("RGBA")
    return im.resize((im.width * ICON_SCALE, im.height * ICON_SCALE), Image.NEAREST)


def draw_icon(canvas, im, x, y):
    """先落一层柔影，再把像素压上去，图标才不像贴纸。"""
    shadow = Image.new("RGBA", im.size, (0, 0, 0, 0))
    shadow.putalpha(im.getchannel("A").point(lambda a: int(a * 0.55)))
    shadow = shadow.filter(ImageFilter.GaussianBlur(9))
    canvas.alpha_composite(shadow, (x + 4, y + 10))
    canvas.alpha_composite(im, (x, y))


def fade_line(canvas, y, x0, x1, color, width=2):
    """两端淡出的横线，比一整条实线耐看。"""
    span = max(1, x1 - x0)
    for x in range(x0, x1):
        t = (x - x0) / span
        edge = min(t, 1 - t) * 2          # 0 在两端，1 在中间
        a = int(color[3] * (edge ** 0.55))
        if a <= 0:
            continue
        ImageDraw.Draw(canvas).line([(x, y), (x, y + width - 1)],
                                    fill=(color[0], color[1], color[2], a))


def main():
    canvas = vertical_gradient((SIZE, SIZE), BG_TOP, BG_BOTTOM).convert("RGBA")
    add_glow(canvas, (170, 210), 300, ACCENT, 26)
    add_glow(canvas, (880, 830), 330, ACCENT_WARM, 22)
    add_grid(canvas)
    add_vignette(canvas)

    draw = ImageDraw.Draw(canvas)

    # ── 标题 ────────────────────────────────────────────────
    f_title = font(FONT_BOLD, 86)
    f_sub = font(FONT_REG, 27)
    f_label = font(FONT_BOLD, 27)
    f_foot = font(FONT_REG, 22)

    def text_w(text, f):
        return draw.textbbox((0, 0), text, font=f)[2]

    title_w = text_w(TITLE, f_title)
    title_x = (SIZE - title_w) // 2
    title_y = 74

    glow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).text((title_x, title_y), TITLE, font=f_title,
                              fill=(ACCENT[0], ACCENT[1], ACCENT[2], 150))
    glow = glow.filter(ImageFilter.GaussianBlur(16))
    canvas.alpha_composite(glow)
    draw.text((title_x, title_y), TITLE, font=f_title, fill=INK)

    fade_line(canvas, title_y + 132, MARGIN + 40, SIZE - MARGIN - 40,
              (170, 178, 198, 90), width=2)

    sub_w = text_w(SUBTITLE, f_sub)
    draw.text(((SIZE - sub_w) // 2, title_y + 150), SUBTITLE, font=f_sub, fill=INK_DIM)

    # ── 六宫格 ──────────────────────────────────────────────
    for r, row in enumerate(ROWS):
        row_w = len(row) * CELL_W + (len(row) - 1) * GAP_X
        x0 = (SIZE - row_w) // 2
        y0 = ROW1_Y + r * (CELL_H + ROW_GAP)
        for c, (name, label) in enumerate(row):
            im = load_icon(name)
            cx = x0 + c * (CELL_W + GAP_X)
            ix = cx + (CELL_W - im.width) // 2
            # 底边对齐：册子高 288，箱子只有 192，统一贴到同一条基线
            baseline = y0 + 288
            iy = baseline - im.height
            add_ground_shadow(canvas, cx + CELL_W // 2, baseline + 4, int(CELL_W * 0.58))
            draw_icon(canvas, im, ix, iy)
            # 一条两端淡出的"展台"横线，把六个物件摆成柜台陈列
            fade_line(canvas, baseline + 8, cx + 34, cx + CELL_W - 34,
                      (186, 194, 214, 74), width=2)

            lw = text_w(label, f_label)
            ly = baseline + LABEL_GAP
            draw.text((cx + (CELL_W - lw) // 2, ly), label, font=f_label, fill=INK_DIM)

    # ── 页脚 ────────────────────────────────────────────────
    fy = SIZE - 66
    fade_line(canvas, fy - 22, MARGIN + 40, SIZE - MARGIN - 40,
              (170, 178, 198, 70), width=1)
    fw = text_w(FOOTER, f_foot)
    draw.text(((SIZE - fw) // 2, fy), FOOTER, font=f_foot, fill=INK_FAINT)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    canvas.convert("RGB").save(OUT, quality=95)
    print("saved", OUT, canvas.size)


if __name__ == "__main__":
    main()
