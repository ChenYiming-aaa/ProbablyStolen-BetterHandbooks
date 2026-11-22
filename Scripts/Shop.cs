using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterHandbooks
{
    /// <summary>
    /// 招牌箱的购买渠道：博士的柜台。
    ///
    /// 原版每晚到博士那儿，Arrive() 会调 PlaceInventorInventory() 把货物摆到柜台上，
    /// 并在函数开头触发 ModHook.OnPlaceInventorInventoryItemEarly(玩家已有的物品清单)。
    /// 我们就在这个钩子里把箱子放上去，走正常的议价流程——玩家自己决定买不买。
    ///
    /// 规矩（按需求定死）：
    ///   1) 只在他手上还没有箱子的时候上架；
    ///   2) 买过一次就永久下架（存档标记 bought_sign_box），扔了、卖了都不再补；
    ///   3) 同一晚只摆一个，反复进出博士的店不会刷出一排。
    /// </summary>
    internal static class Shop
    {
        private static bool hooked;

        internal static void Hook()
        {
            if (hooked)
            {
                return;
            }
            hooked = true;
            ModHook.OnPlaceInventorInventoryItemEarly += OnPlaceInventory;
        }

        internal static void Unhook()
        {
            if (!hooked)
            {
                return;
            }
            hooked = false;
            ModHook.OnPlaceInventorInventoryItemEarly -= OnPlaceInventory;
        }

        private static void OnPlaceInventory(List<GameItem> ownedItems)
        {
            try
            {
                string id = Books.SignBoxId;
                if (ModData.IsBought(id))
                {
                    return;
                }

                // 玩家手上已经有了（上次买走、或存档里本来就有一件）→ 记下"已购"，以后不再进货。
                // 用钩子传进来的清单（原版 GetAllOwnedItems 的结果），不去遍历全船，避免把
                // 柜台上那件还没买的也算成"已有"。
                if (Contains(ownedItems, id))
                {
                    ModData.MarkBought(id);
                    Debug.Log("[BETTERHB] sign_box already owned, will not be stocked again");
                    return;
                }

                int today = StoreStation.Instance != null ? StoreStation.Instance.dayCounter : int.MinValue;
                if (ModData.OfferedDay(id) == today)
                {
                    return;
                }

                PlayerStore store = PlayerStore.Instance;
                if (store == null)
                {
                    return;
                }
                if (!store.TryAddDirectSellingItemToTable(id))
                {
                    Debug.Log("[BETTERHB] sign_box not offered: Dr. Jackson's counter is full");
                    return;
                }
                ModData.MarkOffered(id, today);
                Debug.Log("[BETTERHB] sign_box stocked at Dr. Jackson's, day " + today + ", price 1");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] stock sign_box failed: " + e);
            }
        }

        private static bool Contains(List<GameItem> items, string identifier)
        {
            if (items == null)
            {
                return false;
            }
            for (int i = 0; i < items.Count; i++)
            {
                GameItem item = items[i];
                if (item != null && item.identifier == identifier)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
