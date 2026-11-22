using System;
using UnityEngine;

namespace BetterHandbooks
{
    /// <summary>
    /// 存档里的 mod 数据：每本册子记一个"已送"标记、一个"条件达标日期"。
    /// 改名前的旧 mod（inspection.handbook）用 mentor_sent 记过鉴定手册的状态，这里做一次迁移，
    /// 免得升级后导师再送第二本。
    /// </summary>
    internal static class ModData
    {
        private const string SentPrefix = "sent_";
        private const string ReadyPrefix = "ready_";
        private const string BoughtPrefix = "bought_";
        private const string OfferedPrefix = "offered_";

        internal static bool IsSent(string bookId)
        {
            if (Get(SentPrefix + bookId) == "1")
            {
                return true;
            }
            if (bookId == Books.InspectionId && ModHelper.GetModData(ModEntry.LegacyId, "mentor_sent") == "1")
            {
                MarkSent(bookId);
                Debug.Log("[BETTERHB] migrated legacy mentor_sent flag");
                return true;
            }
            return false;
        }

        internal static void MarkSent(string bookId)
        {
            Set(SentPrefix + bookId, "1");
        }

        internal static int ReadyDay(string bookId, int today)
        {
            string raw = Get(ReadyPrefix + bookId);
            int day;
            if (string.IsNullOrEmpty(raw) || !int.TryParse(raw, out day))
            {
                Set(ReadyPrefix + bookId, today.ToString());
                return today;
            }
            return day;
        }

        // ---- 只卖不送的物品（招牌箱）----

        /// <summary>买过一次就永久记下：玩家把箱子扔了、卖了，博士也不会再进货。</summary>
        internal static bool IsBought(string itemId)
        {
            return Get(BoughtPrefix + itemId) == "1";
        }

        internal static void MarkBought(string itemId)
        {
            Set(BoughtPrefix + itemId, "1");
        }

        /// <summary>上一次上架的那一天，避免同一天反复进出博士店刷出一堆箱子。</summary>
        internal static int OfferedDay(string itemId)
        {
            string raw = Get(OfferedPrefix + itemId);
            int day;
            return !string.IsNullOrEmpty(raw) && int.TryParse(raw, out day) ? day : int.MinValue;
        }

        internal static void MarkOffered(string itemId, int day)
        {
            Set(OfferedPrefix + itemId, day.ToString());
        }

        private static string Get(string key)
        {
            try
            {
                return ModHelper.GetModData(ModEntry.ID, key);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Set(string key, string value)
        {
            try
            {
                ModHelper.SetModData(ModEntry.ID, key, value);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] mod data write failed: " + e.Message);
            }
        }
    }
}
