# 图标生图提示词 · 五本册子（不含招牌箱、不含档案箱）

面向外部生图软件（SDXL / Flux / SD1.5+像素 LoRA / Midjourney）。正向、负向、步数等参数、后处理全在这里。

---

## 0. 出图前必须知道的四条硬约束

| 约束 | 数值 | 为什么 |
|---|---|---|
| 成品尺寸 | **32×48 px**，占 2×3 格 | 游戏 `UISettings.gridSize = 16`，图标边长必须是 16 的倍数；内容容量是另一回事 |
| 比例 | **2:3**（竖） | 32:48 就是 2:3，出图时先按这个比例构图，后处理才不用补边 |
| 背景 | **透明，且 alpha 必须二值化** | 半透明边缘在游戏里会渲染成灰边；alpha>40 的半透明像素还会被算进占位，把 2×3 撑成 3×4 |
| 文字 | **一个字母都不要** | 游戏里封面那两条浅色带是色块不是字；AI 写的字缩到 32px 必糊 |

AI 直接出 32×48 一定是糊的。正确流程是：**1024 级别出图 → 抠底 → 下采样到 32×48 → alpha 二值化**。

---

## 1. 共用风格块

五段提示词里都已经内嵌这段，改图时**不要删**，否则五张摆一起不像一套：

```
pixel art game inventory icon, 1990s retro CRPG item sprite, single hardcover binder standing upright, front cover facing the viewer, thin dark spine band across the top, tiny metal ring binding detail at the top-left corner, a pale rectangular label strip across the upper cover (solid colour block, not text), page block visible as a lighter 2-pixel edge along the right side, 1-pixel near-black outline around the whole silhouette, flat cel shading, one soft lighter band across the top, limited 8-colour palette, chunky readable pixels, no anti-aliasing, centred, filling 85% of the frame, isolated on a solid pure white background
```

风格关键词的处理原则：**用"像素画"约束造型，用色值约束配色，用"flat / no gradient / 1-pixel outline"约束渲染**。只写 "pixel art" 的话，模型会给你一张带光泽、带渐变、边缘抗锯齿的"伪像素"，缩到 32px 就散架。

---

## 2. 正向提示词（五段，各自完整可整段复制）

### 2.1 鉴定手册 · Inspection Handbook

配色：暗红褐皮革 + 黄铜放大镜。识别点是**放大镜**，任何情况下都不要换成书签或印章。

```
pixel art game inventory icon, 1990s retro CRPG item sprite, single hardcover binder standing upright, front cover facing the viewer, dark reddish-brown leather boards in #3A2624 and #8C6050, warmer mid-tone panel in the middle of the cover, pale cream paper label strip across the upper cover in #C4B298 as a solid colour block, thin dark spine band across the top, tiny metal ring binding detail at the top-left corner, page block visible as a lighter 2-pixel edge along the right side, a small brass magnifier emblem on the lower-left of the cover, gold rim in #C49E5C with a #E2C084 specular highlight on the upper-left arc, dark tinted lens in #2C1E1A, short diagonal handle pointing to the lower-right, 1-pixel near-black outline around the whole silhouette, flat cel shading, one soft lighter band across the top, limited 8-colour palette, chunky readable pixels, no anti-aliasing, centred, filling 85% of the frame, isolated on a solid pure white background, no text, no letters, no numbers, no watermark, no shadow, no ground
```

### 2.2 彩票手册 · Lottery Handbook

配色：暗红封面 + 米色票根。识别点是**票根（带刮奖区虚线）**。

```
pixel art game inventory icon, 1990s retro CRPG item sprite, single hardcover binder standing upright, front cover facing the viewer, deep crimson red boards in #5A181A, #963A36 and #C45C4E, thin dark spine band across the top, tiny metal ring binding detail at the top-left corner, pale cream paper label strip across the upper cover in #E8CA9A as a solid colour block, page block visible as a lighter 2-pixel edge along the right side, a single pale ticket stub emblem lying on the lower-left of the cover, cream paper in #E8CA9A with a near-black outline, three short dotted rows on the left half suggesting a scratch panel, a vertical dashed tear line near the right edge of the stub, 1-pixel near-black outline around the whole silhouette, flat cel shading, one soft lighter band across the top, limited 8-colour palette, chunky readable pixels, no anti-aliasing, centred, filling 85% of the frame, isolated on a solid pure white background, no text, no letters, no numbers, no watermark, no shadow, no ground
```

### 2.3 生产手册 · Production Handbook

配色：墨绿封面 + 浅色齿轮。识别点是**六齿齿轮**（不是扳手、不是烧瓶）。

```
pixel art game inventory icon, 1990s retro CRPG item sprite, single hardcover binder standing upright, front cover facing the viewer, deep bottle green boards in #1A2620, #386852 and #5E9A70, thin dark spine band across the top, tiny metal ring binding detail at the top-left corner, pale sage paper label strip across the upper cover in #D0D4C0 as a solid colour block, page block visible as a lighter 2-pixel edge along the right side, a chunky six-tooth industrial gear emblem centred on the lower half of the cover, off-white metal in #D0D4C0 with a darker #386852 shadow on the lower-right teeth and a small hollow hub in the centre, heavy 1-pixel near-black outline around the gear and around the whole silhouette, flat cel shading, one soft lighter band across the top, limited 8-colour palette, chunky readable pixels, no anti-aliasing, centred, filling 85% of the frame, isolated on a solid pure white background, no text, no letters, no numbers, no watermark, no shadow, no ground
```

### 2.4 电话簿 · Phonebook

配色：靛蓝封面 + 浅灰蓝听筒。识别点是**斜 45° 的老式听筒**（两端有耳罩，中间下弯）。这张最容易画成骨头/钩子/香蕉，出图后务必检查两端耳罩是否明显。

```
pixel art game inventory icon, 1990s retro CRPG item sprite, single hardcover binder standing upright, front cover facing the viewer, indigo blue boards in #181C32, #38467E and #5C6EA8, thin dark spine band across the top, tiny metal ring binding detail at the top-left corner, pale blue-grey paper label strip across the upper cover in #D0D4E4 as a solid colour block, page block visible as a lighter 2-pixel edge along the right side, a single old telephone handset emblem on the lower half of the cover, handset tilted 45 degrees running from upper-right down to lower-left, light grey-blue plastic in #D6D8E4, clearly visible earpiece cup at each end and a curved grip bar in the middle, 1-pixel near-black outline, flat cel shading, one soft lighter band across the top, limited 8-colour palette, chunky readable pixels, no anti-aliasing, centred, filling 85% of the frame, isolated on a solid pure white background, no text, no letters, no numbers, no watermark, no shadow, no ground
```

### 2.5 回执手册 · Receipt Book

配色：治安部深蓝灰 + 一张浅色回执 + 红方章。识别点是**红章**，且章必须是方框（实心红块会读成"贴了张红纸"，红底白十字会读成"急救"）。

```
pixel art game inventory icon, 1990s retro CRPG item sprite, single hardcover binder standing upright, front cover facing the viewer, dark security navy-grey boards in #1A1C22, #32384C and #4A566C, thin dark spine band across the top, tiny metal ring binding detail at the top-left corner, pale grey paper label strip across the upper cover in #CED2D8 as a solid colour block, page block visible as a lighter 2-pixel edge along the right side, a pale receipt sheet tucked vertically on the lower half of the cover, paper in #E4E4DC with a near-black outline, three ruled entry lines of uneven length, a short signature line at the bottom-left of the sheet, a small square red rubber stamp in the lower-right corner of the sheet, hollow square outline in #BC3830 with a thin dark red #8E2622 inner mark and paper colour showing through the middle, 1-pixel near-black outline around the whole silhouette, flat cel shading, one soft lighter band across the top, limited 8-colour palette, chunky readable pixels, no anti-aliasing, centred, filling 85% of the frame, isolated on a solid pure white background, no text, no letters, no numbers, no watermark, no shadow, no ground
```

---

## 3. 负向提示词（五张通用，整段复制）

```
3d render, cgi, photorealistic, glossy plastic, specular bloom, gradient shading, soft airbrush, anti-aliasing, smudged edges, blurry, noise, film grain, jpeg artifacts, lens flare, bokeh, depth of field, perspective distortion, tilted camera, isometric, open book, fanned pages, loose pages flying, bookmark, ribbon, dust jacket, embossed filigree, gold foil ornament, leather tooling, multiple books, stack of books, library shelf, hands, cartoon mascot, anime, chibi, cute face, eyes, text, letters, numbers, typography, logo, watermark, signature, mockup, ui frame, rounded border, drop shadow, ground plane, reflection, clutter, background props
```

Flux.1 [dev] 蒸馏版不支持负向，把上面这几条**写进正向末尾**即可（正向里已经有 `no text / no shadow / no ground`，够用）。

---

## 4. 参数

### 4.1 SDXL（Juggernaut XL / DreamShaper XL / Illustrious 系，推荐首选）

| 参数 | 值 | 说明 |
|---|---|---|
| 分辨率 | **768×1152** | 2:3；想更快用 512×768，但细节会少一档 |
| 步数 Steps | **30**（28~36） | 低于 24 描边会断，高于 40 只是耗时 |
| CFG | **5.5**（4.5~7.0） | 拉高到 8 以上配色会过饱和，五张拼一起不统一 |
| 采样器 | **DPM++ 2M Karras** | 边缘最干净；Euler a 次之 |
| 调度器 | Karras | |
| 重绘幅度 | 1.0（txt2img） | |
| Hires fix | **先关** | 边缘毛糙时再开 1.5×、denoise 0.30、4x-UltraSharp |
| 批次 | 4 | 挑 16×16 分块最干净的那张 |
| 种子 | 每本固定一个 | 换构图只改提示词，别跟种子一起变，否则分不清是哪边起的作用 |

### 4.2 Flux.1 [dev] / [schnell]

| 参数 | 值 |
|---|---|
| 分辨率 | **1024×1536**（2:3） |
| Steps | **28**（dev）／4（schnell） |
| Guidance（蒸馏） | **3.5** |
| CFG | 1.0 |
| 采样器 | euler + simple |
| 负向 | 不支持，见第 3 节说明 |

Flux 对"1 像素描边、限色"的服从度不如 SDXL，出图更"漂亮"但不"像素"。**要么只用它出概念图再手绘，要么把 LoRA 关掉、把 `chunky readable pixels / limited 8-colour palette` 重复写两遍**。

### 4.3 SD1.5 + 像素画 LoRA（最贴近最终效果）

| 参数 | 值 |
|---|---|
| 分辨率 | **512×768** |
| Steps | **28** |
| CFG | **7** |
| 采样器 | DPM++ 2M Karras |
| LoRA 权重 | **0.75**（0.6~0.9，过高会满屏抖动噪点） |
| Hires fix | 2×，denoise 0.35 |
| 触发词 | 按 LoRA 作者要求加（常见 `pixel art` / `pixelart`），加在正向最前面 |

### 4.4 Midjourney v7

```
<提示词> --ar 2:3 --style raw --s 50 --v 7 --no text, shadow, gradient, 3d render
```
MJ 没有步数/CFG，`--s 50` 是风格化强度（越低越平），`--style raw` 减少默认美化。出图后同样要抠底 + 下采样。

### 4.5 Z-Image（本机 ComfyUI 0.41.0 实测，2026-11-10）

**用这套，别用 8 步 / cfg 1.0 的 Turbo 参数。** 本地权重是 `z_image-Q4_K_M.gguf`（GGUF 元数据只写 `architecture=lumina2`，没有 turbo/base 标识），实测按 Turbo 的 8 步 / cfg 1.0 出图会变成带透视的 3D 厚书、水彩笔触、底部一行乱码文字；按下面这套才得到平面像素图标。

| 项 | 值 |
|---|---|
| 模型 | `UnetLoaderGGUF` → `z_image-Q4_K_M.gguf` |
| 文本编码器 | `CLIPLoaderGGUF` → `Qwen3-4B-Q4_K_M.gguf`，type **`lumina2`**（ComfyUI 检测到 QWEN3_4B 权重会自动走 `z_image` TE，官方蓝图也是这么配的） |
| VAE | `ae.safetensors` |
| 采样 | **25 步 / cfg 4.0 / res_multistep / simple / denoise 1.0** |
| 位移 | `ModelSamplingAuraFlow` shift **3.0** |
| 画布 | `EmptySD3LatentImage` **768×1152** |
| 负向 | cfg 4 > 1，用第 3 节的文本负向接 `CLIPTextEncode`；**不要**用 `ConditioningZeroOut`（那是 cfg=1 蒸馏版用的） |
| 耗时 | RTX 3060 Laptop 6GB / `--lowvram`，约 200 秒/张（首张含模型加载 70 秒） |

**提示词必须改写**：Z-Image 会把 `standing upright` + `page block visible on the right side` 理解成有厚度的 3D 盒子，把 `pixel art` 画成水彩。`tools/run_comfy_zimage.py` 里的 `adapt_for_zimage()` 做了五处替换（强制 `orthographic front view / no visible top or side faces / solid flat colour blocks only`、删掉页缘描述、去掉渐变与笔触、清空背景），并在末尾追加压制 `no writing / no scribbles / no debris`。五段提示词的差异部分（配色、徽记）完整保留。

一键复现：

```bash
D:/Anaconda3/python.exe tools/run_comfy_zimage.py --suffix v1      # 五张全跑
D:/Anaconda3/python.exe tools/run_comfy_zimage.py --test --suffix v2  # 只跑鉴定手册试参数
```

原图落在工作区 `generated-images/zimage_icons/`；`--suffix` 用来保留多组对比结果，不会互相覆盖。

### 4.6 2026-11-10 实跑结果

五张全部生成完毕，产物在 `generated-images/zimage_icons/`：

| 文件 | 内容 |
|---|---|
| `<name>_zimage.png` | 768×1152 原图五张 |
| `<name>_32x48.png` | 抠底 + alpha 二值化后的游戏尺寸，五张占位都验证为 2×3 |
| `sheet_zimage.png` | 五张原图并排 |
| `sheet_compare.png` | 上排现有游戏图标 / 下排 AI 版 32×48 对照 |
| `run_params_b.json` | 每张的 seed、耗时、参数、结论 |

耗时：160~206 秒/张（RTX 3060 Laptop 6GB）。

**结论：适合当概念稿，不建议直接替换 `Textures/Items/`。** 五张徽记都能认出来（放大镜、票根、齿轮、听筒、回执单），但和现有图标比有四处系统性差异：

1. **配色偏亮**：封面被画成亮橙、品红、亮绿、电蓝，而游戏里是低饱和暗色。Z-Image 对 `#3A2624` 这类色值基本不买账。
2. **顶部标签条过大**：AI 版占了上部一整块，现有图标是一条细横带。
3. **左侧多一列装订环**：现有图标只有左上角一个环 + 顶部横带。
4. **回执红章画成实心红块**：提示词要求的空心方框没做到，缩到 32px 后读成"贴了张红纸"。
5. 电话簿那张没有外描边，边缘直接切齐，和另外四张不成套。

要真的用进游戏，最省事的是**别再调提示词**，而是出图后做一次后处理：按 `make_icon.py` 里的 `PALETTES` 把封面色映射到现有色板、把顶部标签条裁细、去掉左侧装订环列、把红章改成空心方框。这几步都在 32×48 像素上做，比再跑五张快得多。

---

## 5. 出图 → 进游戏

写好了转换脚本：

```bash
D:/Anaconda3/python.exe tools/ai_icon_to_game.py 原图.png Textures/Items/handbook.png
```

它会做四件事：从四边 flood-fill 抠掉白底（或洋红底）→ 裁到内容边界 → 等比缩进 32×48 → alpha 二值化（阈值 110）。然后打印一张 16×16 分块占用图，形如：

```
  ##
  ##
  ##
```

三行两列、每格都满，才算 2×3 合规。出现第三列或第四行，说明边缘有半透明残渣撑大了占位——把 `--alpha 130` 提高阈值再跑一次。

---

## 6. 验收清单（每张图都过一遍）

1. 32×48 且 alpha 全为 0 或 255，没有中间值。
2. 16×16 分块占用正好 2 列 × 3 行。
3. 缩小到 100% 大小，能一眼认出徽记：放大镜 / 票根 / 齿轮 / 听筒 / 红章。
4. 五张并排，封面底色、描边粗细、标签条位置一致。
5. 进游戏后图标不是问号、没有灰边；若变成问号，先查 `Player.log` 的 `Sprite name 'xxx' in atlas 'better.handbooks' was not found`。
