"""生成册子的「装满」图标：把原版导师手册空/满的像素差异套到我们的图标上。

原版做法（generated-images/ref/tutorial_book.png vs tutorial_book_full.png）：
封面完全不动，只有 y=8..11 这 4 行变成露出的纸页——
浅灰纸面 (168,174,165) + 深灰缝线 (136,148,138) + 边缘 2px (55,53,65)。
这三种颜色都在 make_icon.py 的调色板映射表里，所以直接按调色板换色即可。

我们的空版图标是原版空图的纯换色版（几何完全一致），所以原版 diff 的坐标可以直接套。

用法（在 BetterHandbooks 项目根目录下跑，同 make_icon.py 的约定）：
    D:/Anaconda3/python.exe tools/make_filled_icon.py production [handbook ...]

不传名字就跑全部五本。输出 Textures/Items/<name>_full.png。
"""
from PIL import Image
import os
import sys

VANILLA_EMPTY = "../../generated-images/ref/tutorial_book.png"
VANILLA_FULL = "../../generated-images/ref/tutorial_book_full.png"

# make_icon.py 的调色板（复制过来，避免 import 时执行 build()）
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_icon import PALETTES  # noqa: E402

# 原版 diff 引入的三种颜色 -> 调色板键。(55,53,65) 不在表里，归入顶部暗带色。
EXTRA_MAP = {(55, 53, 65): (48, 58, 65)}


def vanilla_diff():
    """返回 {(x, y): 新颜色}，只含原版空->满变化的像素。"""
    a = Image.open(VANILLA_EMPTY).convert("RGBA")
    b = Image.open(VANILLA_FULL).convert("RGBA")
    if a.size != b.size:
        raise SystemExit("vanilla ref size mismatch: %s vs %s" % (a.size, b.size))
    pa, pb = a.load(), b.load()
    diff = {}
    for y in range(a.size[1]):
        for x in range(a.size[0]):
            if pa[x, y] != pb[x, y]:
                diff[(x, y)] = pb[x, y][:3]
    return diff, a.size


def build(name, diff, size):
    src = "Textures/Items/%s.png" % name
    im = Image.open(src).convert("RGBA")
    if im.size != size:
        raise SystemExit("%s is %s, expected %s" % (src, im.size, size))
    pal = PALETTES[name]
    px = im.load()
    changed = 0
    for (x, y), vanilla_color in diff.items():
        mapped = EXTRA_MAP.get(vanilla_color, vanilla_color)
        new = pal.get(mapped)
        if new is None:
            raise SystemExit("no palette entry for %s in %s" % (mapped, name))
        px[x, y] = new + (255,)
        changed += 1
    dst = "Textures/Items/%s_full.png" % name
    im.save(dst)
    print(dst, "patched", changed, "px")


if __name__ == "__main__":
    names = sys.argv[1:] or ["handbook", "lottery", "production", "phonebook", "receipt"]
    diff, size = vanilla_diff()
    for n in names:
        build(n, diff, size)
