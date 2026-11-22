"""生成 AI 场景版创意工坊封面（1024x1024）。

版式要点：
  * AI 场景做底，中央暗部压一层柔光展台，七件真品像素图标一排陈列
  * 关键信息（标题/图标/名称）全部落在中央 16:9 安全区 y=210..810，
    Steam 列表页把方图裁成横图时不会被切掉
  * 标题中英文按字体真实像素边界排布，避免末笔压住英文
  * 图标用游戏内贴图 NEAREST 放大，和游戏里长得一样
  * 页脚版本号随 manifest 同步

依赖：D:\\Anaconda3\\python.exe（Pillow）。底图为 cover/ai-scene-bg.png。
"""
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import os
import re

SIZE = 1024
BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROOT = os.path.dirname(os.path.dirname(BASE))
BG = os.path.join(ROOT, "cover", "ai-scene-bg.png")
OUT = os.path.join(ROOT, "dist", "workshop_preview.png")

FONT_BOLD = r"C:\Windows\Fonts\msyhbd.ttc"
FONT_REG = r"C:\Windows\Fonts\msyh.ttc"

INK = (244, 242, 236)
INK_DIM = (186, 196, 214)
INK_FAINT = (136, 148, 170)
CYAN = (88, 226, 214)
WARM = (255, 112, 174)

TITLE = "更好的手册"
TITLE_EN = "BETTER HANDBOOKS"
SUBTITLE = "七件容器  ·  手册自动归类  ·  放入即归位"

# 陈列顺序：六本册子 + 招牌箱 + 万能手册
# 全部改用 *_full（装满状态）图标：册口露出一叠卡片/纸张，直观体现"能装东西"
ITEMS = [
    ("handbook",   "鉴定手册", 5),
    ("lottery",    "彩票手册", 5),
    ("production", "生产手册", 5),
    ("phonebook",  "电话簿",   5),
    ("receipt",    "回执手册", 5),
    ("signbox",    "招牌箱",   4),
    ("omni",       "万能手册", 5),
]
USE_FULL_ICON = True

SHELF_Y = 650      # 物品统一站立的基线（副标题下移后，整排下移留净空）
CELL_W = 136       # 略放宽格宽，满状态图标顶部有卡片露出，避免相互贴边
LABEL_Y = 666

# 垂直排版锚点（按 msyh 字体真实像素边界算，避免文字互相压住）
# 中文标题 draw@y=224 → 实际墨迹 245..331
TITLE_Y = 224
EN_DY = 112        # 英文 draw@336 → 墨迹 343..358（与中文相隔 12px）
SUB_DY = 148       # 副标题 draw@372 → 墨迹 377..403（与英文相隔 19px）


def font(path, size):
    try:
        return ImageFont.truetype(path, size)
    except Exception:
        return ImageFont.load_default()


def text_w(draw, text, f):
    return draw.textbbox((0, 0), text, font=f)[2]


def dim_bg(im, amount=0.48):
    """把底图整体压暗，好让白色标题和亮像素跳出来。"""
    return Image.blend(im, Image.new("RGB", im.size, (6, 10, 18)), amount)


def add_center_glow(canvas, box, color, alpha):
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.ellipse(box, fill=(color[0], color[1], color[2], alpha))
    layer = layer.filter(ImageFilter.GaussianBlur(70))
    canvas.alpha_composite(layer)


def add_shelf(canvas):
    """一条从中间向两端淡出的展台光带 + 落地阴影，物品才像"摆着"。"""
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    x0, x1 = 92, SIZE - 92
    for x in range(x0, x1):
        t = (x - x0) / (x1 - x0)
        edge = min(t, 1 - t) * 2
        a = int(120 * (edge ** 0.7))
        d.line([(x, SHELF_Y + 12), (x, SHELF_Y + 13)], fill=(120, 226, 220, a))
    canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(4)))

    shadow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    sd.ellipse([x0, SHELF_Y + 2, x1, SHELF_Y + 30], fill=(0, 0, 0, 150))
    canvas.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(14)))


def load_icon(name, scale):
    suffix = "_full" if USE_FULL_ICON else ""
    p = os.path.join(BASE, "Textures", "Items", name + suffix + ".png")
    im = Image.open(p).convert("RGBA")
    return im.resize((im.width * scale, im.height * scale), Image.NEAREST), im.size


def draw_icon(canvas, im, x, y, tint=None):
    shadow = Image.new("RGBA", im.size, (0, 0, 0, 0))
    shadow.putalpha(im.getchannel("A").point(lambda a: int(a * 0.6)))
    shadow = shadow.filter(ImageFilter.GaussianBlur(11))
    canvas.alpha_composite(shadow, (x + 5, y + 12))
    if tint:
        glow = Image.new("RGBA", im.size, (*tint, 0))
        glow.putalpha(im.getchannel("A").point(lambda a: int(a * 0.42)))
        glow = glow.filter(ImageFilter.GaussianBlur(13))
        canvas.alpha_composite(glow, (x, y))
    canvas.alpha_composite(im, (x, y))


def main():
    bg = Image.open(BG).convert("RGB")
    if bg.size != (SIZE, SIZE):
        bg = bg.resize((SIZE, SIZE), Image.LANCZOS)
    canvas = dim_bg(bg).convert("RGBA")

    # 右下角是生图工具的水印：先用邻近柜台像素把它糊掉，再压一层渐隐暗板
    patch = canvas.crop((810, 930, SIZE, SIZE)).filter(ImageFilter.GaussianBlur(24))
    canvas.alpha_composite(patch, (810, 930))
    veil = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    vd = ImageDraw.Draw(veil)
    for i in range(120):
        y = SIZE - 120 + i
        a = int(235 * (i / 120) ** 1.8)
        vd.line([(0, y), (SIZE, y)], fill=(6, 9, 15, a))
    canvas.alpha_composite(veil)

    # 中央展台柔光：青在左、品红在右，呼应背景霓虹
    add_center_glow(canvas, (60, 250, 620, 780), CYAN, 30)
    add_center_glow(canvas, (440, 250, 990, 780), WARM, 24)
    add_shelf(canvas)

    draw = ImageDraw.Draw(canvas)

    f_title = font(FONT_BOLD, 88)
    f_en = font(FONT_REG, 20)
    f_sub = font(FONT_REG, 25)
    f_label = font(FONT_BOLD, 21)

    # ── 标题 ────────────────────────────────────────────────
    tw = text_w(draw, TITLE, f_title)
    tx = (SIZE - tw) // 2
    glow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).text((tx, TITLE_Y), TITLE, font=f_title,
                              fill=(CYAN[0], CYAN[1], CYAN[2], 175))
    canvas.alpha_composite(glow.filter(ImageFilter.GaussianBlur(18)))
    draw = ImageDraw.Draw(canvas)
    draw.text((tx, TITLE_Y), TITLE, font=f_title, fill=INK)

    ew = text_w(draw, TITLE_EN, f_en)
    draw.text(((SIZE - ew) // 2, TITLE_Y + EN_DY), TITLE_EN, font=f_en, fill=CYAN)

    sw = text_w(draw, SUBTITLE, f_sub)
    draw.text(((SIZE - sw) // 2, TITLE_Y + SUB_DY), SUBTITLE, font=f_sub, fill=INK_DIM)

    # ── 七件陈列 ────────────────────────────────────────────
    row_w = len(ITEMS) * CELL_W
    x0 = (SIZE - row_w) // 2
    for i, (name, label, scale) in enumerate(ITEMS):
        im, orig = load_icon(name, scale)
        cx = x0 + i * CELL_W + CELL_W // 2
        ix = cx - im.width // 2
        iy = SHELF_Y - im.height
        tint = (126, 88, 210) if name == "omni" else None
        draw_icon(canvas, im, ix, iy, tint)
        lw = text_w(draw, label, f_label)
        draw.text((cx - lw // 2, LABEL_Y), label, font=f_label,
                  fill=(206, 176, 255) if name == "omni" else INK)

    # 256 色量化：肉眼几乎无损，体积从 ~1100KB 降到 ~520KB，便于工坊上传
    final = canvas.convert("RGB").quantize(
        colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.FLOYDSTEINBERG)
    final.save(OUT, optimize=True)
    print("saved", OUT, canvas.size, "colors=256")


if __name__ == "__main__":
    main()
