# 更好的手册 / Better Handbooks

Probably Stolen 的原生 mod。柜台上、抽屉里堆得越来越多的是纸：鉴定指南、配方书、彩票、名片。
游戏给了一本导师手册，其余全散着。这个 mod 补上能收它们的东西。

## 七件收纳工具

| 收纳 | 物品 id | 容量 | 收什么 | 谁送 | 什么时候送 |
|---|---|---|---|---|---|
| 鉴定手册 | `inspection_book` | 10×10 | 7 张鉴定纸 + 色卡/标识检查器/放大镜 + 四级鉴定证书 | 导师 | 鉴定服务开启后的第二天（老档当天） |
| 彩票手册 | `lottery_book` | 6×6 | `lottery_ticket`（号码票）、`lottery_ticket_old`（旧彩票）、`scratcher`、`scratcher_stack`、`lottery_info` | 彩票代表 | 身上有 1 张彩票的次日 |
| 生产手册 | `production_book` | 10×10 | 5 本配方书（水/水培/化学/金属锭/酒） | 导师 | 身上有 1 本配方书的次日 |
| 电话簿 | `phonebook` | 6×6 | 6 种原版名片（酿酒/采矿/制药/化工/回收/乔）+ identifier 含 card 的文档 | 供应商 | 身上有 1 张名片的次日 |
| 回执手册 | `receipt_book` | 6×6 | 只有 `sec_slip`（治安部回执单） | 治安官 | 身上有 1 张回执的次日 |
| 招牌箱 | `sign_box` | 10×8 | 10 种 `sign_` 霓虹招牌 | **不送** | 博士柜台上卖 1 块 |

赠送规则对所有册子统一：

1. 条件满足的**当天只登记日期**，不排客人；
2. **次日起**才会来，一天最多来一位；
3. 玩家不接待就第二天再来，直到收下；
4. 手上已经有这本册子（老档、上次已收、旧版发过）就直接记"已送"，不重复发。

招牌箱不进这套队列：`BookDef.ShopOnly = true`，`Giver.MaybeInvite` 直接跳过。

**图标占位和内容容量是两件事**：图标决定物品在背包/柜台上占几格（32×48 = 2×3，48×32 = 3×2），
内容容量决定翻开来有几格（`Books.cs` 里的 `Width`/`Height`）。五本册子的图标都是 32×48，
招牌箱是 48×32；内容容量各自独立，且都能在设置里改。

**老存档里已有的册子不会自动改容量**：`Repair` 只在册子缺 `DOCUMENT` 标签时才重建内容窗口，
已存在的窗口保持原样——重建等于把玩家已经放进去的东西删掉。

## 为什么这样分

分类判据全部来自反编译 `Assembly-CSharp`，不靠 identifier 字面猜：

- **鉴定纸** = `BookHelper.InitPaperGuide` + `PaperUIManager.OpenXxxGuide`，共 7 种：
  `cigarette / hand / aug_id / stamp / injector / fuel / ribwich`
- **配方书** = `BookHelper.InitGuide` 或 `InitBookItem` + `MultiPageUIManager`，共 5 种：
  `water / hydroponic / chemistry / ingot_book / wine`
- **名片** = `PaperUIManager.OpenXxxCard`，共 6 种
- 两类纸的**标签完全一样**（都是 `GUIDE_PAPER_TAG`），所以只能按 identifier 分；
  之前把 `fuel_guide`（等离子燃料匣鉴定指南）、`ribwich_guide`（肋明治鉴定指南）当生产类拦掉，就是踩了这个坑。
- 1.3.0 又踩了一次同一个坑：别的内容 mod 的手册也带 `GUIDE_PAPER_TAG`，会被鉴定手册误收。
  这些手册的 ID 来自源码核对（`be_brewing_manual`、`te_cigarette_inspection_guide`、`gse_manual` 等），
  在 `IsAppraisalItem` 里显式排除，改归万能手册。`ThirdPartyManuals` 名单不要按标签猜。
- 1.4.0 把这份名单按**内容**拆成三组，各归各位（ID 全部来自各 mod 源码/反编译核对，非推测）：
  - 生产配方类 `ExternalProductionManuals`（5）：`be_brewing_manual`、`be_brewing_hydroponic_guide`、
    `te_tobacco_manual`、`te_tobacco_hydroponic_guide`、`pcrepair.computer_build_guide`。
    依据是 mod 自带简介——酿酒手册讲配方收录、两本水培手册讲种植与返种、装机手册讲装机交易。
  - 鉴定类 `ExternalAppraisalManuals`（1）：`te_cigarette_inspection_guide`——源码里直接
    `DirectoryMaster.Item("ribwich_guide")` 克隆而来，就是原版鉴定纸。
  - 工具书 `ExternalToolBooks`（2）：`gse_manual`、`gse_catalog`——规则手册与参数目录，无专属册子。
  `ThirdPartyManuals` 仍是三组合集，只供日志与送达判定用。
- 归属规则是**动态**的，由 `Settings.OmniTakes(OmniCategory)` 决定：某类默认勾选＝万能手册收；
  取消勾选＝生产类回生产手册、鉴定类回鉴定手册、工具书没人收。`IsAppraisalItem` /
  生产手册的 `Accept` / `IsOmniDocument` 三处都要读同一个开关，别漏改任何一处。
- 万能手册**不是**"所有 DOCUMENT 通收"：DOCUMENT 里混着合同、证书、档案、委托单，
  塞进册子可能卡交付。规则是"先排掉已归别册的 → 再排掉任务物 → 按大类开关放行"。
  identifier 里带 `contract` / `certificate` / `archive` / `commission` / `order` / `slip` 的一律不收。
- 关总开关只影响**新收**：`IsOmniDocument` 和 `Ready` 都读 `Settings.OmniEnabled`，
  但已经收进任何册子的东西一律不搬迁（玩家选的"保留册子、停止收新"）。
  送达条件 `HasOmniCandidate()` 要求"确实有一本按当前勾选会收的手册"，避免送来一本空用途的书。

## 项目结构

```
Scripts/
  Books.cs        七件收纳的定义、收纳规则、赠送条件（要加一件改这里）
  BookFactory.cs  物品注册 / 老档修补 / 收纳过滤 / 贴图 / 改尺寸
  Giver.cs        剧情客人、对话、发放、防重复
  Shop.cs         招牌箱在博士柜台的上架与"买过不再补"
  Settings.cs     ModConfig 容量项 + 实时改尺寸（1.5.0 起不再持有 bool 开关）
  DocCatalog.cs   1.5.0：扫描全部 DOCUMENT 按来源 mod 分组，动态生成逐本勾选框 + 读勾选状态
  CounterDelivery.cs 1.5.0：勾了「我要万能手册」后，开门时直接放一本到玩家柜台
  ConfigUI.cs     1.6.0：接管本 mod 设置窗口，两标签页（宽高同行 / 万能手册按 mod 分组），反射复用原版 Apply/Reset
  AutoFile.cs     1.6.0：档案箱放入回调，按 专属→万能→留箱 的优先级自动整理 DOCUMENT
  ShipScan.cs     全船物品扫描（船体根节点 + 档案箱 + 背包 + 前后仓，按 uniqueId 去重）
  Grant.cs        进游戏时修补已有册子、补冶金指南标签
  ModData.cs      存档标记（sent_ / ready_ / bought_ / offered_）+ 从旧 mod id 迁移
  Diagnostics.cs  进游戏自检，打印每本册子的七项放置判定
  Patches.cs      Harmony：冶金标签、来源标注、每日客人生成、设置行、成交标记
Localization/    zh / zh-Hans / en（文件名即游戏的 Locale Code）
Textures/Items/  handbook / lottery / production / phonebook / receipt / omni（32×48）、signbox（48×32），各有 *_full
tools/           make_icon.py（册子底版改色 + 徽记）、make_preview.py、package.ps1
```

## 技术要点

- **物品注册**必须走 `ModHook.OnModItemDirectoryInit` → `ModItemDirectory.Add`。原版 `inspection_book` 定义在
  `UnusedDirectory`（MonoBehaviour，场景没挂）里，`DirectoryMaster.Item` 会退回空壳：没标签、没形状、双击无反应。
- **图标尺寸**必须是 `UISettings.gridSize = 16` 的整数倍，形状按 16px 块采样 alpha 生成；32×48 = 2×3 格，48×32 = 3×2 格。
  贴图放 `Textures/Items/<key>.png`，代码用 `SetSpriteAndShapeFromMod` 取，取不到才退回原版封面。
  五本册子 32×48（占 2×3），招牌箱 48×32（占 3×2）。
- **徽记画法**（`tools/make_icon.py`）：先算实心遮罩，再沿外缘统一描 1px 深色边（`stamp()`）。
  几何参数直接画 45° 听筒，无论怎么调都会读成骨头/钩子/箭头——电话簿的听筒是把横放轮廓抠出来整体旋转 45° 再降采样的结果，矩阵已固化在脚本里。
- **收纳过滤**挂在 `grid.mayInventoryAddItemFunc`。注意：**不要**在里面加 `GeneralHelper.IsItemOwned` 判定——
  原版 `AllowOnlyTaggedItemsOr` 没这条，指南纸不保证带 `IS_OWNED_TAG`，加了会导致"什么都放不进去"。
- **放置判定链**：`MayHaveValidInventorySlot` → `CheckIsStateValid` → `CheckChangeParentShapeStateModifier` → `MaxNumAdd`，
  任何一步 false 都是"拖不进"。`Diagnostics.cs` 会把七项断言打出来。
- **来源标注**（tooltip 底部那行蓝字）由 `ModSourceHelper.GetItemModName` 决定，它按"工厂方法所在程序集"反查 mod，
  关标签没用，只能在函数返回处判空。
- **对话富文本**：对话框是 TMP，动作描写要写 `<i>……</i>`，只写星号不会转斜体。

### 容量设置（1.0.6）

- 游戏自带 mod 配置系统，别自己写 json：`ModHelper.GetConfig(modId)` → `ModConfig.Define(key, default, section, label, min, max, tooltip)`，
  返回的 `ConfigEntry<int>` 有 `.Value` / `.Set()` / `OnChanged` 事件，落在 `%userprofile%\AppData\LocalLow\...\ModConfigs\<modId>.json`。
  模组列表里选中有条目的 mod 会出现「设置」入口（`ModMenu.OpenSelectedModSettings` → `ModConfigWindow.Open`）。
- **改容量**用 `GameGridInventory.SetShape(width, height)`，游戏自己也是这么扩容的（售货机 `SetShape(8, cap)`、水箱 `SetShape(8+lvl, 4+lvl)`）。
  它只重建 `inventoryShape` 并 `Validate()`，不动里面的物品——所以**放大无条件安全，缩小要自己检查**。
- 缩小前的检查：遍历 `grid.childItems`，每件 `modifiedShape` 的 `minX/minY >= 0` 且 `maxX < width`、`maxY < height`，全过才 `SetShape`。
  否则保持原尺寸并打一行 `keep <id> at WxH`。
- 设置项必须在 `OnEnable` 里登记（列表构建前），且 `Define` 是幂等的，重复调用只覆盖。
- **bool 设置项**：`ModConfig.Define(key, bool, section, Func<string> label, Func<string> tooltip)`，
  `label`/`tooltip` 延迟求值（窗口打开时才取，否则英文界面会钉成中文）。`ConfigEntry<bool>.Value` 即时、`Set()` 触发 `OnChanged`。
- **1.5.0 万能手册的逐本勾选是"运行时动态追加"的例外**：设置窗口 `ModConfigWindow.Open` 是打开时才遍历
  `config.Sections` / `config.Entries` 构建的，所以可以在扫描到物品之后
  再 `config.Define("docsel_<identifier>", false, section, label, tooltip)` 追加任意多条——必须先扫到物品才知道有哪些手册。
  section 是固定字符串（不支持延迟求值），所以用 `BookFactory.Text()` 当场取当前语言。
- **1.6.1 扫描时机修正**：1.5.0 只在 `OnGameLoadedLate` 扫，但设置窗口在主菜单就能打开，玩家会看到空清单和消失的
  「我要万能手册」开关（窗口半透明，背后模组列表透过来，看起来像乱码条目）。现在 `ConfigUI.Build` 开头先扫一次，
  读档后 `OnLoaded` 再 `ScanAndDefine(true)` 强制重扫补漏；`definedKeys` 保证重复扫描不会重复 `Define`，
  扫到 0 条时不锁定 `scanned`，允许下次重试。`Debug.Log` 里带 `locked=` 便于确认是哪一轮扫描。
- **枚举全部物品**：`DirectoryMaster.GetIdentifierList<GameItem>()` 列出所有已注册的 identifier（含所有 mod）。
  要读每个的名字/类型，用 `DirectoryMaster.GetFactory<GameItem>(id)()` 造一个"探针"——它只 new 实例 + 设 identifier，
  **不消耗 uniqueId、不入存档**，读完 `GetDisplayName()` / `IsGameItemType("DOCUMENT")` 后 `Destroy()` 掉。
  不要用 `DirectoryMaster.Item(id)`，那个会消耗 uniqueId、触发发放回调、污染存档。
- **按来源 mod 分组**：`ModSourceHelper.GetItemModName(item)` 读 `MOD_ID_TAG` / `MOD_NAME_TAG`；原版物品返回 null → 归"游戏原版"组。
  少数不打来源标记的 mod 退而按 identifier 前缀分组。
- **一本手册可被两本册子同时认领**（1.6.0 起）：曾有"让位机制"（专属册子先判 `!DocCatalog.IsSelected(id)`），
  结果勾给万能手册后专属册子拒收，玩家反而哪本都进不去。已彻底移除——`Books.All` 的 `Accept` 不再读勾选状态，
  勾选只决定万能手册肯不肯收，不影响专属册子。实际进哪本由拖放目标 / 自动整理优先级决定。
- **开门直放柜台**：`CounterDelivery` 挂 `ModHook.OnShutterOpenedLate`，勾了 `want_omni` 且店里没有就
  `DirectoryMaster.Item(omni_book)` + `PlayerStore.AddDirectSellingItemToTable(book, true)`，柜台满则 `GraphUtils.EmporiumTryAdd`。
  复用 Giver 的同一套放置逻辑（含放不下转店内容器）。
- **档案箱自动整理**（`AutoFile.cs`）：档案箱 id = `dossier`，由 `MiscItemDirectory.Dossier()` 造，工厂里用 `=` 赋了
  `gridInventory.onSlotAddItemEndFunc`（教程提示 + 换图）。**不能覆盖**，用 Harmony patch 工厂的 Postfix +
  `Delegate.Combine` 追加我们自己的回调。该回调触发时（`GameGridInventory.UncheckedAccept` 里 items.Add + AddParent 之后）
  物品已在格子里、可安全移走。移走用 `GraphUtils.TryAcceptAll(册子, item, -1)`（和导师笔记自动收纳同一条原生路径），
  移之前先 `MayHaveValidInventorySlot` + `TryFindOneValidInventorySlot`（numTransfer > 0）预判。
  只在档案箱的格子上挂回调 → 移进手册不会递归。老档的档案箱在 OnEnable 前造好、错过工厂 Postfix，
  用 `EmporiumEntry.Instance.dossier` 在 `OnLoaded` 里补挂（`AttachExisting`）。
- **1.6.2 双通道触发**：1.6.0 只靠 `onSlotAddItemEndFunc`，日志证实挂上了却一次都没触发
  （`hooked dossier grids=1` 之后没有任何 `autofile ...`）。档案箱换实例或原生重新赋值都会让它失效。
  现在保留回调，并 patch `GameGridInventory.UncheckedAccept` 的 Postfix，用 `GraphUtils.FindParentType<GameItem>(grid)`
  取拥有者、只认 `identifier == "dossier"`；同帧同 uniqueId 去重，两个入口不会重复整理。
  入口和每一步失败都打日志（`autofile hit via=...` / `skip ...` / `move ... moved=N`），便于定位。
  档案箱工厂里的 `mayInventoryRemoveItemFunc = false` 只挡玩家手动拖出，`GameGridInventory.Expel` 不查它，
  所以 `TryAcceptAll` 的自动移出不受影响。
- **1.6.3 批量入口分流**：批量放进档案箱走 `GraphUtils.TryAcceptAll(GraphNodeStorage, GameItem, int amount)`，
  目标可能是 `dossier` 本体（GameItem 实现 GraphNodeStorage）或它的 GameGridInventory，不能只等落箱后的回调。
  patch 该方法 Prefix：`IsDossierNode` 认这两种目标，`FileIntoBooks(item, want)` 按 专属→万能 顺序跨册子搬，
  返回实际移动量（`GraphUtils.TryAcceptAll` 返回的是 `amount - num`，即剩余未放量，别再写反）。
  全部分流走时 `__result = 0` 并返回 false 跳过原版放入；部分分流时修正 `ref int amount` 后放行，
  剩余部分仍按原版尝试进档案箱。`MarkRedirected` 用 frame + uniqueId 抑制后续回调重复整理。
  回调路径也改用同一个 `FileIntoBooks`，优先级完全一致。
- **1.6.4 单件拖放**：单件不经过 `GraphUtils.TryAcceptAll`，而是 `ItemMouseDragHandler.EndDrag`
  调 `lastInventory.TryInventorySlot(currentItem, currentRemove, ...).TryAcceptOnce()`；
  档案箱没空位时 `lastSlot` 为 null，之前的落箱后钩子全不触发。patch `EndDrag` 的 Prefix，
  用反射读私有字段（`currentItem` / `lastInventory` / `lastItem` / `currentRemove`），
  落点是档案箱格子或 `lastItem.identifier == "dossier"` 时先 `FileIntoBooks`。
  全部分流走时把 `currentRemove` 设为 0，`TryInventorySlot` 算出的 `numTransfer = 0`，
  原版 `TryAcceptOnce` 空转，不会把物品从册子拽回档案箱。注意本项目引用的 0Harmony 版本
  没有 `Traverse.Field().Value<T>()`。
- **1.6.5 拖拽提示**：红字「无法将X放入Y！」来自 `RichTextGenerator.InsertItemInOtherItem`
  的 `mech_drag_cant_insert`，由 `ItemMouseDragHandler` 悬停时按目标 `MayHaveValidInventorySlot`
  / `TryFindOneValidInventorySlot` 传入 `canInsert`。档案箱满时它报 false，但我们会在 EndDrag 前分流。
  patch 该方法，目标为 dossier 且 `AutoFile.HasFileTarget(item)` 为真时改成肯定句。
  `HasFileTarget` 只读判定（册子存在 / 可插 / 有槽），不移动物品。
- **1.6.7 mod 扫描器感叹号**：模组列表的橙色感叹号提示 `reflection-external` 能力，来自
  `Patch_SingleDragFile` 里 `target.GetType().GetField(name, ...)`——`System.Type.GetField` 在扫描器的
  `TypeMemberLookups` 里，接收者是运行时计算的 Type（`ClassifyReceiver` 判为 external）就报警。
  改用 `AccessTools.Field(typeof(ItemMouseDragHandler), name)`：Harmony 类型走扫描器特判分支，
  第一个参数是字面 typeof（`ItemMouseDragHandler` 属 `Assembly-CSharp`，`IsExternalScope` 返回 false），
  字段名是常量，两条件都不触发。**凡是要在游戏程序集上反射私有字段，一律用字面 typeof + AccessTools，
  不要用 `instance.GetType()`。**
- **1.6.8 红色高亮（此前 1.6.5~1.6.7 一直改错对象）**：用户说的"背后是红的"是**档案箱被整块涂红的
  高亮**，不是 tooltip 窗口外框。真实来源：`ItemMouseDragHandler` 悬停时按 `lastSlot.numTransfer<=0`
  调 `highlightManager.Try(lastSlot, Red)`（`RefreshDragHighlight` 与 drag 更新里各一处）。
  `HighlightManager.Try` → `slot.inventory.HighlightSlot(slot, color)` → `GameGridInventory.HighlightSlot`
  用 `BackgroundType.Border` 画高亮。修法：patch `HighlightManager.Try` 的 Prefix，当 `color==Red`、
  目标是档案箱（`IsDossierNode(slot.item) || IsDossierNode(slot.inventory)`——1.6.6 只判 `slot.item`，
  悬停在格子上时 `item=null` 而漏判）、且 `HasFileTarget(dragged)` 为真时，把 `ref color` 改成 Green；
  拖拽物用公开的 `ItemMouseDragHandler.current.GetCurrentItem()`（字面类型，不触发反射扫描）。
  同时 `Patch_DragTooltip` 把 1.6.7 的暖色 workaround 撤销、文字恢复 `ColorPalette.Green`。
  **教训**：截图里的"红"要先分清是文字色、tooltip 窗口外框（图集固定，改不了）、还是目标高亮；
  用户描述"背后"通常指目标高亮，不是浮层边框。
- **1.6.9 红色高亮仍未消失**：1.6.8 patch `HighlightManager.Try` 实测无一行日志命中——满仓时
  `MayHaveValidInventorySlot` 只看物品类型返回 true，但 `TryFindOneValidInventorySlot` 返回 null，
  于是走 `ItemMouseDragHandler` 悬停逻辑的**第一个分支**
  `parentInventory.HighlightSlot(incompleteSlotInInventory, Red, doReplace:true)` 直接调用，
  根本不经过 `HighlightManager.Try`（后者只在"完全不可插入"的 `else if` 分支被调用）。
  正确挂点：`GameInventory.HighlightSlot` 的各 override——两条高亮路径（直接调用 + `Try` 内部）都汇入它。
  对 `GameGridInventory` / `GameGridScrollableInventory` / `GameSlotInventory` 三种网格都 patch，
  `DossierHighlightFix.Green(slot, ref color)` 统一判断（Red + 归属 dossier + HasFileTarget）后改绿。
  **教训**：一个视觉元素常有多条绘制调用路径，patch 前必须确认实际走的那条；用日志命中数验证，
  没命中就说明挂点错了，别在错误挂点上继续加逻辑。
- **设置界面两标签页**（`ConfigUI.cs`）：Harmony patch `ModConfigWindow.Open` 的 Prefix，只对 `better.handbooks` 接管，
  其余 mod 放行返回 true。自建窗口复刻原版结构（CreateWindow / 应用 / 重置 / 提示行），中间换成 `BeginTabs`。
  容量控件 id 沿用原版约定：滑块 = `entry.Key`、输入框 = `entry.Key + "_input"`，勾选 = `entry.Key`——
  这样反射调用的原版私有 `Apply` / `ResetWidgets` 能照常按 id 读写。隐藏标签页的控件 `IsAlive` 仍为 true
  （只看 root != null），`SetActive(false)` 不影响应用时读值。自建失败则回退原版，不让设置打不开。

### 名片为什么没有标签

原版纸张标签只有三种：`GUIDE_PAPER_TAG`（鉴定纸，连 `logo_checker` 都带）、`LOTTERY_PAPER_TAG`、`TUTORIAL_PAPER_TAG`。
名片（`CardBrewer` 等）只是 `DOCUMENT` + `IMPORTANT_TAG` + 双击回调，**没有任何"名片"标签**，只能按 identifier 认。
所以别的 mod 加名片时，除非它主动打标签，我们无法可靠识别。当前策略：原版六张白名单 + `BETTERHB_CARD_TAG` + identifier 含 `card` 的 `DOCUMENT`。

### 回执手册（1.1.0）

- `sec_slip` 由 `DirectoryMaster.SecSlip()` 造：`DOCUMENT` + `onActivateSlotItemFunc → PaperUIManager.OpenCard("sec_slip", item)`，
  **没有专属标签**，只能 `identifier == "sec_slip"`。
- 另外两种单据故意不收：`slip`（通用手写单据，内容动态）、`procurement_slip`（采购回执，到期自己消失）——都是任务中间物。
- 赠送人用原版治安官：`LocHelper.GetLocalizedClientName("name_security_officer")` + `SpriteDict.GetRandomSecSpriteName()`。
  **不要**调 `StoreClientFactionSetup.InitSecurity(client)`：它会设 `isSecurity = true`，游戏会把这位客人当治安人员走搜查/登记那套。
- 统计类台词能用的真数据只有两项，**没有"成功举报次数"字段**：
  - `PlayerStore.wantedStatus`（`Dictionary<string,int>`）：举报成功并归案时 `wantedStatus[wantedPosterId] = 1`，
    累计且不回退，是唯一可靠的正向指标。
  - `PlayerStore.wrongfulReportCount`：当前挂着的错报次数，`wrongfulReportMax = 5`，**每 5 天自动 -1**（`dayCounter % 5 == 0` 那段），
    所以它表示"近期错误"，不是历史总数。
- **`sec_slip` 不能当成功次数用**：上报流程里 Correct 和 Wrongful 两个分支都会 `DirectoryMaster.Item("sec_slip")`，
  错报也会给你一张盖了章的单据。按手上回执张数算正确率是错的。
- 累计赏金只存在于回执的 `body` 文本里（`mech_report_wanted_body` 把 reward 和真名拼进去），
  要正则抓，中英格式还不一样——不值得，放弃。
- 台词分三档（`rcp_g` / `rcp_m` / `rcp_b`，各 5 句），判据：
  `wrongfulReportCount >= 2` → 差；`== 0` 且已归案 ≥ 3 → 好；其余 → 中。
  `WantedClosed() == 0` 时第 2 句换成 `<key>_d2_zero`，避免"你举报了 0 个"。
- 占位符在运行时替换（`Books.FillNumbers`）：`{0}` 回执张数、`{1}` 错报次数、`{2}` 已归案数。
  游戏自己的本地化格式化只对 `LocHelper` 那套生效，`ModHelper.GetLocalized` 拿到的是原始串。

### 招牌箱为什么是"箱子"但不是 STORAGE（1.1.0）

- 10 种 `sign_*` 全部只带 `IMPORTANT_TAG`，其中 9 种还有 `SHOWCASE_SPECIAL_ITEM_TAG`（展示柜用），
  但 `sign_berry` 连物品类型都没设 → **按标签收必漏**，只能按 id 前缀 `sign_`。
- 箱体沿用册子那套 `DOCUMENT` 容器（能进档案箱、双击打开、走同一套过滤），只是贴图换成玻璃箱。
  做成 `STORAGE` 要另验一遍"双击能否打开 / 档案箱是否接受"，收益不抵风险。
- 上架走 `ModHook.OnPlaceInventorInventoryItemEarly`（`PlaceInventorInventory` 开头触发，参数是玩家已拥有物品清单）。
  用 `PlayerStore.TryAddDirectSellingItemToTable(id)` 摆上柜台，`unitValue = 1`。
- **"买过一次永久不补"靠两层**：成交时 `PlayerStore.OnItemBought` 的 Harmony postfix 立刻写 `bought_sign_box`；
  上架前再看一眼清单里有没有（兜底，防止玩家买完立刻扔掉、又被当成"没买过"）。
- 同一晚只摆一个：`offered_sign_box` 记日期，反复进出博士的店不会刷出一排。离开时原版会 `ClearFrontInv`，
  未售出的箱子当晚销毁，不会累积。
- **空/满两张图**：`BookDef.SpriteKeyFilled`（只有招牌箱填了）。切换靠 `BookFactory.RefreshFilled`，
  挂在 **`GameGridInventory.UncheckedAccept`**（放入）和 **`GameGridInventory.Expel`**（取出）两处 postfix 上。
  用 `uniqueId -> bool` 缓存，状态没变就不重设贴图（否则拖动时每帧重建形状）。
  两张 PNG 必须用**同一个裁切框和缩放参数**生成（`_preview` 里的 `BOX=(96,208,928,816)` → 48×32），
  否则切换时箱体会跳位。
- **踩过的坑：`GameInventory.TryAcceptAll` 不是拖放的入口**。第一版打在那儿，日志里 `sign_box accepts ... True`
  照常出现、`skin ->` 一次都没有。真正的链路是
  `GraphUtils.TryAcceptAll` → `SlotMarker.TryAcceptOnce` → `GameGridInventory.UncheckedAccept`。
  `TryAcceptAll` / `UncheckedAcceptAll` 是给代码内部"整批塞东西"用的，玩家拖放不经过。

## 部署

```
.\build.ps1              # 编译 + 部署 + 哈希校验
```

游戏运行时 dll 被内存映射锁住，脚本会明确提示并退出；必须完全退出游戏再装。

## 排错

`Player.log` 里搜 `[BETTERHB]`：

```
[BETTERHB] all factories registered: True      # 四本册子的工厂都注册上了
[BETTERHB] repaired 2 handbook(s)              # 进游戏时修补了几本
[BETTERHB] invited lottery_book on day 15       # 今天排了谁
[BETTERHB] delivered lottery_book (彩票手册)      # 东西放下柜台了
[BETTERHB] lottery_book accepts water_guide? False   # 收纳判定（每种物品只记第一次）
[BETTERHB] diag lottery_book owned=True ... slotValid=True
```

`slotValid=False` 说明某本册子收不了东西，`diag` 那行的 `accept/may/state/shape/maxAdd` 会指出卡在哪一步。

## 卸载

删掉 `Mods/BetterHandbooks/`。存档里多出的册子会变成普通容器物品，里面的东西不会丢。

## 旧版

0.1–0.7.7 的《鉴定手册》（mod id `inspection.handbook`，目录 `Mods/HandbookFix`）已并入本 mod。
旧的 `mentor_sent` 标记会自动迁移，不会重复发书。旧目录请删除，避免两个 mod 同时注册 `inspection_book`。
