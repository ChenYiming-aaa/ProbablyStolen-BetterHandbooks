"""以原版 tutorial_book 的像素为底版，生成各本册子的图标。

保留原版全部结构（描边、书脊、顶部亮带、正面标签条），只做两件事：
  1) 整组颜色映射到目标色系（连深灰过渡色一起换，避免残留青蓝）；
  2) 在封面压一枚徽记。

徽记画法：先算实心遮罩，再沿遮罩外缘描 1px 深色边。
比手摆方块稳，也比缩小原版图标清晰——32px 画布上，复杂细节只会糊成一团。

尺寸必须是游戏 UISettings.gridSize = 16 的整数倍：
  32x48 = 2x3 格（鉴定/彩票/生产），32x32 = 2x2 格（电话簿）。
图标只决定"占几格"，与册子内部容量无关（原版导师手册就是图标 2x3、内容 10x10）。

依赖：D:/Anaconda3/python.exe（Pillow）。参考图用 tools/extract_ref.py 导出。
"""
from PIL import Image
import math
import os

SRC = "../../generated-images/ref/tutorial_book.png"

PALETTES = {
    "handbook": {   # 暗红褐
        (41, 34, 52): (41, 30, 28),
        (62, 105, 121): (104, 70, 62),
        (69, 145, 139): (140, 96, 80),
        (53, 82, 113): (82, 54, 48),
        (48, 58, 65): (58, 38, 34),
        (68, 70, 76): (72, 48, 42),
        (168, 174, 165): (196, 178, 152),
        (136, 148, 138): (172, 152, 126),
    },
    "lottery": {    # 暗红
        (41, 34, 52): (58, 24, 26),
        (62, 105, 121): (150, 58, 54),
        (69, 145, 139): (196, 92, 78),
        (53, 82, 113): (112, 40, 40),
        (48, 58, 65): (46, 28, 26),
        (68, 70, 76): (66, 40, 38),
        (168, 174, 165): (232, 202, 154),
        (136, 148, 138): (206, 174, 128),
    },
    "production": {  # 墨绿
        (41, 34, 52): (26, 38, 32),
        (62, 105, 121): (56, 104, 82),
        (69, 145, 139): (94, 154, 112),
        (53, 82, 113): (38, 74, 58),
        (48, 58, 65): (28, 38, 32),
        (68, 70, 76): (42, 56, 48),
        (168, 174, 165): (208, 212, 192),
        (136, 148, 138): (182, 188, 168),
    },
    "phonebook": {   # 靛蓝
        (41, 34, 52): (24, 28, 50),
        (62, 105, 121): (56, 70, 126),
        (69, 145, 139): (92, 110, 168),
        (53, 82, 113): (40, 50, 94),
        (48, 58, 65): (30, 34, 48),
        (68, 70, 76): (44, 50, 68),
        (168, 174, 165): (208, 212, 228),
        (136, 148, 138): (180, 186, 204),
    },
    "receipt": {     # 治安部的深蓝灰 + 一枚红章
        (41, 34, 52): (26, 28, 34),
        (62, 105, 121): (50, 58, 76),
        (69, 145, 139): (74, 86, 108),
        (53, 82, 113): (38, 44, 56),
        (48, 58, 65): (28, 32, 40),
        (68, 70, 76): (44, 50, 60),
        (168, 174, 165): (206, 210, 216),
        (136, 148, 138): (178, 184, 192),
    },
    "omni": {        # 紫罗兰 + 金：收别家手册的万能册子
        (41, 34, 52): (36, 28, 48),
        (62, 105, 121): (96, 72, 132),
        (69, 145, 139): (138, 108, 178),
        (53, 82, 113): (68, 50, 98),
        (48, 58, 65): (38, 32, 50),
        (68, 70, 76): (56, 48, 68),
        (168, 174, 165): (232, 214, 178),
        (136, 148, 138): (198, 178, 142),
    },
}


def load(size=None):
    im = Image.open(SRC).convert("RGBA")
    if size is not None and im.size != size:
        im = im.resize(size, Image.NEAREST)
    return im


def recolor(im, table):
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a > 200 and (r, g, b) in table:
                nr, ng, nb = table[(r, g, b)]
                px[x, y] = (nr, ng, nb, a)
    return im


def stamp(im, pts, fill, line):
    """先把遮罩填色，再沿遮罩外缘描 1px 边。"""
    w, h = im.size
    px = im.load()
    for p in pts:
        px[p] = fill
    for (x, y) in pts:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if 0 <= n[0] < w and 0 <= n[1] < h and n not in pts:
                px[n] = line


def mask_gear(im, cx, cy, teeth=6, r_body=4.3, r_tooth=6.3, r_hole=1.7):
    w, h = im.size
    pts = set()
    for y in range(h):
        for x in range(w):
            dx, dy = x - cx, y - cy
            d = (dx * dx + dy * dy) ** 0.5
            if d <= r_hole:
                continue
            if d <= r_body:
                pts.add((x, y))
                continue
            seg = 2 * math.pi / teeth
            off = abs(((math.atan2(dy, dx) + seg / 2) % seg) - seg / 2)
            if d <= r_tooth and off < seg * 0.22:
                pts.add((x, y))
    return pts


# 电话听筒：直接把用户认可的那张横放听筒（两端方耳罩 + 中间下弯的杆）
# 抠出来，整体逆时针转 45°，再降采样成像素。手写几何怎么调都像骨头/钩子，
# 沿着真实轮廓走一遍才保住了"两端耳罩"这个识别特征。
PHONE_ORIGIN = (8, 25)
PHONE_ROWS = [
    ".............###..",
    "............#####.",
    "............######",
    "............######",
    ".............#####",
    "..............####",
    "..............####",
    ".............####.",
    ".............####.",
    ".............####.",
    "............####..",
    "...........####...",
    ".###....#######...",
    "###############...",
    "############......",
    "###########.......",
    ".#########........",
]


def mask_phone(scale=0.72, center=(16.0, 33.0)):
    ox, oy = PHONE_ORIGIN
    pts = {
        (ox + x, oy + y)
        for y, row in enumerate(PHONE_ROWS)
        for x, ch in enumerate(row)
        if ch == "#"
    }
    # 外扩 1px：抠图降采样后颈杆只有 2px，太细会读成"钩子"；加粗到 3px 才像听筒的握杆。
    grown = set(pts)
    for (x, y) in pts:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            grown.add((x + dx, y + dy))
    return zoom(grown, scale, center)


def zoom(pts, scale, center, SS=5):
    """按比例缩放点集（同比例缩小），超采样重采样，避免直接抽点留下空洞。"""
    if abs(scale - 1.0) < 1e-6:
        return set(pts)
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    cx = (min(xs) + max(xs) + 1) / 2.0
    cy = (min(ys) + max(ys) + 1) / 2.0
    w = int(round((max(xs) - min(xs) + 1) * scale))
    h = int(round((max(ys) - min(ys) + 1) * scale))
    x0 = int(round(center[0] - w / 2.0))
    y0 = int(round(center[1] - h / 2.0))
    out = set()
    for j in range(h):
        for i in range(w):
            hit = 0
            for sy in range(SS):
                for sx in range(SS):
                    # 目标像素 -> 源坐标
                    sxp = cx + ((x0 + i + (sx + 0.5) / SS) - (x0 + w / 2.0)) / scale
                    syp = cy + ((y0 + j + (sy + 0.5) / SS) - (y0 + h / 2.0)) / scale
                    if (int(math.floor(sxp)), int(math.floor(syp))) in pts:
                        hit += 1
            if hit * 2 >= SS * SS:
                out.add((x0 + i, y0 + j))
    return out


def mask_magnifier(im, cx, cy, r_out=5.0, r_lens=3.0, handle=4):
    """放大镜：圆环 + 镜片 + 右下 45° 手柄。返回 (环, 镜片, 手柄) 三组点。"""
    w, h = im.size
    ring, lens, grip = set(), set(), set()
    for y in range(h):
        for x in range(w):
            d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
            if d <= r_lens:
                lens.add((x, y))
            elif d <= r_out:
                ring.add((x, y))
    for i in range(handle):
        hx, hy = int(cx) + 4 + i, int(cy) + 4 + i
        for dx in (0, 1):
            p = (hx + dx, hy)
            if 0 <= p[0] < w and 0 <= p[1] < h:
                grip.add(p)
    return ring, lens, grip


def stamp_cards(im, light, outline, ink):
    """两张交叠的名片——比 13px 的听筒好认，而且正是电话簿装的东西。"""
    def card(x0, y0, x1, y1):
        return {(x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1)}

    back = card(11, 17, 21, 24)
    stamp(im, back, light, outline)
    front = card(8, 21, 18, 27)
    stamp(im, front, light, outline)

    px = im.load()
    for x in (10, 12, 14):        # 前面那张的标题与两行字
        px[x, 23] = ink
    for x in (10, 12):
        px[x, 25] = ink


def mask_ticket(x0, y0, x1, y1):
    return {(x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1)}


def ticket_details(im, x0, y0, x1, ink):
    px = im.load()
    for x in (x0 + 2, x0 + 4, x0 + 6):        # 刮奖区
        px[x, y0 + 3] = ink
    for y in (y0 + 1, y0 + 3, y0 + 5):        # 票根虚线
        px[x1 - 2, y] = ink


def stamp_magnifier(im, cx, cy):
    """放大镜：金环 + 深色镜片 + 高光 + 手柄。

    这是鉴定手册的识别特征，尺寸和位置跟旧版一致（中心 16,29.5），
    不许因为"统一风格"把它简化掉。
    """
    gold = (196, 158, 92, 255)
    gold_hi = (226, 192, 132, 255)
    gold_dk = (150, 116, 62, 255)
    lens = (44, 30, 26, 255)
    lens_hi = (172, 156, 140, 255)

    ring, lenspts, grip = mask_magnifier(im, cx, cy)
    px = im.load()
    for (x, y) in ring:
        dx, dy = x - cx, y - cy
        if dx + dy < -1.2:
            px[x, y] = gold_hi
        elif dx + dy > 1.6:
            px[x, y] = gold_dk
        else:
            px[x, y] = gold
    for (x, y) in lenspts:
        px[x, y] = lens
    for (x, y) in lenspts:
        if x < cx and y < cy and ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5 <= 1.6:
            px[x, y] = lens_hi
    for (x, y) in grip:
        px[x, y] = gold_hi if (x + y) % 3 else gold_dk


def stamp_receipt(im, x0, y0, x1, y1):
    """一张竖着夹在封面上的回执单：浅色纸 + 几行条目 + 右下角一枚盖歪的红章。

    红章是识别点，别缩到 2px 以下——32px 画布上一旦糊成暗红点就失去意义。
    """
    paper = (228, 228, 220, 255)
    ink = (56, 60, 68, 255)
    red = (188, 56, 48, 255)
    red_dk = (142, 38, 34, 255)

    sheet = {(x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1)}
    stamp(im, sheet, paper, ink)
    px = im.load()

    # 条目行：长短交错，像金额不一的账目
    for y in (y0 + 2, y0 + 4, y0 + 6):
        end = x1 - 1 if y != y0 + 4 else x1 - 4
        for x in range(x0 + 2, end):
            px[x, y] = ink
    # 底部签字线
    for x in range(x0 + 2, x0 + 6):
        px[x, y1 - 2] = ink

    # 红章："回"字——红方框 + 中心一点红，中间留纸色。
    # 实心色块读起来是"贴了张红纸"，掏空之后才像盖上去的章。
    sx, sy = x1 - 5, y1 - 5
    for y in range(sy, sy + 5):
        for x in range(sx, sx + 5):
            edge = y in (sy, sy + 4) or x in (sx, sx + 4)
            px[x, y] = red if edge else paper
    px[sx + 2, sy + 2] = red
    px[sx + 1, sy + 2] = red_dk
    px[sx + 3, sy + 2] = red_dk


def stamp_omni(im):
    """三本叠放的册子：万能手册的识别特征。

    和电话簿的两张名片区分开——这里是三本厚册子，从后往前画，
    前一本的描边盖住后一本，读起来就是一摞。位置避开封面顶部的亮带，
    集中在 y=21..39 的正面标签区。
    """
    back = (150, 126, 104, 255)
    mid = (190, 164, 126, 255)
    front = (234, 216, 180, 255)
    outline = (36, 28, 48, 255)
    ink = (68, 50, 98, 255)

    def book(x0, y0, x1, y1, color):
        pts = {(x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1)}
        stamp(im, pts, color, outline)

    book(11, 21, 21, 34, back)   # 后：偏右
    book(9, 24, 19, 37, mid)     # 中：偏左
    book(7, 27, 17, 40, front)   # 前：最靠左，露出三本的书脊

    px = im.load()
    # 最前面那本的三行字
    for y, end in ((30, 15), (33, 15), (36, 13)):
        for x in range(10, end):
            px[x, y] = ink


def build(name, size):
    pal = PALETTES[name]
    im = recolor(load(size), pal)
    outline = pal[(41, 34, 52)] + (255,)
    label = pal[(168, 174, 165)] + (255,)
    dark = pal[(53, 82, 113)] + (255,)

    if name == "handbook":
        stamp_magnifier(im, 16.0, 29.5)
    elif name == "lottery":
        stamp(im, mask_ticket(10, 27, 20, 33), label, outline)
        ticket_details(im, 10, 27, 20, dark)
    elif name == "production":
        cy = 33.0 if im.size[1] == 48 else 22.0
        stamp(im, mask_gear(im, 16.0, cy), label, outline)
    elif name == "phonebook":
        # 听筒整体 45°，浅灰金属色，和封面的靛蓝拉开对比。
        stamp(im, mask_phone(), (214, 216, 228, 255), outline)
    elif name == "receipt":
        stamp_receipt(im, 9, 25, 22, 39)
    elif name == "omni":
        stamp_omni(im)

    dst = "Textures/Items/%s.png" % name
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    im.save(dst)
    print(dst, im.size, os.path.getsize(dst), "bytes")


if __name__ == "__main__":
    build("handbook", (32, 48))
    build("lottery", (32, 48))
    build("production", (32, 48))
    build("phonebook", (32, 48))
    build("receipt", (32, 48))
    build("omni", (32, 48))
