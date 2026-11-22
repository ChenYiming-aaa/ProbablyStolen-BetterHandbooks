# 发布到创意工坊

面向作者的发布流程。玩家侧说明见 `README.md`。

## 一、打包

```powershell
cd mod\BetterHandbooks
powershell -ExecutionPolicy Bypass -File tools\package.ps1
```

脚本会编译、重建 `dist\BetterHandbooks\`、并跑一遍发布前校验：

> **文案的源头是 `mod\BetterHandbooks\workshop\description.*.txt`**，`package.ps1` 每次都把它拷到 `dist\` 根目录。
> 直接改 `dist\description.zh.txt` 会在下次打包时被原样盖回去——改文案一律改 `workshop\` 那份。

- manifest 必需元素齐全（ID / Name / Author / ModVersion / Description / Assemblies / Capabilities）
- manifest 版本与程序集版本一致
- 包内没有未声明的 DLL（加载器会把未列出的 DLL 当可疑文件上报）
- 三份 CSV 不带 BOM、键名齐全（五本册子 × name/desc/d1..d6，加招牌箱的 name/desc）
- 每张 PNG 的宽高都是 16 的整数倍（物品形状按 16px 块采样）

校验不过会以非零退出。**别跳过这一步**，这些错误在游戏里表现为"没图标/放不进/英文名"，很难现场定位。

## 二、上传

**已发布**：物品 ID `3830606413` — <https://steamcommunity.com/sharedfiles/filedetails/?id=3830606413>
（1.0.5 2026-10-31 首发；1.0.6 2026-11-04，manifest 6352167379813409702；1.1.0 2026-11-06，manifest 431606671356674987；1.2.0 2026-11-16 空/满双外观，一次过没超时；1.6.9 2026-11-19 七件容器 + 万能手册 + 自动归类，manifest 1919391945932352237 后重新提交；1.7.1 2026-11-19 15:52 修复停用再启用手册变问号/内容丢失 + 内容随存档备份自动恢复，一次过没超时，线上描述仍中文版；1.7.2 2026-11-19 17:52 新增开关可关闭自动整理（名片等留档案箱做装饰），一次过，file_size 96784；1.7.4 2026-11-20 00:42 招牌箱收其他 mod 招牌 + 移除内容备份功能，manifest 6500940436627612487，file_size 91152；2026-11-21 12:46 描述改为中英双语精简版 3997 字节（代码未变、file_size 仍 91152）；1.7.6 2026-11-21 13:18 修复读档卡住与文件被吞（读档期自动整理暂停）+ 自动整理默认关闭，manifest 2437153778614014254，file_size 91664，含双语描述；1.7.7 两阶段扫描提速版本地实测出现"清单全是英文 id"，已撤回未上线））

**工坊描述：中英双语精简版（2026-11-21 起）**：Steam 描述字段有约 8000 **字节**上限（不是字符）。早期中英合并版 8626 字节超限，提交报 `k_EResultInvalidParam`（内容能传到 100%、preview 也能传，但最终 commit 失败，Steam 日志写 `Upload finished ... Invalid Parameter` 并 `Reverting to previous content`），所以一度只传中文版 4018 字节，英文玩家反馈看不到说明。2026-11-21 把两语各自精简后合并成 `workshop\description.bilingual.txt`，**3997 字节**，中英齐全、余量充足，已上线。文案源头是 `workshop\description.bilingual.txt`，上传时拷成 `bh_package\description.txt`；旧的 `description.zh.txt`（4018B）/ `description.en.txt`（4647B）仍留在源头与 `dist\` 作存档，必要时可换用单语言版。

游戏本体没有内置上传入口（`ModWorkshop` 只做订阅与下载），实际走的是 Steamworks API —— 复用已登录的 Steam 客户端，不需要账号密码。上传器工程在 `D:\ps_upload_tmp\uploader_bh`（与本项目其它 mod 共用同一套写法）：

```bash
cd /d/ps_upload_tmp/uploader_bh
./bin/Release/net8.0/uploader_bh.exe public    # private 则先发成私密
```

它读取 `D:\ps_upload_tmp\bh_package\`（内容目录 + preview.png + description.txt + publishedfileid.txt），首次运行创建物品并写回 ID，之后同一目录再运行就是「更新」。

`bh_package` 放在纯英文路径是有意的：Steamworks 传内容目录时，中文路径（「mod制作」）容易在 manifest 里出问题。

### 备选方案

- **steamcmd**：`steamcmd +login <账号> +workshop_build_item "...\dist\workshop_build.vdf" +quit`（需要账号密码 + Steam Guard，本机没缓存登录，所以没采用）
- **Steam 客户端**：库 → Probably Stolen → 创意工坊 → 「创建物品」，内容目录选 `dist\BetterHandbooks`，预览图选 `dist\workshop_preview.png`，描述粘贴 `dist\description.zh.txt`。

## 三、上传后自检

1. 订阅自己的条目，等 Steam 下载到 `steamapps\workshop\content\4348910\<id>\`。
2. 关掉本地 `Mods\BetterHandbooks`（**必须挪走**，否则两个同名 ID 会互相遮蔽，日志里出现 `shadowed`）。
3. 启动游戏 → 模组列表应显示「更好的手册 / Better Handbooks」、作者、版本、来源 Workshop。
4. 读档 → 睡一觉 → 看柜台/客人队列。
5. 日志搜 `[BETTERHB]`：

```
[BETTERHB] all factories registered: True
[BETTERHB] day 26 scan: items=196 lottery=15 recipe=1 card=1
[BETTERHB] invited production_book on day 26
[BETTERHB] delivered production_book (生产手册)
```

同时搜 `[ModLoader] audit: better.handbooks`，确认 `caps=none`、`source=Workshop`、`trust=Match`。

## 四、更新版本

1. 改 `manifest.xml` 的 `<ModVersion>` 与 `BetterHandbooks.csproj` 的 `<Version>`（两处必须一致，脚本会校验）。
2. `CHANGELOG.md` 加一段。
3. 跑 `package.ps1`，然后同步内容到 `D:\ps_upload_tmp\bh_package\BetterHandbooks\`（以及 `description.txt` / `preview.png`）。
4. 改上传器的 `ChangeNote`，重新运行 `uploader_bh.exe public` —— 物品 ID 已在 `bh_package\publishedfileid.txt`，同一条目被更新。

## 五、已知坑

| 现象 | 原因 |
|---|---|
| 物品名叫英文、图标是问号 | `Localization/zh.csv` 没带（游戏上报的语言代码是 `zh`，不是 `zh-Hans`） |
| 玩家被拒绝启用，提示 "uses capabilities its manifest does not declare" | manifest 声明的能力少于加载器扫描结果。以 `[ModLoader] audit` 的 `caps=` 为准 |
| CSV 第一列读成 `?key` | 文件带 UTF-8 BOM。`package.ps1` 会检查 |
| 图标在格子里错位 | PNG 宽高不是 16 的倍数 |
| 订阅后和本地版同时存在 | 两个同 ID 模组，必须删掉本地那份 |
| `上传失败: EResult=k_EResultFail`，日志写 `Timeout uploading manifest` | Steam 客户端到工坊上传接口的连接超时，不是包的问题（2~3 KB 的 manifest 也会超时）。**直接重跑**，实测第 2~3 次就过 |
| `上传失败: EResult=k_EResultInvalidParam`，内容已传到 100%、日志写 `Upload finished ... Invalid Parameter` + `Reverting to previous content` | **描述字段超字节上限**（约 8000 字节）。中英合并版超限、单语言版能过。换用单语言描述即可，不要用中英合订本。ChangeNote 过长也会触发，保持 100 字节左右 |

## 六、产物清单

`dist\` 下每次打包会生成：

| 文件 | 用途 |
|---|---|
| `BetterHandbooks\` | 上传给创意工坊的内容目录（**12 个文件**、约 56 KB，只含游戏要读的东西） |
| `BetterHandbooks-1.1.0.zip` | 手动安装包，解压到 `Mods\` 即可（额外带 README / CHANGELOG / LICENSE） |
| `workshop_preview.png` | 工坊预览图，1024×1024 |
| `description.zh.txt` / `description.en.txt` | 工坊简介（Steam BBCode），中文/英文 |
| `workshop_build.vdf` | steamcmd 上传配置，`publishedfileid` 会自动沿用上次的值 |
| `PUBLISH.md` | 本文件 |

上传目录只有运行期文件：`manifest.xml`、`BetterHandbooks.dll`、`Localization\*.csv`、`Textures\Items\*.png`。
README / CHANGELOG / LICENSE 不打进上传目录（订阅者不需要，游戏也不读），只进手动安装包。`Mods\` 里多这几份无所谓。

**路径里的中文**：`workshop_build.vdf` 里的路径含「mod制作」。如果 steamcmd 报找不到目录或乱码，
把整个 `dist` 复制到纯英文路径（例如 `C:\ps-workshop\`）再上传，或改用 Steam 客户端方式上传。
