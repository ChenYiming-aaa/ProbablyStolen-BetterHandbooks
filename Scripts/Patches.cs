using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BetterHandbooks
{
    // 补丁原则：不跳过原方法、不改原参数、不覆盖返回值。
    // 任何异常都吞掉并记一行日志——mod 出错不能影响游戏。

    /// <summary>
    /// 原版 IngotGuide() 是十三张指南纸里唯一漏打 GUIDE_PAPER_TAG 的。
    /// 本 mod 的生产手册按 identifier 收纳，不依赖这个标签，但补上它能让原版系统行为一致
    /// （例如档案箱、掉落筛选），成本为零。
    /// </summary>
    [HarmonyPatch(typeof(DirectoryMaster), "Item", new Type[] { typeof(string), typeof(bool) })]
    internal static class Patch_Item
    {
        private const string IngotId = "ingot_book";
        private const string GuideTag = "GUIDE_PAPER_TAG";

        private static void Postfix(string identifier, GameItem __result)
        {
            try
            {
                if (__result == null || identifier != IngotId)
                {
                    return;
                }
                if (__result.EnableTag(GuideTag))
                {
                    Debug.Log("[BETTERHB] metallurgy guide tagged");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] patch item: " + e.Message);
            }
        }
    }

    /// <summary>
    /// tooltip 底部那行蓝色斜体的「物品名 / mod名」是来源标注，由 ModSourceHelper.GetItemModName
    /// 反查"这个物品的工厂是哪个 mod 注册的"得来。册子走 ModItemDirectory 注册就一定会命中，
    /// 关标签没用，只能在这一步把本 mod 的物品判空。只影响自己的四本册子，不动全局设置。
    /// </summary>
    [HarmonyPatch(typeof(ModSourceHelper), "GetItemModName")]
    internal static class Patch_ModSource
    {
        private static void Postfix(GameItem item, ref string __result)
        {
            try
            {
                if (item != null && Books.IsOurs(item.identifier))
                {
                    __result = null;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] patch mod source: " + e.Message);
            }
        }
    }

    /// <summary>
    /// 原版每天生成顾客时会走 HandleClientFromIntro 排剧情客人（第 3 天导师教外观检查就在这里）。
    /// 这里在它排完之后再看有没有册子该送。
    /// </summary>
    [HarmonyPatch(typeof(StoreClientManager), "HandleClientFromIntro")]
    internal static class Patch_Intro
    {
        private static void Postfix(StoreClientManager __instance)
        {
            try
            {
                Giver.MaybeInvite(__instance);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] patch intro: " + e.Message);
            }
        }
    }

    /// <summary>
    /// 招牌箱"买过一次就不再进货"的**主判定**。上架时的 owned 检测只是兜底：
    /// 玩家买完就把箱子扔进太空、卖掉，下一次进店时清单里也没有它，
    /// 所以必须在成交那一刻落一笔存档标记。BuyAllItemFromTable 不走这里，
    /// 但那是批量买柜台上的东西，箱子只能一件一件议价，够用。
    /// </summary>
    [HarmonyPatch(typeof(PlayerStore), "OnItemBought")]
    internal static class Patch_ItemBought
    {
        private static void Postfix(GameItem gameItem)
        {
            try
            {
                if (gameItem != null && gameItem.identifier == Books.SignBoxId)
                {
                    ModData.MarkBought(Books.SignBoxId);
                    Debug.Log("[BETTERHB] sign_box bought, no more will be stocked");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] patch item bought: " + e.Message);
            }
        }
    }

    /// <summary>
    /// 招牌箱"装了就换图"：物品进出容器时看一眼里面还有没有招牌。
    ///
    /// 放入不走 GameInventory.TryAcceptAll——拖放的真正落点是
    /// GraphUtils.TryAcceptAll → SlotMarker.TryAcceptOnce → GameGridInventory.UncheckedAccept，
    /// 所以必须打在 UncheckedAccept 上（第一版打在 TryAcceptAll 上，日志里一次都没触发）。
    /// 取出走 GameGridInventory.Expel。容器不是我们的箱子时第一行就返回。
    /// </summary>
    [HarmonyPatch(typeof(GameGridInventory), "UncheckedAccept")]
    internal static class Patch_InventoryAccept
    {
        private static void Postfix(GameGridInventory __instance)
        {
            Patch_Skin.Apply(__instance, "accept");
        }
    }

    [HarmonyPatch(typeof(GameGridInventory), "Expel")]
    internal static class Patch_InventoryExpel
    {
        private static void Postfix(GameGridInventory __instance)
        {
            Patch_Skin.Apply(__instance, "expel");
        }
    }

    internal static class Patch_Skin
    {
        internal static void Apply(GameInventory inventory, string tag)
        {
            try
            {
                BookFactory.RefreshFilled(inventory);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] patch " + tag + ": " + e.Message);
            }
        }
    }

    /// <summary>
    /// 游戏自带的 mod 设置窗口对每个条目只给一个控件：有 min/max 就画滑块（不显示数值），
    /// 没范围才画输入框。滑到哪一格全靠眼估，"宽 6 格"和"宽 8 格"根本看不出来。
    ///
    /// 这里把本 mod 的容量行换成「标签 + 滑块 + 输入框」：拖滑块即时生效并回填数字，
    /// 也可以直接把数字填进去。输入框的 id 仍是条目 key，所以窗口自带的「应用 / 重置」
    /// 照常读得到它。
    /// </summary>
    [HarmonyPatch(typeof(ModConfigWindow), "BuildEntry")]
    internal static class Patch_ConfigRow
    {
        // 用 __args 取参数，不依赖私有方法的形参名。
        private static bool Prefix(object[] __args)
        {
            CustomUIBuilder builder = null;
            try
            {
                if (__args == null || __args.Length < 3)
                {
                    return true;
                }
                builder = __args[0] as CustomUIBuilder;
                ConfigEntry raw = __args[2] as ConfigEntry;
                if (builder == null || raw == null)
                {
                    return true;
                }
                ConfigEntry<int> entry;
                if (!Settings.TryGetEntry(raw.Key, out entry) || !ReferenceEquals(raw, entry))
                {
                    return true;
                }
                BuildRow(builder, raw, entry);
                return false;
            }
            catch (Exception e)
            {
                // 已经动过 builder 就返回 false，否则原方法会在半开的那一行里再画一遍，界面会烂掉。
                Debug.LogWarning("[BETTERHB] config row: " + e);
                try
                {
                    builder.End();
                }
                catch (Exception)
                {
                }
                return false;
            }
        }

        private static void BuildRow(CustomUIBuilder b, ConfigEntry raw, ConfigEntry<int> entry)
        {
            string text = Label(raw);
            if (raw.Tooltip != null)
            {
                string link = "modcfg:" + raw.Config.ModId + ":" + raw.Key;
                TMPIconHover.CustomLinks[link] = raw.Tooltip;
                text = text + " <link=\"" + link + "\"><sprite name=\"info\" tint=1></link>";
            }

            int min = Mathf.RoundToInt(raw.Min);
            int max = Mathf.RoundToInt(raw.Max);

            b.BeginRow(4f, TextAnchor.MiddleLeft);
            b.AddLabel(text, "label_" + raw.Key);
            b.AddFlexibleSpace();

            CustomUIElement input = null;
            CustomUIElement slider = null;

            // 滑块拿条目的 key：窗口自带的「应用 / 重置」都是按 key 取控件、按滑块读值，
            // 让输入框占着 key 的话，应用会把值当成 0 再夹到 min。
            b.AddSlider(min, max, entry.Value, delegate(float value)
            {
                int cells = Mathf.Clamp(Mathf.RoundToInt(value), min, max);
                if (entry.Value != cells)
                {
                    entry.Set(cells);
                }
                if (input != null && input.IsAlive)
                {
                    input.SetString(cells.ToString());
                }
            }, true, raw.Key);
            b.Last.SetFixedWidth(170f);
            slider = b.Last;

            b.AddInput("", entry.Value.ToString(), delegate(string typed)
            {
                int cells;
                if (int.TryParse((typed ?? "").Trim(), out cells))
                {
                    cells = Mathf.Clamp(cells, min, max);
                    if (entry.Value != cells)
                    {
                        entry.Set(cells);
                    }
                    if (slider != null && slider.IsAlive)
                    {
                        slider.SetFloat(cells);
                    }
                    if (input != null && input.IsAlive)
                    {
                        input.SetString(cells.ToString());
                    }
                }
            }, raw.Key + "_input");
            b.Last.SetFixedWidth(70f);
            input = b.Last;

            b.End();
        }

        private static string Label(ConfigEntry entry)
        {
            if (entry.Label == null)
            {
                return entry.Key;
            }
            try
            {
                return entry.Label();
            }
            catch (Exception)
            {
                return entry.Key;
            }
        }
    }

    /// <summary>
    /// 「恢复默认」走的是 ResetWidgets：它只把控件回写默认值，并不调用条目本身。
    /// 我们这一行有两个控件，滑块会被改回去、输入框却停在旧数字，看着就像按钮没反应。
    /// 这里补一次：真把值设回默认（顺带即时改尺寸），再把两个控件都刷成默认值。
    /// </summary>
    [HarmonyPatch(typeof(ModConfigWindow), "ResetWidgets")]
    internal static class Patch_ConfigReset
    {
        private static void Postfix(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length < 2)
                {
                    return;
                }
                CustomUIWindow window = __args[0] as CustomUIWindow;
                ModConfig config = __args[1] as ModConfig;
                if (window == null || config == null || !window.IsAlive)
                {
                    return;
                }

                int reset = 0;
                for (int i = 0; i < config.Entries.Count; i++)
                {
                    ConfigEntry raw = config.Entries[i];
                    ConfigEntry<int> entry;
                    if (raw == null || !Settings.TryGetEntry(raw.Key, out entry) || !ReferenceEquals(raw, entry))
                    {
                        continue;
                    }
                    int fallback = entry.Default;
                    entry.Set(fallback);
                    reset++;

                    CustomUIElement slider = window.Get(raw.Key);
                    if (slider != null && slider.IsAlive)
                    {
                        slider.SetFloat(fallback);
                    }
                    CustomUIElement input = window.Get(raw.Key + "_input");
                    if (input != null && input.IsAlive)
                    {
                        input.SetString(fallback.ToString());
                    }
                }
                Debug.Log("[BETTERHB] settings reset: " + reset + " entries");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] settings reset: " + e);
            }
        }
    }

    /// <summary>
    /// "放不进"到底卡在哪一步。MayHaveValidInventorySlot 是格容器的总闸，
    /// 返回 false 时把它的四道关重新复核一遍打出来（同物品同容器只记第一次，拖动时不会刷屏）。
    /// </summary>
    [HarmonyPatch(typeof(GameGridInventory), "MayHaveValidInventorySlot")]
    internal static class Patch_SlotRefused
    {
        private static readonly HashSet<string> Seen = new HashSet<string>();

        private static void Postfix(GameGridInventory __instance, GameItem item, bool __result)
        {
            try
            {
                if (item == null || __instance == null)
                {
                    return;
                }
                string id = item.identifier;
                if (Books.Find(id) == null)
                {
                    return;
                }
                GameItem host = null;
                try
                {
                    host = GraphUtils.FindParentType<GameItem>(__instance);
                }
                catch (Exception)
                {
                }
                string hostId = host != null ? host.identifier : "?";
                // 档案箱放行时也记一行：能对照出"判定放行但没落格"这种半路失败。
                if (__result && hostId != "dossier")
                {
                    return;
                }
                if (!Seen.Add(id + "@" + hostId + "@" + __result))
                {
                    return;
                }
                bool locked = false, ownOk = false, room = false, hostOwned = false;
                string shape = "null";
                try
                {
                    locked = __instance.IsInsertLocked();
                }
                catch (Exception)
                {
                }
                try
                {
                    ownOk = GeneralHelper.MayOwnershipAcceptItem(item, __instance);
                }
                catch (Exception)
                {
                }
                try
                {
                    room = GraphUtils.CanAccept(__instance, item);
                }
                catch (Exception)
                {
                }
                try
                {
                    hostOwned = host != null && GeneralHelper.IsItemOwned(host);
                }
                catch (Exception)
                {
                }
                try
                {
                    shape = item.modifiedShape == null
                        ? "null"
                        : item.modifiedShape.width + "x" + item.modifiedShape.height;
                }
                catch (Exception)
                {
                }
                Debug.Log("[BETTERHB] " + (__result ? "mayfit" : "refused") + " " + id + " -> " + hostId
                          + " locked=" + locked + " ownOk=" + ownOk + " room=" + room
                          + " hostOwned=" + hostOwned + " shape=" + shape
                          + " itemOwned=" + GeneralHelper.IsItemOwned(item));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] slot probe: " + e.Message);
            }
        }
    }

    /// <summary>
    /// 空位搜索返回 null 时，把"为什么搜不到"打出来：
    /// TryFindOneValidInventorySlot 第一行就是 MaxNumRemove() &lt; 1 直接放弃，
    /// 所以先记 unitCount / MaxNumRemove / 挂在哪 / 形状有多少格。
    /// 同 (物品, 容器) 只记一次。
    /// </summary>
    [HarmonyPatch(typeof(GameGridInventory), "TryFindOneValidInventorySlot", new Type[] { typeof(GameItem) })]
    internal static class Patch_NoSlot
    {
        private static readonly HashSet<string> Seen = new HashSet<string>();

        private static void Postfix(GameGridInventory __instance, GameItem item, SlotMarker __result)
        {
            try
            {
                if (__result != null || item == null || __instance == null)
                {
                    return;
                }
                if (Books.Find(item.identifier) == null)
                {
                    return;
                }
                GameItem host = null;
                try
                {
                    host = GraphUtils.FindParentType<GameItem>(__instance);
                }
                catch (Exception)
                {
                }
                string hostId = host != null ? host.identifier : "?";
                if (!Seen.Add(item.identifier + "@" + hostId))
                {
                    return;
                }
                int maxRemove = -1;
                bool may = false;
                int maxAdd = -1;
                string shapeText = "null";
                try
                {
                    maxRemove = item.MaxNumRemove();
                }
                catch (Exception)
                {
                }
                try
                {
                    may = __instance.MayHaveValidInventorySlot(item);
                }
                catch (Exception)
                {
                }
                try
                {
                    maxAdd = item.MaxNumAdd();
                }
                catch (Exception)
                {
                }
                try
                {
                    GridShape shape = item.modifiedShape;
                    if (shape != null)
                    {
                        shapeText = shape.width + "x" + shape.height;
                    }
                }
                catch (Exception)
                {
                }
                Debug.Log("[BETTERHB] noslot " + item.identifier + " -> " + hostId
                          + " unit=" + item.unitCount + " maxRemove=" + maxRemove + " maxAdd=" + maxAdd
                          + " may=" + may + " shape=" + shapeText
                          + " parent=" + (item.parentInventory == null ? "none" : item.parentInventory.GetType().Name));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] noslot probe: " + e.Message);
            }
        }
    }

    /// <summary>真的放进容器了就记一行，方便和上面的 refused / noslot 对照。</summary>
    [HarmonyPatch(typeof(GameGridInventory), "UncheckedAccept")]
    internal static class Patch_Placed
    {
        private static void Postfix(GameGridInventory __instance, GameItem item, bool __result)
        {
            try
            {
                if (!__result || item == null || Books.Find(item.identifier) == null)
                {
                    return;
                }
                GameItem host = GraphUtils.FindParentType<GameItem>(__instance);
                Debug.Log("[BETTERHB] placed " + item.identifier + " into " + (host != null ? host.identifier : "?"));
            }
            catch (Exception)
            {
            }
        }
    }
}
