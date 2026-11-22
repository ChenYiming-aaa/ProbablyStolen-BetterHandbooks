using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterHandbooks
{
    /// <summary>
    /// 全船物品扫描。用游戏自己的船体根节点兜底，避免玩家把东西挪到展示柜/机器里就漏掉。
    /// 同一个物品可能从多个根节点被遍历到，按 uniqueId 去重。
    /// </summary>
    internal static class ShipScan
    {
        // 一天里四本册子都要查一遍（IsSent/Has/Count），全船遍历重复做没意义。
        // Giver 每次排客人前 BeginBatch 一次，当天结束时丢掉缓存。
        private static List<GameItem> cache;

        internal static void BeginBatch()
        {
            cache = null;
        }

        internal static void Invalidate()
        {
            cache = null;
        }

        internal static List<GameItem> All()
        {
            if (cache != null)
            {
                return cache;
            }
            List<GameItem> result = new List<GameItem>();
            HashSet<int> seen = new HashSet<int>();

            // 先用游戏自己的全量清单：invElement / back / front / docInvElement（档案抽屉）
            // / showcaseElement（展示柜）/ trash / hidden / afterhour / 售货机，一个不落。
            // 自己拼容器列表会漏，之前生产手册和电话簿一直不触发就是漏在这里。
            EmporiumEntry entry = EmporiumEntry.Instance;
            if (entry != null)
            {
                try
                {
                    List<GameItem> all = entry.GetAllItems();
                    for (int i = 0; i < all.Count; i++)
                    {
                        Add(all[i], result, seen);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[BETTERHB] store scan failed: " + e.Message);
                }
            }

            // 再补上船体根节点：机器、仓库、以及不在店铺容器里的东西。
            Collect(GameMaster.current != null && GameMaster.current.shipManager != null
                ? (GraphNodeStorage)GameMaster.current.shipManager.ship
                : null, result, seen);

            cache = result;
            return result;
        }

        private static void Add(GameItem item, List<GameItem> result, HashSet<int> seen)
        {
            if (item == null)
            {
                return;
            }
            int key = item.uniqueId;
            if (key != 0 && !seen.Add(key))
            {
                return;
            }
            result.Add(item);
        }

        private static void Collect(GraphNodeStorage root, List<GameItem> result, HashSet<int> seen)
        {
            if (root == null)
            {
                return;
            }
            List<GameItem> items;
            try
            {
                items = GraphUtils.FindAllChildrenType<GameItem>(root);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] scan failed: " + e.Message);
                return;
            }
            for (int i = 0; i < items.Count; i++)
            {
                Add(items[i], result, seen);
            }
        }

        internal static int Count(string[] identifiers)
        {
            List<GameItem> items = All();
            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                GameItem item = items[i];
                if (item == null)
                {
                    continue;
                }
                for (int j = 0; j < identifiers.Length; j++)
                {
                    if (identifiers[j] == item.identifier)
                    {
                        count++;
                        break;
                    }
                }
            }
            return count;
        }

        internal static int CountPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return 0;
            }
            List<GameItem> items = All();
            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                GameItem item = items[i];
                if (item != null && item.identifier != null
                    && item.identifier.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        internal static bool Has(string identifier)
        {
            List<GameItem> items = All();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].identifier == identifier)
                {
                    return true;
                }
            }
            return false;
        }

        internal static GameItem Find(string identifier)
        {
            List<GameItem> items = All();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].identifier == identifier)
                {
                    return items[i];
                }
            }
            return null;
        }
    }
}
