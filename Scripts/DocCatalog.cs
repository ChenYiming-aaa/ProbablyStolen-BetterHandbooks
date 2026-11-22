using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterHandbooks
{
    /// <summary>
    /// 文档目录 + 万能手册的"按具体名字勾选"。
    ///
    /// 1.5.0 起万能手册不再靠硬编码名单或大类开关，而是：
    ///   - 游戏加载后扫描所有已注册的 DOCUMENT 物品，按来源 mod 分组，取每个的显示名；
    ///   - 在模组设置里为每一本生成勾选框（分 mod 一节），玩家勾哪些、万能手册就收哪些；
    ///   - 默认全不勾，不替玩家动物品。
    ///
    /// 扫描时机：必须等所有 mod 的物品目录注册完（OnGameLoadedLate），
    /// 早于此时 GetIdentifierList 拿不到别家 mod 的物品。
    /// 设置窗口是"打开时"才遍历 config.Entries 构建的，所以这会儿追加勾选框来得及。
    /// </summary>
    internal static class DocCatalog
    {
        internal const string WantOmniKey = "want_omni";
        internal const string AutoFileKey = "auto_file";
        internal const string SelectPrefix = "docsel_";

        private static bool scanned;
        // 主菜单打开设置和读档后都会触发扫描；同一个 key Define 两次会抛异常，这里去重。
        private static readonly HashSet<string> definedKeys = new HashSet<string>();
        private static readonly List<DocEntry> entries = new List<DocEntry>();

        /// <summary>一个可勾选的文档：identifier、显示名、来源 mod 显示名、分组 key、勾选条目。</summary>
        internal sealed class DocEntry
        {
            internal string identifier;
            internal string displayName;
            internal string modName;      // 显示用："游戏原版" 或 mod 名
            internal string modKey;       // 分组/去重用：稳定 key
            internal ConfigEntry<bool> toggle;
        }

        internal const string OriginalModKey = "original";

        /// <summary>一个稳定 key：优先用 MOD_ID_TAG，没有就用 "mod_" + 首字母归一化。
        /// 用它保证同一 mod 的东西聚一起，且不依赖显示名（显示名会随语言变）。</summary>
        private static string ModKeyFor(GameItem item, string identifier)
        {
            string modId = null;
            try
            {
                if (item.IsTag("MOD_ID_TAG"))
                {
                    modId = item.GetTagReadonly("MOD_ID_TAG").GetString();
                }
            }
            catch (Exception)
            {
            }
            if (!string.IsNullOrEmpty(modId))
            {
                return "mod_" + modId;
            }
            // 没打来源标记：原版物品。少数 mod 不打标记，退而按 identifier 前缀归组，
            // 前缀取到第一个下划线为止（be_ / te_ / gse_ / pcrepair. 等）。
            int cut = identifier.IndexOf('_');
            if (cut <= 0)
            {
                cut = identifier.IndexOf('.');
            }
            string prefix = cut > 0 ? identifier.Substring(0, cut) : identifier;
            return "pfx_" + prefix.ToLowerInvariant();
        }

        /// <summary>游戏加载后调用一次：扫描 + 追加设置勾选框。</summary>
        /// <param name="force">读档后传 true：主菜单时目录可能只注册了一部分，允许重扫。</param>
        internal static void ScanAndDefine(bool force = false)
        {
            if (scanned && !force)
            {
                return;
            }

            ModConfig config = ModHelper.GetConfig(ModEntry.ID);
            if (config == null)
            {
                Debug.LogWarning("[BETTERHB] doc catalog: no ModConfig, skip");
                return;
            }

            // "我要万能手册"总开关：默认关，玩家勾了才在开门时放一本到柜台。
            if (definedKeys.Add(WantOmniKey))
            {
                config.Define(WantOmniKey, false,
                    BookFactory.Text("cfg_omni_section", "万能手册"),
                    () => BookFactory.Text("cfg_want_omni", "我要万能手册（开门时放柜台）"),
                    () => BookFactory.Text("cfg_want_omni_tip", "勾上后，下次开门时如果店里没有万能手册，就直接放一本到你的柜台。取消后不再自动放，已有的册子保留。"));
            }

            // 自动整理总开关：1.7.6 起默认关。读档期游戏重建档案箱会连带触发整理，
            // 文档多时卡加载、个别情况下把纸弄丢；默认关让不知情的玩家不受影响。
            // 老玩家配置里已存过 auto_file 的，Register 会读回原值，不会被重置。
            // 玩家显式打开后，整理仍按 专属册子 → 万能手册 → 留档案箱 工作。
            if (definedKeys.Add(AutoFileKey))
            {
                config.Define(AutoFileKey, false,
                    BookFactory.Text("cfg_section", "更好的手册"),
                    () => BookFactory.Text("cfg_auto_file", "放入档案箱后自动整理"),
                    () => BookFactory.Text("cfg_auto_file_tip", "勾上后，把东西放进档案箱会自动归类到对应册子。关掉后东西会留在档案箱里，方便你把名片等留在外面做装饰。"));
            }

            List<string> ids;
            try
            {
                ids = DirectoryMaster.GetIdentifierList<GameItem>();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] doc catalog: enumerate failed: " + e.Message);
                return;
            }
            if (ids == null)
            {
                return;
            }

            int docCount = 0;
            int added = 0;
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (string.IsNullOrEmpty(id) || !seen.Add(id))
                {
                    continue;
                }
                // 本 mod 自己的册子/工具不进候选（防套娃）。
                if (Books.IsOurs(id))
                {
                    continue;
                }

                GameItem probe = null;
                try
                {
                    Func<GameItem> factory = DirectoryMaster.GetFactory<GameItem>(id);
                    if (factory == null)
                    {
                        continue;
                    }
                    probe = factory();   // 只 new + 设 identifier，不消耗 uniqueId、不入存档
                    if (probe == null || !probe.IsGameItemType("DOCUMENT"))
                    {
                        continue;
                    }
                    // 任务关键文档不列进勾选（合同/证书/档案等），免得玩家误勾卡任务。
                    if (Books.IsTaskCriticalId(id))
                    {
                        continue;
                    }

                    string displayName = SafeName(probe, id);
                    string modName = ModSourceHelper.GetItemModName(probe);
                    bool isOriginal = string.IsNullOrEmpty(modName);
                    string modKey = isOriginal ? OriginalModKey : ModKeyFor(probe, id);
                    string modLabel = isOriginal ? OriginalLabel() : StripTags(modName);

                    // 重扫时只补新出现的条目，已 Define 的 key 跳过。
                    string key = SelectPrefix + id;
                    if (!definedKeys.Add(key))
                    {
                        continue;
                    }
                    // section 是固定字符串（ModConfig 不支持延迟求值），显示成友好名。
                    string section = BookFactory.Text("cfg_doc_section", "手册（按来源）") + " · " + modLabel;
                    ConfigEntry<bool> toggle = config.Define(key, false,
                        section,
                        () => displayName,
                        () => BookFactory.Text("cfg_doc_tip", "勾上后万能手册会收这本手册；不勾就留在店里。"));

                    entries.Add(new DocEntry
                    {
                        identifier = id,
                        displayName = displayName,
                        modName = modLabel,
                        modKey = modKey,
                        toggle = toggle
                    });
                    added++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[BETTERHB] doc catalog: probe '" + id + "' failed: " + e.Message);
                }
                finally
                {
                    // 探针是脱离存档的实例，直接销毁，避免泄漏到场景里。
                    if (probe != null)
                    {
                        try { probe.Destroy(); } catch (Exception) { }
                    }
                }
                docCount++;
            }

            // 只有真的扫到东西才锁定；主菜单时目录没注册全，就留着等读档后再扫。
            if (added > 0)
            {
                scanned = true;
            }
            Debug.Log("[BETTERHB] doc catalog: scanned " + docCount + " ids, " + added
                      + " document types selectable (locked=" + scanned + ")");
            if (added == 0)
            {
                Debug.LogWarning("[BETTERHB] doc catalog: no documents yet, will retry on next open / save load");
            }
        }

        private static string SafeName(GameItem probe, string id)
        {
            try
            {
                string n = probe.GetDisplayName();
                if (!string.IsNullOrEmpty(n) && n != id && n != "UNKNOWN_ITEM")
                {
                    return n;
                }
            }
            catch (Exception)
            {
            }
            return id;
        }

        private static string StripTags(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }
            // mod 名可能带富文本标签，去掉。
            int a;
            while ((a = s.IndexOf('<')) >= 0)
            {
                int b = s.IndexOf('>', a);
                if (b < a)
                {
                    break;
                }
                s = s.Substring(0, a) + s.Substring(b + 1);
            }
            return s.Trim();
        }

        private static string OriginalLabel()
        {
            return BookFactory.Text("doc_original", "游戏原版");
        }

        /// <summary>某个 identifier 是否被玩家勾选为由万能手册收。默认不收。</summary>
        internal static bool IsSelected(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return false;
            }
            for (int i = 0; i < entries.Count; i++)
            {
                DocEntry e = entries[i];
                if (e.identifier == identifier)
                {
                    return e.toggle != null && e.toggle.Value;
                }
            }
            // 扫描还没跑（存档刚载入、设置还没打开）→ 去 ModConfig 直接读原始值，
            // 保证重进游戏后勾选仍然生效，不必先打开一次设置窗口。
            ModConfig config = ModHelper.GetConfig(ModEntry.ID);
            if (config != null)
            {
                ConfigEntry<bool> raw = config.Get(SelectPrefix + identifier) as ConfigEntry<bool>;
                if (raw != null)
                {
                    return raw.Value;
                }
            }
            return false;
        }

        /// <summary>玩家是否要一本万能手册（开门时放柜台）。默认不要。</summary>
        internal static bool WantOmni()
        {
            ModConfig config = ModHelper.GetConfig(ModEntry.ID);
            if (config == null)
            {
                return false;
            }
            ConfigEntry<bool> raw = config.Get(WantOmniKey) as ConfigEntry<bool>;
            return raw != null && raw.Value;
        }

        /// <summary>是否开启"放入档案箱后自动整理"。1.7.6 起默认关；配置条目还没注册时
        /// （扫描未跑/未开过设置）也按关处理，只有玩家在设置里明确勾上才开。</summary>
        internal static bool AutoFileEnabled()
        {
            ModConfig config = ModHelper.GetConfig(ModEntry.ID);
            if (config == null)
            {
                return false;
            }
            ConfigEntry<bool> raw = config.Get(AutoFileKey) as ConfigEntry<bool>;
            return raw != null && raw.Value;
        }

        internal static int Count
        {
            get { return entries.Count; }
        }
    }
}
