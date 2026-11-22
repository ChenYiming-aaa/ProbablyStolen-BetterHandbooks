"""用本地 ComfyUI (127.0.0.1:8188) 的 Z-Image-Turbo Q4 工作流批量出五本册子图标。

提示词单一来源：tools/ICON_PROMPTS.md 第 2 节的五个代码块，不在本脚本里重复写。
参数取自 ComfyUI 官方蓝图 Text to Image (Z-Image-Turbo)：
  UnetLoaderGGUF + CLIPLoaderGGUF(lumina2) + VAELoader(ae.safetensors)
  ModelSamplingAuraFlow(shift=3) + EmptySD3LatentImage
  KSampler steps=8 cfg=1.0 sampler=res_multistep scheduler=simple
  负向走 ConditioningZeroOut（蒸馏版 cfg=1 时负向不参与，压制内容写在正向里）

用法：
  python tools/run_comfy_zimage.py --test          # 只跑鉴定手册一张，验证链路
  python tools/run_comfy_zimage.py                 # 跑五张
  python tools/run_comfy_zimage.py --width 640 --height 960 --steps 8
"""
import argparse
import io
import json
import os
import re
import time
import urllib.error
import urllib.parse
import urllib.request

BASE = "http://127.0.0.1:8188"
HERE = os.path.dirname(os.path.abspath(__file__))
MD = os.path.join(HERE, "ICON_PROMPTS.md")
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "generated-images", "zimage_icons"))

NAMES = ["handbook", "lottery", "production", "phonebook", "receipt"]
SEEDS = [1101, 1102, 1103, 1104, 1105]


def get_json(path, timeout=30):
    with urllib.request.urlopen(BASE + path, timeout=timeout) as r:
        return json.load(r)


def post_json(path, payload, timeout=60):
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(BASE + path, data=data,
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.load(r)


def load_prompts():
    text = io.open(MD, encoding="utf-8").read()
    section = text.split("## 2. 正向提示词", 1)[1].split("## 3.", 1)[0]
    blocks = re.findall(r"```\n(.*?)\n```", section, re.S)
    blocks = [b.strip().replace("\n", " ") for b in blocks]
    if len(blocks) != len(NAMES):
        raise RuntimeError("ICON_PROMPTS.md 第 2 节应有 %d 段提示词，实际 %d 段" % (len(NAMES), len(blocks)))
    neg = re.findall(r"```\n(.*?)\n```", text.split("## 3. 负向提示词", 1)[1], re.S)
    return dict(zip(NAMES, blocks)), (neg[0].strip().replace("\n", " ") if neg else "")


def adapt_for_zimage(p):
    """Z-Image 把 'standing upright / page block edge' 理解成 3D 厚书，
    把 'pixel art' 理解成水彩插画。这里强制平面正交视图 + 纯色块。"""
    swaps = [
        ("pixel art game inventory icon, 1990s retro CRPG item sprite, single hardcover binder standing upright, front cover facing the viewer",
         "flat 2D pixel art game item icon, straight-on orthographic front view, one closed flat book seen exactly from the front like a printed card, no perspective, no visible top face, no visible side face, no 3D volume, no thickness"),
        ("thin dark spine band across the top",
         "a dark horizontal band along the top edge of the flat rectangle"),
        ("page block visible as a lighter 2-pixel edge along the right side, ", ""),
        ("flat cel shading, one soft lighter band across the top",
         "solid flat colour blocks only, hard crisp pixel edges, no gradient, no shading, no gloss, no brush strokes, no watercolour, no texture"),
        ("isolated on a solid pure white background",
         "isolated on a completely empty clean pure white background"),
    ]
    for old, new in swaps:
        if old not in p:
            print("  [warn] 未命中替换: %s ..." % old[:60])
        p = p.replace(old, new)
    p += (", no perspective, no isometric view, no 3d render, no photorealism, "
          "no writing anywhere, no letters, no numbers, no signature, no scribbles, "
          "no decorative flourishes, no scattered debris, no fragments, no border lines, "
          "no ground, no drop shadow, clean single object only")
    return p


def build_workflow(prompt, negative, seed, width, height, steps, cfg, prefix):
    # cfg=1 的蒸馏版负向不参与计算；cfg>1 时才把负向文本接上。
    negative_node = ({"class_type": "CLIPTextEncode",
                      "inputs": {"text": negative, "clip": ["2", 0]}}
                     if cfg > 1.0 and negative else
                     {"class_type": "ConditioningZeroOut",
                      "inputs": {"conditioning": ["5", 0]}})
    return {
        "1": {"class_type": "UnetLoaderGGUF",
              "inputs": {"unet_name": "z_image-Q4_K_M.gguf"}},
        "2": {"class_type": "CLIPLoaderGGUF",
              "inputs": {"clip_name": "Qwen3-4B-Q4_K_M.gguf", "type": "lumina2"}},
        "3": {"class_type": "VAELoader",
              "inputs": {"vae_name": "ae.safetensors"}},
        "4": {"class_type": "ModelSamplingAuraFlow",
              "inputs": {"model": ["1", 0], "shift": 3.0}},
        "5": {"class_type": "CLIPTextEncode",
              "inputs": {"text": prompt, "clip": ["2", 0]}},
        "6": negative_node,
        "7": {"class_type": "EmptySD3LatentImage",
              "inputs": {"width": width, "height": height, "batch_size": 1}},
        "8": {"class_type": "KSampler",
              "inputs": {"model": ["4", 0], "seed": seed, "steps": steps,
                         "cfg": cfg, "sampler_name": "res_multistep",
                         "scheduler": "simple", "positive": ["5", 0],
                         "negative": ["6", 0], "latent_image": ["7", 0],
                         "denoise": 1.0}},
        "9": {"class_type": "VAEDecode",
              "inputs": {"samples": ["8", 0], "vae": ["3", 0]}},
        "10": {"class_type": "SaveImage",
               "inputs": {"images": ["9", 0], "filename_prefix": prefix}},
    }


def wait_history(prompt_id, timeout_s):
    deadline = time.time() + timeout_s
    while time.time() < deadline:
        h = get_json("/history/" + prompt_id)
        if prompt_id in h:
            entry = h[prompt_id]
            status = entry.get("status", {})
            if status.get("completed") or entry.get("outputs"):
                return entry
            if status.get("status_str") == "error":
                msgs = status.get("messages", [])
                raise RuntimeError("执行失败: %s" % json.dumps(msgs[-3:], ensure_ascii=False)[:1200])
        time.sleep(2)
    raise TimeoutError("等待超过 %d 秒" % timeout_s)


def download(entry, dst):
    images = []
    for node_out in entry.get("outputs", {}).values():
        images += node_out.get("images", [])
    if not images:
        raise RuntimeError("history 里没有图片输出")
    img = images[0]
    q = urllib.parse.urlencode({
        "filename": img["filename"],
        "subfolder": img.get("subfolder", ""),
        "type": img.get("type", "output"),
    })
    with urllib.request.urlopen(BASE + "/view?" + q, timeout=60) as r:
        data = r.read()
    io.open(dst, "wb").write(data)
    return img["filename"], len(data)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--test", action="store_true", help="只跑第一张")
    ap.add_argument("--only", default="", help="只跑指定名字，逗号分隔")
    ap.add_argument("--raw-prompt", action="store_true", help="不做 Z-Image 平面视图改写")
    ap.add_argument("--suffix", default="", help="输出文件后缀，用于保留多组对比结果")
    ap.add_argument("--width", type=int, default=768)
    ap.add_argument("--height", type=int, default=1152)
    ap.add_argument("--steps", type=int, default=25)
    ap.add_argument("--cfg", type=float, default=4.0)
    ap.add_argument("--timeout", type=int, default=600, help="单张最长等待秒数")
    args = ap.parse_args()

    os.makedirs(OUT, exist_ok=True)
    prompts, negative = load_prompts()
    if args.only:
        targets = [n.strip() for n in args.only.split(",") if n.strip()]
    elif args.test:
        targets = NAMES[:1]
    else:
        targets = NAMES

    print("服务: %s" % BASE)
    print("模型: z_image-Q4_K_M.gguf + Qwen3-4B-Q4_K_M.gguf + ae.safetensors")
    print("参数: %dx%d  steps=%d cfg=%s  res_multistep/simple  shift=3"
          % (args.width, args.height, args.steps, args.cfg))
    print("负向: %s" % ("文本条件" if args.cfg > 1.0 else "ConditioningZeroOut"))
    print("输出: %s\n" % OUT)

    result = {}
    for i, name in enumerate(targets):
        seed = SEEDS[NAMES.index(name)]
        prompt = prompts[name] if args.raw_prompt else adapt_for_zimage(prompts[name])
        wf = build_workflow(prompt, negative, seed, args.width, args.height,
                            args.steps, args.cfg, "BH_" + name)
        t0 = time.time()
        try:
            resp = post_json("/prompt", {"prompt": wf, "client_id": "betterhandbooks"})
        except urllib.error.HTTPError as e:
            body = e.read().decode("utf-8", "replace")
            raise RuntimeError("提交被拒: HTTP %d\n%s" % (e.code, body[:2000]))
        pid = resp["prompt_id"]
        print("[%s] queued %s ... " % (name, pid[:8]), end="", flush=True)
        entry = wait_history(pid, args.timeout)
        dst = os.path.join(OUT, name + "_raw" + args.suffix + ".png")
        filename, size = download(entry, dst)
        print("done  %.1fs  %s (%d bytes)" % (time.time() - t0, filename, size))
        result[name] = {"seed": seed, "file": filename, "seconds": round(time.time() - t0, 1)}

    meta = os.path.join(OUT, "run_params" + args.suffix + ".json")
    io.open(meta, "w", encoding="utf-8").write(json.dumps({
        "models": ["z_image-Q4_K_M.gguf", "Qwen3-4B-Q4_K_M.gguf", "ae.safetensors"],
        "width": args.width, "height": args.height, "steps": args.steps, "cfg": args.cfg,
        "sampler": "res_multistep", "scheduler": "simple", "shift": 3.0,
        "negative": "text" if args.cfg > 1.0 else "ConditioningZeroOut",
        "zimage_prompt_adaptation": not args.raw_prompt,
        "runs": result,
    }, ensure_ascii=False, indent=2))
    print("\n参数记录: %s" % meta)


if __name__ == "__main__":
    main()
