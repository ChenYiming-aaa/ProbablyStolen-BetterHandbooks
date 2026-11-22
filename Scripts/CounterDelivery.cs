using System;
using UnityEngine;

namespace BetterHandbooks
{
    /// <summary>
    /// 万能手册的"直接放柜台"（1.5.0）：
    /// 不再由供货商上门送——玩家在设置里勾了「我要万能手册」后，
    /// 每次开门时检查：勾着 + 店里还没有万能手册 → 直接放一本到自己柜台。
    ///
    /// 用 OnShutterOpenedLate：卷帘门开、玩家店铺进入可操作状态时，柜台已经就绪。
    /// 柜台放不下就转店内其它容器（沿用 Giver 的处理），不静默丢物品。
    /// </summary>
    internal static class CounterDelivery
    {
        private static bool hooked;

        internal static void Hook()
        {
            if (hooked)
            {
                return;
            }
            hooked = true;
            ModHook.OnShutterOpenedLate += OnShutterOpened;
        }

        internal static void Unhook()
        {
            if (!hooked)
            {
                return;
            }
            hooked = false;
            ModHook.OnShutterOpenedLate -= OnShutterOpened;
        }

        private static void OnShutterOpened()
        {
            try
            {
                // 玩家没勾「我要万能手册」→ 完全不介入，开门行为保持原版。
                if (!DocCatalog.WantOmni())
                {
                    return;
                }
                // 店里/身上已经有一本 → 不重复放（扔了或卖了下次开门会补一本）。
                if (ShipScan.Has(Books.OmniId))
                {
                    return;
                }

                BookDef def = Books.Find(Books.OmniId);
                if (def == null)
                {
                    return;
                }

                GameItem book = DirectoryMaster.Item(def.Id);
                if (book == null || !book.IsTag("DOCUMENT"))
                {
                    book = BookFactory.Create(def);
                }
                if (book == null)
                {
                    Debug.LogWarning("[BETTERHB] omni book: could not create");
                    return;
                }

                book.name = BookFactory.Text(def.NameKey, def.DefaultName);
                book.shortDescription = BookFactory.Text(def.DescKey, def.DefaultDesc);
                BookFactory.ApplySprite(book, def);
                book.EnableTag("IS_OWNED_TAG");

                bool fits = false;
                try
                {
                    fits = EmporiumEntry.Instance != null
                           && EmporiumEntry.Instance.frontInvinvElement != null
                           && GraphUtils.CanAccept(EmporiumEntry.Instance.frontInvinvElement, book);
                }
                catch (Exception)
                {
                    fits = false;
                }
                if (fits)
                {
                    PlayerStore.Instance.AddDirectSellingItemToTable(book, isOwend: true);
                }
                else
                {
                    GraphUtils.EmporiumTryAdd(book);
                    Debug.LogWarning("[BETTERHB] omni book: counter full, went to store inventory");
                }

                ShipScan.Invalidate();
                Debug.Log("[BETTERHB] omni book placed at counter (wantOmni=1, counter=" + fits + ")");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] place omni book failed: " + e);
            }
        }
    }
}
