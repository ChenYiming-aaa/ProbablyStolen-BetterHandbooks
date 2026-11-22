using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterHandbooks
{
    /// <summary>
    /// 册子的赠予流程。照抄原版 CreateMentorClientVisualInspection 的做法：
    /// 构造一个 DIALOGUE 类型的 StoreClient 排进当天队列，在 SetEndAction 里发物品。
    ///
    /// 时机规则（对四本册子通用）：
    ///   1. 触发条件当天只登记日期，不打扰玩家；
    ///   2. 次日起才会来，一天最多来一位，玩家不接待就第二天再来；
    ///   3. 玩家手上已经有这本册子（老档/上次已收）就直接记"已送"，不重复发。
    /// </summary>
    internal static class Giver
    {
        private static readonly Dictionary<string, int> lastInvitedDay = new Dictionary<string, int>();

        internal static void MaybeInvite(StoreClientManager manager)
        {
            if (manager == null)
            {
                return;
            }
            StoreStation station = StoreStation.Instance;
            if (station == null)
            {
                return;
            }
            int today = station.dayCounter;
            ShipScan.BeginBatch();
            // 每天一行扫描结果：万一某本一直不来，看这行就知道是"没达标"还是"没轮到他"。
            Debug.Log("[BETTERHB] day " + today + " scan: items=" + ShipScan.All().Count
                      + " lottery=" + ShipScan.Count(Books.LotteryItems)
                      + " recipe=" + ShipScan.Count(Books.RecipeGuides)
                      + " card=" + ShipScan.Count(Books.BusinessCards)
                      + " slip=" + ShipScan.Count(Books.SecSlips)
                      + " manual=" + ShipScan.Count(Books.ThirdPartyManuals)
                      + " [prod=" + ShipScan.Count(Books.ExternalProductionManuals)
                      + " appr=" + ShipScan.Count(Books.ExternalAppraisalManuals)
                      + " tool=" + ShipScan.Count(Books.ExternalToolBooks) + "]"
                      + " sign=" + ShipScan.CountPrefix(Books.SignPrefix)
                      + " docs=" + DocCatalog.Count
                      + " wantOmni=" + (DocCatalog.WantOmni() ? "1" : "0"));

            for (int i = 0; i < Books.All.Length; i++)
            {
                BookDef def = Books.All[i];
                if (def.ShopOnly)
                {
                    // 招牌箱是博士柜台上的商品，不走赠送队列，也不占"一天一位"的名额。
                    continue;
                }
                if (ModData.IsSent(def.Id))
                {
                    continue;
                }
                if (ShipScan.Has(def.Id))
                {
                    ModData.MarkSent(def.Id);
                    Debug.Log("[BETTERHB] " + def.Id + " already owned, marked as sent");
                    continue;
                }
                if (!IsReady(def))
                {
                    Debug.Log("[BETTERHB] " + def.Id + " not ready yet (day " + today + ")");
                    continue;
                }

                int readyDay = ModData.ReadyDay(def.Id, today);
                if (today <= readyDay)
                {
                    // 条件刚满足的当天不排，给玩家留一天缓冲，也避免同一天连着来两位。
                    Debug.Log("[BETTERHB] " + def.Id + " ready on day " + readyDay + ", will come tomorrow");
                    continue;
                }

                int last;
                if (lastInvitedDay.TryGetValue(def.Id, out last) && last == today)
                {
                    continue;
                }
                lastInvitedDay[def.Id] = today;

                try
                {
                    manager.AddClient(Build(def));
                    Debug.Log("[BETTERHB] invited " + def.Id + " on day " + today);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[BETTERHB] invite " + def.Id + " failed: " + e);
                }
                return;
            }
        }

        private static bool IsReady(BookDef def)
        {
            try
            {
                return def.Ready != null && def.Ready();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] ready check " + def.Id + " failed: " + e.Message);
                return false;
            }
        }

        private static StoreClient Build(BookDef def)
        {
            StoreClient client = new StoreClient();
            client.identifier = "giver_" + def.Key;
            client.displayName = LocHelper.GetLocalizedClientName(def.GiverNameKey);
            client.spriteName = ResolveSprite(def);
            client.clientIntent = StoreClient.ClientIntent.DIALOGUE;
            client.dismissable = false;

            string speaker = client.displayName;

            // 有的册子按情况换一套台词（目前是回执手册：治安官看你的举报成绩）。
            string variant = null;
            if (def.DialogVariant != null)
            {
                try
                {
                    variant = def.DialogVariant();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[BETTERHB] dialog variant " + def.Id + " failed: " + e.Message);
                }
            }

            Dialogue node = client.mainDialogue;
            for (int i = 0; i < def.DialogDefaults.Length; i++)
            {
                if (i > 0)
                {
                    node = node.NextDialogue();
                }
                string key = string.IsNullOrEmpty(variant)
                    ? def.DialogKey(i + 1)
                    : def.Key + "_" + variant + "_d" + (i + 1);
                string line = BookFactory.Text(key, def.DialogDefaults[i]);

                // 第 2 句通常提"通缉名单上几个"，一个都没抓到的时候换一句，不然会出现"你举报了 0 个"。
                if (i == 1 && Books.WantedClosed() == 0)
                {
                    string zero = BookFactory.Text(key + "_zero", null);
                    if (!string.IsNullOrEmpty(zero))
                    {
                        line = zero;
                    }
                }
                node = node.SetText(speaker, Books.FillNumbers(line));
            }
            node.SetEndAction(() => Deliver(def));
            return client;
        }

        /// <summary>
        /// 立绘：导师用固定的 "mentor"，其他角色用游戏自己的随机商人立绘。
        /// 直接写死一个不存在的键时，SpriteDict.GetSprite 会退回随机拾荒者立绘并打警告，很难看。
        /// </summary>
        private static string ResolveSprite(BookDef def)
        {
            if (!string.IsNullOrEmpty(def.GiverSprite))
            {
                return def.GiverSprite;
            }
            try
            {
                // 治安官要用治安部那批立绘，不是商人那批。
                string name = def.GiverIsSecurity
                    ? SpriteDict.GetRandomSecSpriteName()
                    : SpriteDict.GetRandomMerchantSpriteName();
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
            catch (Exception)
            {
            }
            return "george";
        }

        private static void Deliver(BookDef def)
        {
            try
            {
                if (ShipScan.Has(def.Id))
                {
                    ModData.MarkSent(def.Id);
                    Debug.Log("[BETTERHB] " + def.Id + " already in ship, skip delivery");
                    return;
                }

                GameItem book = DirectoryMaster.Item(def.Id);
                string source = "directory";
                if (book == null || !book.IsTag("DOCUMENT"))
                {
                    book = BookFactory.Create(def);
                    source = "built";
                    Debug.Log("[BETTERHB] directory had no " + def.Id + ", built one directly");
                }
                if (book == null)
                {
                    return;
                }

                book.name = BookFactory.Text(def.NameKey, def.DefaultName);
                book.shortDescription = BookFactory.Text(def.DescKey, def.DefaultDesc);
                BookFactory.ApplySprite(book, def);

                // 档案箱（dossier）的过滤器是 AllowOnlyTaggedItemsOr(..., ownedOnly: true)，
                // 送出的东西必须是"已拥有"才放得进去。这里先写死，不依赖商店那条路。
                book.EnableTag("IS_OWNED_TAG");

                // 柜台摆不下时 AddDirectSellingItemToTable 会走 TryAcceptAllOrDestroy，
                // 物品被直接销毁而我们还在打"delivered"——这是 1.1.0 丢过一本回执手册的原因。
                // 所以先问柜台收不收（按物品实际格子判定），收不下就换到店内其它容器。
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
                    Debug.LogWarning("[BETTERHB] counter had no room, " + def.Id + " went to store inventory instead");
                }

                ShipScan.Invalidate();
                ModData.MarkSent(def.Id);
                Debug.Log("[BETTERHB] delivered " + def.Id + " (" + book.name + ")"
                          + " from=" + source
                          + " counter=" + fits
                          + " owned=" + GeneralHelper.IsItemOwned(book)
                          + " document=" + book.IsTag("DOCUMENT")
                          + " unit=" + book.unitCount
                          + " parent=" + (book.parentInventory == null ? "none" : book.parentInventory.GetType().Name)
                          + " shape=" + (book.modifiedShape == null ? "null" : book.modifiedShape.width + "x" + book.modifiedShape.height));
                Diagnostics.ProbeDossierFor(book);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] deliver " + def.Id + " failed: " + e);
            }
            try
            {
                PlayerStore.Instance.DismissCurrentClient(callNextWhenPossible: false);
            }
            catch (Exception)
            {
            }
        }
    }
}
