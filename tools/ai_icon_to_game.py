"""把 AI 生图成品转成游戏能用的物品图标。

用法：
    D:/Anaconda3/python.exe tools/ai_icon_to_game.py 原图.png Textures/Items/handbook.png
    ... [--size 32x48] [--alpha 110] [--bg auto|white|magenta] [--width-cells 2]

做五件事：
  1) 取四角平均色当背景基准，从四边 flood-fill 抠底（兼容 Z-Image 常画的
     淡黄绿/米色"白底"，不要求背景真的是纯白）；
  2) 裁到内容边界；
  3) 等比缩进目标尺寸，居中放置；
  4) alpha 二值化 —— 游戏里半透明边缘会变灰边，而且 alpha>40 的像素会被算进占位；
  5) 按 8 连通域清掉面积 <5% 主体的漂浮墨点，免得它们把 getbbox 撑大。

最后打印 16x16 分块占用图，用来确认恰好 2x3 格。
"""
import argparse
import sys
from collections import deque

from PIL import Image

GRID = 16


def key_out(im, bg):
    """从四边 flood-fill，把与角落同色的背景变成全透明。"""
    w, h = im.size
    px = im.load()

    # Z-Image 会把"白底"画成淡黄绿、米色甚至带渐变。用四角实际颜色当基准，
    # 凡与它足够接近的像素都算背景；比硬编码 >226 的白判断可靠得多。
    corners = [px[0, 0], px[w - 1, 0], px[0, h - 1], px[w - 1, h - 1]]
    if bg == "magenta":
        base = (255, 0, 255)
    else:
        base = tuple(sum(c[i] for c in corners) // 4 for i in range(3))
        bg = "corner:%d,%d,%d" % base

    def is_bg(c):
        if c[3] < 8:
            return True
        return sum((c[i] - base[i]) ** 2 for i in range(3)) < 70 * 70

    seen = bytearray(w * h)
    q = deque()
    for x in range(w):
        q.append((x, 0))
        q.append((x, h - 1))
    for y in range(h):
        q.append((0, y))
        q.append((w - 1, y))

    while q:
        x, y = q.popleft()
        if not (0 <= x < w and 0 <= y < h):
            continue
        i = y * w + x
        if seen[i]:
            continue
        if not is_bg(px[x, y]):
            continue
        seen[i] = 1
        px[x, y] = (0, 0, 0, 0)
        q.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))

    return im, bg


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src")
    ap.add_argument("dst")
    ap.add_argument("--size", default="32x48", help="目标尺寸，默认 32x48（2x3 格）")
    ap.add_argument("--alpha", type=int, default=110, help="alpha 二值化阈值 0-255")
    ap.add_argument("--bg", default="auto", choices=["auto", "white", "magenta"])
    args = ap.parse_args()

    tw, th = (int(v) for v in args.size.lower().split("x"))
    if tw % GRID or th % GRID:
        sys.exit("目标尺寸必须是 %d 的整数倍" % GRID)

    im = Image.open(args.src).convert("RGBA")
    im, used_bg = key_out(im, args.bg)

    box = im.getbbox()
    if box is None:
        sys.exit("整张图都被抠掉了：换个 --bg 或者把背景换成纯色再跑")
    im = im.crop(box)

    scale = min(tw / im.width, th / im.height)
    nw, nh = max(1, int(round(im.width * scale))), max(1, int(round(im.height * scale)))
    small = im.resize((nw, nh), Image.LANCZOS)

    canvas = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
    canvas.alpha_composite(small, ((tw - nw) // 2, (th - nh) // 2))

    px = canvas.load()
    for y in range(th):
        for x in range(tw):
            r, g, b, a = px[x, y]
            px[x, y] = (r, g, b, 255) if a >= args.alpha else (0, 0, 0, 0)

    # AI 常在主体外留几块孤立墨点。它们与主体不连通，却会被 getbbox 算进内容
    # 边界，导致整本书被缩小。这里按 8 连通域保留主体，丢掉面积 <5% 的碎块。
    seen = bytearray(tw * th)
    blobs = []
    for sy in range(th):
        for sx in range(tw):
            if seen[sy * tw + sx] or px[sx, sy][3] == 0:
                continue
            q = deque([(sx, sy)])
            seen[sy * tw + sx] = 1
            cells = []
            while q:
                x, y = q.popleft()
                cells.append((x, y))
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1),
                               (1, 1), (-1, -1), (1, -1), (-1, 1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < tw and 0 <= ny < th and not seen[ny * tw + nx] and px[nx, ny][3]:
                        seen[ny * tw + nx] = 1
                        q.append((nx, ny))
            blobs.append(cells)

    if blobs:
        biggest = max(len(b) for b in blobs)
        dropped = 0
        for cells in blobs:
            if len(cells) < biggest * 0.05:
                for x, y in cells:
                    px[x, y] = (0, 0, 0, 0)
                dropped += 1
        print("连通域 %d 个，主体 %d px，清掉 %d 个碎块" % (len(blobs), biggest, dropped))

    canvas.save(args.dst)

    print("%s -> %s  %dx%d  (bg=%s, alpha>=%d)" % (args.src, args.dst, tw, th, used_bg, args.alpha))
    print("占用 %dx%d 格（每格 %dpx）:" % (tw // GRID, th // GRID, GRID))
    for by in range(th // GRID):
        row = ""
        for bx in range(tw // GRID):
            filled = sum(
                1
                for y in range(by * GRID, (by + 1) * GRID)
                for x in range(bx * GRID, (bx + 1) * GRID)
                if px[x, y][3] > 0
            )
            row += "#" if filled > GRID * GRID * 0.45 else ("." if not filled else "+")
        print("  " + row)

    any_col = [
        any(px[x, y][3] for x in range(c * GRID, (c + 1) * GRID) for y in range(th))
        for c in range(tw // GRID)
    ]
    rows = [
        any(px[x, y][3] for x in range(tw) for y in range(r * GRID, (r + 1) * GRID))
        for r in range(th // GRID)
    ]
    expected_cols = tw // GRID
    expected_rows = th // GRID
    if any_col != [True] * expected_cols or rows != [True] * expected_rows:
        print("  ! 有整列/整行是空的，形状可能被算成更小的格子")
    print("  期望占位：%d 列 x %d 行" % (expected_cols, expected_rows))


if __name__ == "__main__":
    main()
