using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterHandbooks
{
    /// <summary>
    /// 进游戏时的就地修补：
    /// 1) 老存档里的冶金指南不会自己长出标签；
    /// 2) 已存在的册子（旧版发的、或本版发过的）补齐标签/窗口/收纳规则/贴图。
    /// </summary>
    internal static class Grant
    {
        private const string IngotId = "ingot_book";
        private const string GuideTag = "GUIDE_PAPER_TAG";

        internal static void Run()
        {
            // 读档后 uniqueId 会重排，上一局的"空/满"记录不能再用。
            BookFactory.ResetSkinCache();

            List<GameItem> items;
            try
            {
                items = ShipScan.All();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] scan failed: " + e.Message);
                return;
            }

            int repaired = 0;
            for (int i = 0; i < items.Count; i++)
            {
                GameItem item = items[i];
                if (item == null)
                {
                    continue;
                }
                if (item.identifier == IngotId && item.EnableTag(GuideTag))
                {
                    Debug.Log("[BETTERHB] tagged metallurgy guide");
                }
                BookDef def = Books.Find(item.identifier);
                if (def != null)
                {
                    BookFactory.Repair(item, def);
                    repaired++;
                }
            }
            Debug.Log("[BETTERHB] repaired " + repaired + " handbook(s)");
            ShipScan.Invalidate();
        }
    }
}
