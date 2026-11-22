using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterHandbooks
{
    /// <summary>
    /// 册子容量设置。走游戏自带的 ModConfig（主菜单「模组」列表选中本 mod 后进设置），
    /// 每本册子两个整数：宽度 / 高度（格子数）。改完立即套用到已经拿到的册子上。
    ///
    /// 只放大不缩小：缩小时如果里面有东西塞不下，就保持原尺寸并记一行日志，
    /// 免得重建格子把玩家的纸挤掉。
    /// </summary>
    internal static class Settings
    {
        private const string ColsSuffix = "_cols";
        private const string RowsSuffix = "_rows";

        private const int MinCells = 2;
        private const int MaxCells = 16;

        private static readonly Dictionary<string, ConfigEntry<int>> entries = new Dictionary<string, ConfigEntry<int>>();

        // 1.5.0 起万能手册的开关与"按名字勾选"全部由 DocCatalog 在游戏加载后动态生成，
        // 这里只管七本册子的尺寸（int 条目），不再持有 bool 开关。

        private static bool defined;

        internal static void Define()
        {
            if (defined)
            {
                return;
            }
            defined = true;
            try
            {
                ModConfig config = ModHelper.GetConfig(ModEntry.ID);
                if (config == null)
                {
                    Debug.LogWarning("[BETTERHB] no ModConfig, keeping default sizes");
                    return;
                }
                string section = BookFactory.Text("cfg_section", "更好的手册");
                string colsLabel = BookFactory.Text("cfg_cols", "宽（格）");
                string rowsLabel = BookFactory.Text("cfg_rows", "高（格）");
                string tip = BookFactory.Text("cfg_tip", "改完立即生效。册子里有东西装不下时不会缩小。");

                // 标签在窗口打开时才求值：Define 发生在本地化装载之前，
                // 这时候取键只会拿到 key 本身，名字会永久钉成中文。
                for (int i = 0; i < Books.All.Length; i++)
                {
                    BookDef def = Books.All[i];
                    BookDef captured = def;

                    ConfigEntry<int> cols = config.Define(def.Id + ColsSuffix, def.Width, section,
                        () => BookFactory.Text(captured.NameKey, captured.DefaultName) + " · " + colsLabel, MinCells, MaxCells, () => tip);
                    ConfigEntry<int> rows = config.Define(def.Id + RowsSuffix, def.Height, section,
                        () => BookFactory.Text(captured.NameKey, captured.DefaultName) + " · " + rowsLabel, MinCells, MaxCells, () => tip);

                    cols.OnChanged += _ => ApplyToOwned(def);
                    rows.OnChanged += _ => ApplyToOwned(def);

                    entries[def.Id + ColsSuffix] = cols;
                    entries[def.Id + RowsSuffix] = rows;
                }

                Debug.Log("[BETTERHB] settings ready: " + entries.Count + " size entries");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] settings failed: " + e);
            }
        }

        internal static int Width(BookDef def)
        {
            return Read(def.Id + ColsSuffix, def.Width);
        }

        internal static int Height(BookDef def)
        {
            return Read(def.Id + RowsSuffix, def.Height);
        }

        /// <summary>给设置窗口的补丁用：按 key 取回我们自己的强类型条目。</summary>
        internal static bool TryGetEntry(string key, out ConfigEntry<int> entry)
        {
            entry = null;
            return key != null && entries.TryGetValue(key, out entry) && entry != null;
        }

        private static int Read(string key, int fallback)
        {
            ConfigEntry<int> entry;
            if (entries.TryGetValue(key, out entry) && entry != null)
            {
                return entry.Value;
            }
            return fallback;
        }

        /// <summary>把当前尺寸套到手上已有的那本册子（可能不止一本）。</summary>
        private static void ApplyToOwned(BookDef def)
        {
            try
            {
                ShipScan.Invalidate();
                List<GameItem> items = ShipScan.All();
                int changed = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    GameItem item = items[i];
                    if (item != null && item.identifier == def.Id && BookFactory.ApplySize(item, def))
                    {
                        changed++;
                    }
                }
                Debug.Log("[BETTERHB] size " + def.Id + " = " + Width(def) + "x" + Height(def) + ", updated " + changed + " book(s)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] resize " + def.Id + " failed: " + e);
            }
        }
    }
}
