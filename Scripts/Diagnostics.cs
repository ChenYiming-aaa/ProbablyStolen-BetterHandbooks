using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterHandbooks
{
    /// <summary>
    /// 一次性自检：进游戏后对每本册子把"能不能收东西"这条链上每一道关卡跑一遍并打日志。
    ///
    /// 放置判定在 GameGridInventory.CheckInventorySlotStillValid 里，依次过
    /// MayHaveValidInventorySlot → CheckIsStateValid → CheckChangeParentShapeStateModifier → MaxNumAdd，
    /// 任何一步 false 都表现为"拖不进"，但玩家看不到是哪一步。这里把每步布尔值打出来。
    /// </summary>
    internal static class Diagnostics
    {
        private static bool ran;

        internal static void RunOnce()
        {
            if (ran)
            {
                return;
            }
            ran = true;

            List<GameItem> all;
            try
            {
                all = ShipScan.All();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] diag scan failed: " + e.Message);
                return;
            }

            for (int i = 0; i < Books.All.Length; i++)
            {
                BookDef def = Books.All[i];
                CheckBook(def, all);
            }

            CheckDossier(all);
        }

        /// <summary>
        /// 档案箱（dossier）的过滤器是 AllowOnlyTaggedItemsOr(DOCUMENT..., ownedOnly: true)。
        /// 册子放不进去时，这行能直接指出是"没算已拥有"、"缺 DOCUMENT"、还是"格子放不下"。
        /// </summary>
        private static void CheckDossier(List<GameItem> all)
        {
            GameItem dossier = null;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].identifier == "dossier")
                {
                    dossier = all[i];
                    break;
                }
            }
            if (dossier == null || dossier.contentWindow == null)
            {
                return;
            }
            GameGridInventory grid = dossier.contentWindow.child as GameGridInventory;
            if (grid == null)
            {
                return;
            }
            for (int i = 0; i < Books.All.Length; i++)
            {
                BookDef def = Books.All[i];
                GameItem book = null;
                for (int j = 0; j < all.Count; j++)
                {
                    if (all[j] != null && all[j].identifier == def.Id)
                    {
                        book = all[j];
                        break;
                    }
                }
                if (book == null)
                {
                    continue;
                }
                Probe(grid, book);
            }
        }

        /// <summary>
        /// 单个册子对档案箱的完整判定。放置失败时真正拦人的是 MayOwnershipAcceptItem
        /// （册子算"已拥有"、档案箱本身没算的话一律拒绝）和有没有空位，这里都打出来。
        /// </summary>
        internal static void Probe(GameGridInventory grid, GameItem book)
        {
            bool filter;
            try
            {
                filter = grid.mayInventoryAddItemFunc == null || grid.mayInventoryAddItemFunc(book, grid);
            }
            catch (Exception)
            {
                filter = false;
            }
            bool slot;
            try
            {
                slot = grid.CheckInventorySlotStillValid(book, null, null);
            }
            catch (Exception)
            {
                slot = false;
            }
            bool ownOk;
            try
            {
                ownOk = GeneralHelper.MayOwnershipAcceptItem(book, grid);
            }
            catch (Exception)
            {
                ownOk = false;
            }
            bool room;
            try
            {
                room = GraphUtils.CanAccept(grid, book);
            }
            catch (Exception)
            {
                room = false;
            }
            GameItem host = null;
            try
            {
                host = GraphUtils.FindParentType<GameItem>(grid);
            }
            catch (Exception)
            {
            }
            string shape = "null";
            try
            {
                shape = book.modifiedShape == null
                    ? "null"
                    : book.modifiedShape.width + "x" + book.modifiedShape.height;
            }
            catch (Exception)
            {
            }
            int maxRemove = -1;
            try
            {
                maxRemove = book.MaxNumRemove();
            }
            catch (Exception)
            {
            }
            Debug.Log("[BETTERHB] dossier " + book.identifier
                      + " own=" + GeneralHelper.IsItemOwned(book)
                      + " doc=" + book.IsTag("DOCUMENT")
                      + " filter=" + filter + " slot=" + slot
                      + " ownOk=" + ownOk + " room=" + room
                      + " shape=" + shape
                      + " unit=" + book.unitCount + " maxRemove=" + maxRemove
                      + " host=" + (host != null ? host.identifier : "?")
                      + " hostOwned=" + (host != null && GeneralHelper.IsItemOwned(host)));
        }

        /// <summary>送出一本之后立刻探一次档案箱，不用等下次读档。</summary>
        internal static void ProbeDossierFor(GameItem book)
        {
            if (book == null)
            {
                return;
            }
            try
            {
                List<GameItem> all = ShipScan.All();
                for (int i = 0; i < all.Count; i++)
                {
                    GameItem item = all[i];
                    if (item == null || item.identifier != "dossier" || item.contentWindow == null)
                    {
                        continue;
                    }
                    GameGridInventory grid = item.contentWindow.child as GameGridInventory;
                    if (grid != null)
                    {
                        Probe(grid, book);
                    }
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] dossier probe failed: " + e.Message);
            }
        }

        private static void CheckBook(BookDef def, List<GameItem> all)
        {
            GameItem book = null;
            GameItem sample = null;
            for (int i = 0; i < all.Count; i++)
            {
                GameItem item = all[i];
                if (item == null)
                {
                    continue;
                }
                if (book == null && item.identifier == def.Id)
                {
                    book = item;
                }
                if (sample == null && item.identifier != def.Id)
                {
                    try
                    {
                        if (def.Accept(item))
                        {
                            sample = item;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            if (book == null)
            {
                Debug.Log("[BETTERHB] diag " + def.Id + ": not owned yet");
                return;
            }

            GameGridInventory grid = book.contentWindow == null ? null : book.contentWindow.child as GameGridInventory;
            Debug.Log("[BETTERHB] diag " + def.Id
                      + " owned=" + GeneralHelper.IsItemOwned(book)
                      + " document=" + book.IsTag("DOCUMENT")
                      + " window=" + (book.contentWindow != null)
                      + " grid=" + (grid != null)
                      + " filter=" + (grid != null && grid.mayInventoryAddItemFunc != null));

            if (grid == null || sample == null)
            {
                return;
            }

            try
            {
                SlotMarker marker = new SlotMarker
                {
                    item = sample,
                    inventory = grid,
                    itemGridShape = sample.modifiedShape
                };
                bool may = grid.MayHaveValidInventorySlot(sample);
                bool accept = GeneralHelper.MayOwnershipAcceptItem(sample, grid);
                bool state = sample.CheckIsStateValid(grid, marker, sample.modifiedShape, sample.modifiedState);
                bool shape = grid.CheckChangeParentShapeStateModifier(sample, marker, sample.modifiedShape, sample.modifiedState);
                int max = sample.MaxNumAdd(marker);
                bool slot = grid.CheckInventorySlotStillValid(sample, null, null);
                Debug.Log("[BETTERHB] diag " + def.Id + " sample=" + sample.identifier
                          + " accept=" + accept + " may=" + may + " state=" + state
                          + " shape=" + shape + " maxAdd=" + max + " slotValid=" + slot);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] diag " + def.Id + " test failed: " + e.Message);
            }
        }
    }
}
