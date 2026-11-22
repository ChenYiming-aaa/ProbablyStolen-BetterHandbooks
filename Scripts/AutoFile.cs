using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BetterHandbooks
{
    /// <summary>
    /// 自动整理（1.6.0）：玩家把文档放进原版档案箱（dossier）后，立刻按优先级
    /// 自动塞进对应的册子；塞不进的留在档案箱原地。
    ///
    /// 优先级：专属手册（鉴定/彩票/生产/电话簿/回执/招牌箱）→ 万能手册 → 留在档案箱。
    /// 只有"店里已经有的"册子才参与（没拿到那本册子就无从收起）。
    ///
    /// 挂点选择：不覆盖档案箱工厂里用 = 赋的原生回调（教程提示/换图），
    /// 而是在 Dossier() 工厂的 Postfix 里用 Delegate.Combine **追加**一个
    /// onSlotAddItemEndFunc。这个回调触发时物品已经落进档案箱格子、parentInventory 已设好，
    /// 此时用 GraphUtils.TryAcceptAll 把它移进手册是游戏自己拖放用的同一条路径。
    ///
    /// 不会递归：只在档案箱的格子上挂回调，手册自己的格子不挂。
    /// </summary>
    internal static class AutoFile
    {
        // 同一个档案箱实例可能因为重载/多次工厂调用被挂多次，按实例引用去重。
        private static readonly HashSet<GameGridInventory> hookedGrids = new HashSet<GameGridInventory>();
        // 回调和 UncheckedAccept 补丁可能同一帧都命中同一件物品，只处理一次。
        private static int lastHandledFrame = -1;
        private static int lastHandledUid = -1;
        // 批量入口已经分流过的物品，落进档案箱后不要再被回调重复整理。
        private static int lastRedirectFrame = -1;
        private static int lastRedirectUid = -1;

        // 1.7.5：读档期不整理。游戏读档时用 GameGridInventory.UncheckedAccept 逐件重建档案箱
        // 内容（反编译 SaveManager.DecodeNodesInternal:222938 / PlayerStore.DecodeSaveItem:95818），
        // 会连带触发我们的 Patch_GridAccept → OnDossierAdd。读档主线程里对每件文档做全船扫描
        // ×7 册 + 嵌套放入，文档多时把加载卡住；更糟的是 TryAcceptOnce 先 Expel 后放入，
        // 失败时物品变成 parentInventory==null 后被游戏销毁 → 玩家看到"文件被吞、手册里也没有"。
        // 用 PlayerStore.LoadGame 的 Prefix/Postfix/Finalizer 框住整个读档过程，期间一律放行。
        private static bool loading;

        internal static void BeginLoad()
        {
            loading = true;
        }

        internal static void EndLoad()
        {
            loading = false;
        }

        /// <summary>读档重建期间不整理，交回原版行为。</summary>
        private static bool IsLoading()
        {
            return loading;
        }

        /// <summary>工厂 Postfix：给新造出来的档案箱追加放入整理回调。</summary>
        internal static void Attach(GameItem dossier)
        {
            if (dossier == null || dossier.identifier != "dossier")
            {
                return;
            }
            try
            {
                List<GameInventory> invs = GraphUtils.FindChildrenType<GameInventory>(dossier);
                if (invs == null)
                {
                    return;
                }
                for (int i = 0; i < invs.Count; i++)
                {
                    GameGridInventory grid = invs[i] as GameGridInventory;
                    if (grid == null || !hookedGrids.Add(grid))
                    {
                        continue;
                    }
                    grid.onSlotAddItemEndFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(
                        grid.onSlotAddItemEndFunc,
                        new Action<GameItem, GameInventory, SlotMarker>((item, inv, slot) => OnDossierAdd(item, "callback:" + grid.inventoryShape.width + "x" + grid.inventoryShape.height)));
                }
                Debug.Log("[BETTERHB] autofile: hooked dossier grids=" + invs.Count);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] autofile hook failed: " + e.Message);
            }
        }

        /// <summary>老档补救：档案箱在 OnEnable 前就已造好，工厂 Postfix 没赶上时这里补挂。</summary>
        internal static void AttachExisting()
        {
            try
            {
                EmporiumEntry entry = EmporiumEntry.Instance;
                if (entry == null)
                {
                    return;
                }
                Attach(entry.dossier);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] autofile attach existing failed: " + e.Message);
            }
        }

        /// <summary>档案箱放入的入口。via 只用于日志，区分是回调还是 UncheckedAccept 补丁进来的。</summary>
        private static void OnDossierAdd(GameItem item, string via)
        {
            if (item == null)
            {
                return;
            }
            // 1.7.5：读档重建期不整理——见 AutoFile.loading 的说明。
            if (IsLoading())
            {
                return;
            }
            // 玩家在设置里关掉自动整理：完全不干预，东西留在档案箱原地做装饰。
            if (!DocCatalog.AutoFileEnabled())
            {
                return;
            }
            // 两个入口同一帧都命中时只处理一次。
            int frame = Time.frameCount;
            int uid = item.uniqueId;
            if (frame == lastHandledFrame && uid == lastHandledUid)
            {
                return;
            }
            lastHandledFrame = frame;
            lastHandledUid = uid;

            try
            {
                string id = item.identifier;
                // 批量入口已经把这些单位分流出去了，剩下的只是放不进册子的部分，别再试一遍。
                if (frame == lastRedirectFrame && uid == lastRedirectUid)
                {
                    return;
                }
                // 日志放在最前面：哪怕被过滤掉，也能看清回调到底有没有、因什么被挡。
                Debug.Log("[BETTERHB] autofile hit via=" + via + " item=" + id
                          + " docTag=" + item.IsTag("DOCUMENT") + " docType=" + item.IsGameItemType("DOCUMENT"));
                if (!IsEligibleDocument(item))
                {
                    return;
                }

                int moved = FileIntoBooks(item, -1);
                if (moved > 0)
                {
                    Debug.Log("[BETTERHB] autofile " + id + " filed " + moved + " unit(s) from dossier");
                }
                // 还是留在档案箱里的（册子收不下/不收），保持原状。
                if (item != null && item.unitCount > 0)
                {
                    Debug.Log("[BETTERHB] autofile " + id + " stays in dossier");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] autofile on add failed: " + e.Message);
            }
        }

        /// <summary>
        /// 兜底入口：patch GameGridInventory.UncheckedAccept，认"拥有者是 dossier 的格子"。
        /// 回调路径可能因档案箱换实例或原生代码重新赋值 onSlotAddItemEndFunc 而失效，
        /// 这里从更底层的放入路径再兜一次。
        /// </summary>
        internal static void OnGridAccept(GameGridInventory grid, GameItem item)
        {
            if (grid == null || item == null)
            {
                return;
            }
            // 1.7.5：读档期 UncheckedAccept 重建档案箱是最主要的触发源，这里必须先挡。
            if (IsLoading())
            {
                return;
            }
            try
            {
                GameItem owner = GraphUtils.FindParentType<GameItem>(grid);
                if (owner == null || owner.identifier != "dossier")
                {
                    return;
                }
                OnDossierAdd(item, "grid");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] autofile grid probe failed: " + e.Message);
            }
        }

        /// <summary>哪些文档参与自动整理：排除本 mod 自己的册子和任务关键文档。</summary>
        internal static bool IsEligibleDocument(GameItem item)
        {
            if (item == null)
            {
                return false;
            }
            if (!item.IsTag("DOCUMENT") && !item.IsGameItemType("DOCUMENT"))
            {
                return false;
            }
            string id = item.identifier;
            return !string.IsNullOrEmpty(id) && !Books.IsOurs(id) && !Books.IsTaskCriticalId(id);
        }

        /// <summary>复用册子自己的收纳规则判定。接受异常（诊断补丁里也可能抛）当"不收"。</summary>
        private static bool Accepts(BookDef def, GameItem item)
        {
            try
            {
                return def.Accept(item);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 按 专属册子 → 万能手册 的顺序把 item 的单位塞进店里已有的册子。
        /// amount=-1 表示搬走当前剩下的全部，正数表示最多搬这么多；返回实际搬走的数量。
        /// 一本塞不下就继续试下一本，所以批量放入不受单个册子容量限制。
        /// </summary>
        internal static int FileIntoBooks(GameItem item, int amount)
        {
            if (item == null)
            {
                return 0;
            }
            // 1.7.5：读档期一律不收。所有分流路径的汇聚点，在这里兜底。
            if (IsLoading())
            {
                return 0;
            }
            // 自动整理总开关：关掉后一律不收（批量/单件/回调都经过这里）。
            if (!DocCatalog.AutoFileEnabled())
            {
                return 0;
            }
            int want = amount < 0 ? item.unitCount : amount;
            if (want <= 0)
            {
                return 0;
            }
            int moved = 0;
            // 第一轮专属册子，第二轮万能手册。
            for (int round = 0; round < 2 && moved < want; round++)
            {
                bool omniRound = round == 1;
                for (int i = 0; i < Books.All.Length && moved < want; i++)
                {
                    BookDef def = Books.All[i];
                    if ((def.Id == Books.OmniId) != omniRound || def.Accept == null)
                    {
                        continue;
                    }
                    if (!Accepts(def, item))
                    {
                        continue;
                    }
                    moved += MoveInto(def, item, want - moved);
                }
            }
            return moved;
        }

        /// <summary>把最多 amount 个单位移进一本册子，返回实际移动量（TryAcceptAll 返回的是剩余量）。</summary>
        private static int MoveInto(BookDef def, GameItem item, int amount)
        {
            if (item == null || def == null || amount <= 0)
            {
                return 0;
            }
            try
            {
                GameItem book = ShipScan.Find(def.Id);
                if (book == null || book == item)
                {
                    Debug.Log("[BETTERHB] autofile skip " + def.Id + ": book not in store");
                    return 0;
                }
                if (!GeneralHelper.MayPlayerInsertInto(book))
                {
                    Debug.Log("[BETTERHB] autofile skip " + def.Id + ": may not insert");
                    return 0;
                }
                if (!book.MayHaveValidInventorySlot(item))
                {
                    Debug.Log("[BETTERHB] autofile skip " + def.Id + ": no valid slot");
                    return 0;
                }
                SlotMarker slot = book.TryFindOneValidInventorySlot(item);
                if (slot == null || slot.numTransfer <= 0)
                {
                    Debug.Log("[BETTERHB] autofile skip " + def.Id + ": slot numTransfer<=0");
                    return 0;
                }
                int ask = amount < 0 ? item.unitCount : amount;
                int left = GraphUtils.TryAcceptAll(book, item, ask);
                int moved = ask - left;
                if (moved > 0)
                {
                    ShipScan.Invalidate();
                    Debug.Log("[BETTERHB] autofile move " + def.Id + " moved=" + moved + "/" + ask + " for " + item.identifier);
                    return moved;
                }
                Debug.Log("[BETTERHB] autofile move " + def.Id + " moved=0 for " + item.identifier);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] autofile move " + def.Id + " failed: " + e.Message);
            }
            return 0;
        }

        /// <summary>只做判定、不移动物品：当前店里有没有册子肯收且放得下这本手册（给拖拽提示用）。</summary>
        internal static bool HasFileTarget(GameItem item)
        {
            // 关掉自动整理后，提示与高亮也恢复原版，不再显示"能归位"的绿色引导。
            if (!DocCatalog.AutoFileEnabled())
            {
                return false;
            }
            if (!IsEligibleDocument(item))
            {
                return false;
            }
            for (int round = 0; round < 2; round++)
            {
                bool omniRound = round == 1;
                for (int i = 0; i < Books.All.Length; i++)
                {
                    BookDef def = Books.All[i];
                    if ((def.Id == Books.OmniId) != omniRound || def.Accept == null || !Accepts(def, item))
                    {
                        continue;
                    }
                    GameItem book = ShipScan.Find(def.Id);
                    if (book == null || book == item || !GeneralHelper.MayPlayerInsertInto(book)
                        || !book.MayHaveValidInventorySlot(item))
                    {
                        continue;
                    }
                    SlotMarker slot = book.TryFindOneValidInventorySlot(item);
                    if (slot != null && slot.numTransfer > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>批量入口的目标是不是档案箱：可能是 dossier 本体，也可能是它的格子。</summary>
        internal static bool IsDossierNode(GraphNodeStorage node)
        {
            GameItem owner = node as GameItem;
            if (owner != null)
            {
                return owner.identifier == "dossier";
            }
            GameGridInventory grid = node as GameGridInventory;
            if (grid == null)
            {
                return false;
            }
            try
            {
                GameItem gridOwner = GraphUtils.FindParentType<GameItem>(grid);
                return gridOwner != null && gridOwner.identifier == "dossier";
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>记下批量入口已经分流过的物品，避免它落箱后的回调再整理一次。</summary>
        internal static void MarkRedirected(GameItem item)
        {
            if (item == null)
            {
                return;
            }
            lastRedirectFrame = Time.frameCount;
            lastRedirectUid = item.uniqueId;
        }
    }

    // 1.7.5：框住整个读档过程，期间自动整理一律放行。
    // LoadGame() 内部逐件 UncheckedAccept 重建档案箱（反编译 95716），末尾才 FireOnGameLoadedLate（95775）。
    // Prefix 开标志；Postfix 正常收尾清；Finalizer 保证中途抛异常也清，避免标志卡死让整理永久失效。
    [HarmonyPatch(typeof(PlayerStore), "LoadGame")]
    internal static class Patch_LoadGame
    {
        private static void Prefix()
        {
            AutoFile.BeginLoad();
        }

        private static void Postfix()
        {
            AutoFile.EndLoad();
        }

        private static Exception Finalizer(Exception __exception)
        {
            AutoFile.EndLoad();
            // 原样返回：只借 finalizer 清标志，不吞游戏的读档异常。
            return __exception;
        }
    }

    // 档案箱由 MiscItemDirectory.Dossier() 造出来。Hook 工厂而不是覆盖它赋的原生回调。
    [HarmonyPatch(typeof(MiscItemDirectory), "Dossier")]
    internal static class Patch_Dossier
    {
        private static void Postfix(GameItem __result)
        {
            AutoFile.Attach(__result);
        }
    }

    // 兜底：onSlotAddItemEndFunc 只有工厂里追加的那一条路径，档案箱换实例或被覆盖就失效。
    // 直接从格子接受物品的方法上再兜一次；靠 owner.identifier 过滤，不碰别的容器。
    [HarmonyPatch(typeof(GameGridInventory), "UncheckedAccept")]
    internal static class Patch_GridAccept
    {
        private static void Postfix(GameGridInventory __instance, GameItem item, bool __result)
        {
            if (__result)
            {
                AutoFile.OnGridAccept(__instance, item);
            }
        }
    }

    // 批量放入（一次把多本手册塞进档案箱）走的是 GraphUtils.TryAcceptAll(dossier, item, amount)。
    // 在物品占用档案箱格子之前就分流：册子收得下的部分直接进册子，完全分流走时跳过原版放入，
    // 所以档案箱没有空位也能处理整批。收不下的部分仍走原逻辑，留在档案箱或原处。
    [HarmonyPatch(typeof(GraphUtils), "TryAcceptAll")]
    internal static class Patch_BulkFile
    {
        private static bool Prefix(GraphNodeStorage currentNode, GameItem item, ref int amount, ref int __result)
        {
            if (!AutoFile.IsDossierNode(currentNode) || !AutoFile.IsEligibleDocument(item))
            {
                return true;
            }
            int want = amount < 0 ? item.unitCount : amount;
            if (want <= 0)
            {
                return true;
            }
            int moved = AutoFile.FileIntoBooks(item, want);
            if (moved <= 0)
            {
                return true;
            }
            AutoFile.MarkRedirected(item);
            Debug.Log("[BETTERHB] autofile bulk intercepted " + item.identifier + " moved=" + moved + "/" + want);
            if (moved >= want)
            {
                __result = 0;   // TryAcceptAll 返回剩余未放数量，0 表示全部处理完
                return false;   // 不占档案箱格子
            }
            if (amount > 0)
            {
                amount -= moved;   // 剩余部分交给原版继续尝试
            }
            return true;
        }
    }

    // 单件拖放不走 GraphUtils.TryAcceptAll：ItemMouseDragHandler.EndDrag 里先
    // TryInventorySlot(...).TryAcceptOnce()，档案箱没空位时 lastSlot 为 null，
    // 落箱后的回调/UncheckedAccept 补丁全都不会触发。这里在找空位之前就把文档分流到册子。
    [HarmonyPatch(typeof(ItemMouseDragHandler), "EndDrag")]
    internal static class Patch_SingleDragFile
    {
        private static FieldInfo FindField(object target, string name)
        {
            if (target == null)
            {
                return null;
            }
            // 必须用字面 typeof：target.GetType().GetField(...) 的接收者是运行时计算的类型，
            // 会被游戏的 mod 扫描器判成 reflection-external 能力并在模组列表打感叹号。
            return AccessTools.Field(typeof(ItemMouseDragHandler), name);
        }

        private static T GetField<T>(object target, string name)
        {
            FieldInfo field = FindField(target, name);
            object value = field == null ? null : field.GetValue(target);
            return value is T ? (T)value : default(T);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = FindField(target, name);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private static void Prefix(ItemMouseDragHandler __instance)
        {
            try
            {
                GameItem item = GetField<GameItem>(__instance, "currentItem");
                if (!AutoFile.IsEligibleDocument(item))
                {
                    return;
                }
                GameInventory dropInventory = GetField<GameInventory>(__instance, "lastInventory");
                GameItem dropItem = GetField<GameItem>(__instance, "lastItem");
                bool toDossier = (dropInventory != null && AutoFile.IsDossierNode(dropInventory))
                                 || (dropItem != null && dropItem.identifier == "dossier");
                if (!toDossier)
                {
                    return;
                }
                int want = GetField<int>(__instance, "currentRemove");
                if (want <= 0)
                {
                    want = item.unitCount;
                }
                int moved = AutoFile.FileIntoBooks(item, want);
                if (moved <= 0)
                {
                    return;
                }
                AutoFile.MarkRedirected(item);
                // 已经全部进册子了，别让原版 EndDrag 再按旧的 currentRemove 去找档案箱空位。
                if (moved >= want)
                {
                    SetField(__instance, "currentRemove", 0);
                }
                Debug.Log("[BETTERHB] autofile single-drag intercepted " + item.identifier
                          + " moved=" + moved + "/" + want);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] autofile single-drag intercept failed: " + e.Message);
            }
        }
    }

    // 拖拽悬停到档案箱上时，原版按"档案箱有没有空位"显示 mech_drag_cant_insert 红字。
    // 我们会在落点前把手册分流进册子，所以只要确实有册子肯收就改成绿色的肯定句。
    // 配合下面的 Patch_DragHighlight 把档案箱的高亮也从红改绿，文字和高亮同为绿色，不再冲突。
    [HarmonyPatch(typeof(RichTextGenerator), "InsertItemInOtherItem")]
    internal static class Patch_DragTooltip
    {
        private static void Postfix(GameItem sourceItem, GameItem targetItem, bool canInsert,
            RichTextBuilder customBuilder, ref RichText __result)
        {
            if (canInsert || targetItem == null || targetItem.identifier != "dossier")
            {
                return;
            }
            if (!AutoFile.HasFileTarget(sourceItem))
            {
                return;
            }
            if (customBuilder == null)
            {
                return;
            }
            customBuilder.Clear();
            customBuilder.Add(LocHelper.GetLocalizedMechanic("mech_drag_insert_into",
                sourceItem.GetDisplayName(true), "<u>" + targetItem.GetDisplayName(true) + "</u>"),
                RenderHandler.ColorPalette.Green);
            __result = customBuilder.Build();
        }
    }

    // 把"目标为档案箱、拖拽物确实能归位"的红色高亮改成绿色，与绿色提示文字一致。
    // 满仓时原版把档案箱所在位置涂红走的是 parentInventory.HighlightSlot(slot, Red, doReplace:true)
    // 这条直接调用（MayHaveValidInventorySlot 只看物品类型、返回 true，但 TryFindOneValidInventorySlot
    // 返回 null → 判 Red），不经过 HighlightManager.Try。两条高亮路径最终都汇入各容器的
    // HighlightSlot，所以 patch HighlightSlot 才能全覆盖。
    internal static class DossierHighlightFix
    {
        internal static void Green(SlotMarker slot, ref RenderHandler.BackgroundColor color)
        {
            if (color != RenderHandler.BackgroundColor.Red || slot == null)
            {
                return;
            }
            // 高亮目标必须是档案箱：槽位里就是 dossier 本体，或落点是它的内部格子。
            bool isDossier = AutoFile.IsDossierNode(slot.item) || AutoFile.IsDossierNode(slot.inventory);
            if (!isDossier)
            {
                return;
            }
            // 拖拽物用公开的静态访问器取，字面类型不触发 mod 扫描器的反射判定。
            ItemMouseDragHandler handler = ItemMouseDragHandler.current;
            GameItem dragged = handler == null ? null : handler.GetCurrentItem();
            if (dragged == null || dragged == slot.item || !AutoFile.HasFileTarget(dragged))
            {
                return;
            }
            color = RenderHandler.BackgroundColor.Green;
            Debug.Log("[BETTERHB] autofile grid highlight red->green for " + dragged.identifier);
        }
    }

    // 档案箱本体放在哪种网格容器里并不固定，常见的三种都覆盖。只改 Red，不碰正常绿/蓝高亮。
    [HarmonyPatch(typeof(GameGridInventory), "HighlightSlot")]
    internal static class Patch_GridHighlight
    {
        private static void Prefix(SlotMarker slot, ref RenderHandler.BackgroundColor color)
        {
            DossierHighlightFix.Green(slot, ref color);
        }
    }

    [HarmonyPatch(typeof(GameGridScrollableInventory), "HighlightSlot")]
    internal static class Patch_ScrollGridHighlight
    {
        private static void Prefix(SlotMarker slot, ref RenderHandler.BackgroundColor color)
        {
            DossierHighlightFix.Green(slot, ref color);
        }
    }

    [HarmonyPatch(typeof(GameSlotInventory), "HighlightSlot")]
    internal static class Patch_SlotHighlight
    {
        private static void Prefix(SlotMarker slot, ref RenderHandler.BackgroundColor color)
        {
            DossierHighlightFix.Green(slot, ref color);
        }
    }
}
