using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterHandbooks
{
    /// <summary>
    /// 册子物品的注册与修补。
    ///
    /// 为什么不直接用 DirectoryMaster.Item("inspection_book")：原版那件物品定义在 UnusedDirectory 里，
    /// 那个 Directory 是 MonoBehaviour、要靠场景挂载才会自注册，实际游戏里拿不到，
    /// DirectoryMaster 会退回一个空壳（无标签、无形状、双击无反应）。正确通道是官方的
    /// ModHook.OnModItemDirectoryInit，注册进 ModItemDirectory。
    /// </summary>
    internal static class BookFactory
    {
        // 拖动时收纳规则每帧都会被调用，按 identifier 去重，只记第一次。
        private static readonly HashSet<string> Decided = new HashSet<string>();

        // 招牌箱当前是空图还是满图：uniqueId -> 是否装了招牌。
        private static readonly Dictionary<int, bool> FilledState = new Dictionary<int, bool>();

        internal static void Register(ModItemDirectory directory)
        {
            if (directory == null)
            {
                return;
            }
            for (int i = 0; i < Books.All.Length; i++)
            {
                BookDef def = Books.All[i];
                try
                {
                    BookDef captured = def;
                    directory.Add(captured.Id, () => Create(captured));
                    Debug.Log("[BETTERHB] registered " + captured.Id);
                }
                catch (Exception e)
                {
                    Debug.LogError("[BETTERHB] register " + def.Id + " failed: " + e);
                }
            }
        }

        internal static GameItem Create(BookDef def)
        {
            (PixelWindow window, GameGridInventory grid) = DirectoryUtils.CreateGridWindow(Settings.Width(def), Settings.Height(def));
            GameItem item = ItemDirectory.CreateEmptyItem(def.Id);
            ApplySprite(item, def);
            item.SetContentWindow(window);
            ApplyCommon(item, def, grid);
            return item;
        }

        /// <summary>
        /// 修补老存档里的册子：缺 DOCUMENT 的就地补全；已经完整的只重刷收纳规则和贴图。
        /// 不销毁、不移动玩家手上的东西。
        /// </summary>
        internal static void Repair(GameItem item, BookDef def)
        {
            if (item == null || def == null)
            {
                return;
            }
            try
            {
                ApplyText(item, def);
                if (!item.IsTag("DOCUMENT"))
                {
                    (PixelWindow window, GameGridInventory grid) = DirectoryUtils.CreateGridWindow(Settings.Width(def), Settings.Height(def));
                    item.SetContentWindow(window);
                    ApplyCommon(item, def, grid);
                    Debug.Log("[BETTERHB] repaired shell " + def.Id);
                }
                else if (item.contentWindow != null)
                {
                    GameGridInventory existing = item.contentWindow.child as GameGridInventory;
                    if (existing != null)
                    {
                        ApplyFilter(existing, def);
                        ApplySize(item, def);
                    }
                }
                // 六件都按里面有没有东西选空图/满图。
                GameGridInventory live = item.contentWindow != null
                    ? item.contentWindow.child as GameGridInventory
                    : null;
                if (live != null && !string.IsNullOrEmpty(def.SpriteKeyFilled))
                {
                    RefreshFilled(live);
                }
                else
                {
                    ApplySprite(item, def);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] repair " + def.Id + " failed: " + e.Message);
            }
        }

        internal static void ApplySprite(GameItem item, BookDef def)
        {
            ApplySpriteKey(item, def, def.SpriteKey);
        }

        /// <summary>
        /// 重写名字和描述。注册发生在本地化装载之前，那会儿取键只能拿到 key 本身，
        /// 名字会退回写死的中文；读档时（Repair）本地化已经就绪，这里再刷一次。
        /// </summary>
        internal static void ApplyText(GameItem item, BookDef def)
        {
            if (item == null || def == null)
            {
                return;
            }
            string name = Text(def.NameKey, def.DefaultName);
            string desc = Text(def.DescKey, def.DefaultDesc);
            if (!string.IsNullOrEmpty(name))
            {
                item.name = name;
            }
            if (!string.IsNullOrEmpty(desc))
            {
                item.shortDescription = desc;
            }
        }

        internal static void ApplySpriteKey(GameItem item, BookDef def, string key)
        {
            if (item == null || string.IsNullOrEmpty(key))
            {
                return;
            }
            try
            {
                item.SetSpriteAndShapeFromMod(ModEntry.ID, key);
                if (item.useSpriteFromMod)
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] mod sprite failed for " + def.Id + "/" + key + ": " + e.Message);
            }
            item.SetSpriteAndShape("Items/itematlas", def.FallbackSprite ?? "tutorial_book");
        }

        /// <summary>
        /// 册子「空了/满了」换图。物品进出容器都会走到这里（TryAcceptAll / UncheckedAcceptAll / Expel）。
        /// 只处理带 SpriteKeyFilled 的册子（六件都是），其他容器第一行就返回。
        /// 里面有任意东西就算满——招牌箱的过滤器只放招牌，五本册子只放各自的纸，
        /// 所以不必按物品类型判断。同一个 uniqueId 的记录状态不变就不重复换贴图，
        /// 避免拖动时反复重建形状。
        /// </summary>
        internal static void RefreshFilled(GameInventory inventory)
        {
            if (inventory == null)
            {
                return;
            }
            GameItem owner = null;
            try
            {
                owner = inventory.GetParentItem();
            }
            catch (Exception)
            {
            }
            if (owner == null)
            {
                return;
            }
            BookDef def = Books.Find(owner.identifier);
            if (def == null || string.IsNullOrEmpty(def.SpriteKeyFilled))
            {
                return;
            }

            bool filled = false;
            GameGridInventory grid = inventory as GameGridInventory;
            List<GameItem> children = grid != null ? grid.childItems : null;
            if (children != null)
            {
                for (int i = 0; i < children.Count; i++)
                {
                    if (children[i] != null)
                    {
                        filled = true;
                        break;
                    }
                }
            }

            int key = owner.uniqueId;
            bool previous;
            if (FilledState.TryGetValue(key, out previous) && previous == filled)
            {
                return;
            }
            FilledState[key] = filled;
            ApplySpriteKey(owner, def, filled ? def.SpriteKeyFilled : def.SpriteKey);
            Debug.Log("[BETTERHB] " + def.Id + " skin -> " + (filled ? "full" : "empty"));
        }

        /// <summary>读档时 uniqueId 会重排，旧记录作废。</summary>
        internal static void ResetSkinCache()
        {
            FilledState.Clear();
        }

        private static void ApplyCommon(GameItem item, BookDef def, GameGridInventory grid)
        {
            item.name = Text(def.NameKey, def.DefaultName);
            item.shortDescription = Text(def.DescKey, def.DefaultDesc);

            // 这几步决定 tooltip 上的 [文档]、能否被档案箱接受、以及能否双击打开。
            item.EnableTag("DOCUMENT");
            item.SetGameItemType("DOCUMENT");
            item.EnableTag("CONTAINER_TAG");
            item.EnableTag("IMPORTANT_TAG");
            // 册子不值钱（0）；招牌箱单价 1 块，既是博士那儿的售价，也是 tooltip 上的估价。
            item.unitValue = def.UnitValue;

            ApplyFilter(grid, def);
        }

        /// <summary>
        /// 按设置调整已有册子的容量。放大直接做；缩小只在里面的东西仍放得下时才做，
        /// 否则保持原尺寸 —— 重建格子会把越界的纸挤掉。
        /// </summary>
        internal static bool ApplySize(GameItem item, BookDef def)
        {
            if (item == null || item.contentWindow == null)
            {
                return false;
            }
            GameGridInventory grid = item.contentWindow.child as GameGridInventory;
            if (grid == null || grid.inventoryShape == null)
            {
                return false;
            }
            int width = Settings.Width(def);
            int height = Settings.Height(def);
            int currentWidth = grid.inventoryShape.width;
            int currentHeight = grid.inventoryShape.height;
            if (currentWidth == width && currentHeight == height)
            {
                return false;
            }
            if ((width < currentWidth || height < currentHeight) && !ContentsFit(grid, width, height))
            {
                Debug.Log("[BETTERHB] keep " + def.Id + " at " + currentWidth + "x" + currentHeight
                          + ", contents do not fit in " + width + "x" + height);
                return false;
            }
            try
            {
                grid.SetShape(width, height);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] resize " + def.Id + " failed: " + e.Message);
                return false;
            }
            Debug.Log("[BETTERHB] resized " + def.Id + " " + currentWidth + "x" + currentHeight + " -> " + width + "x" + height);
            return true;
        }

        /// <summary>缩小前的安全检查：每件东西都还落在新范围内。</summary>
        private static bool ContentsFit(GameGridInventory grid, int width, int height)
        {
            List<GameItem> children = grid.childItems;
            if (children == null)
            {
                return true;
            }
            for (int i = 0; i < children.Count; i++)
            {
                GameItem child = children[i];
                if (child == null || child.modifiedShape == null)
                {
                    continue;
                }
                GridShape shape = child.modifiedShape;
                if (shape.minX < 0 || shape.minY < 0 || shape.maxX >= width || shape.maxY >= height)
                {
                    return false;
                }
            }
            return true;
        }

        private static void ApplyFilter(GameGridInventory grid, BookDef def)
        {
            grid.mayInventoryAddItemFunc = delegate(GameItem candidate, GameInventory _)
            {
                bool allow;
                try
                {
                    if (candidate == null)
                    {
                        return false;
                    }
                    allow = def.Accept(candidate);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[BETTERHB] filter threw for " + def.Id + ": " + e.Message);
                    return true;
                }
                LogDecision(def, candidate, allow);
                return allow;
            };
        }

        private static void LogDecision(BookDef def, GameItem candidate, bool allow)
        {
            string key = def.Id + "|" + candidate.identifier + "|" + allow;
            if (!Decided.Add(key))
            {
                return;
            }
            Debug.Log("[BETTERHB] " + def.Id + " accepts " + candidate.identifier + "? " + allow);
        }

        internal static string Text(string key, string fallback)
        {
            string value = null;
            try
            {
                value = ModHelper.GetLocalized(ModEntry.ID, key);
            }
            catch (Exception)
            {
            }
            if (string.IsNullOrEmpty(value) || value == key)
            {
                return fallback;
            }
            return value;
        }
    }
}
