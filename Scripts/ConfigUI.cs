using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BetterHandbooks
{
    /// <summary>
    /// 设置界面重构（1.6.0）：只接管本 mod 的设置窗口，做成两个标签页——
    ///   ·「册子大小」：六本基础册子的宽/高放在同一行（滑块 + 输入框）；
    ///   ·「万能手册」：要不要、它的大小，以及"收录哪些手册"按 mod 分组。
    /// 功能条目一个不少，仍走游戏自带的 ConfigEntry/应用/重置，改完照常保存。
    /// 其它 mod 的设置窗口完全走原版（Prefix 直接放行）。
    /// </summary>
    internal static class ConfigUI
    {
        private const string TabSizes = "cfg_tab_sizes";
        private const string TabOmni = "cfg_tab_omni";
        private const string PickHeader = "cfg_pick_header";

        private static readonly MethodInfo ApplyMethod = AccessTools.Method(typeof(ModConfigWindow), "Apply");
        private static readonly MethodInfo ResetMethod = AccessTools.Method(typeof(ModConfigWindow), "ResetWidgets");

        private static string T(string key, string fallback) { return BookFactory.Text(key, fallback); }

        /// <summary>构建本 mod 的设置窗口。结构与原版 Open 对齐，只把中间的条目区换成标签页。</summary>
        internal static CustomUIWindow Build(LoadedMod mod)
        {
            // 主菜单的模组列表里也能打开设置，而文档扫描原本只在读档后跑；
            // 这里先兜底扫一次，让"我要万能手册"和手册清单在打开窗口时就存在。
            // 1.7.7：打开窗口时走完整扫描（造探针补真实显示名）——这是唯一需要名字的时机，
            // 把这段开销从每次读档挪到这里。force=true 确保覆盖读档时的轻量占位条目。
            DocCatalog.ScanAndDefine();
            ModConfig config = ModConfig.Find(mod.Manifest.ID);
            if (config == null || !config.HasEntries)
            {
                return null;
            }
            CustomUIManager manager = CustomUIManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[BETTERHB] config ui: no CustomUIManager");
                return null;
            }

            bool inStore = StoreUIManager.Instance != null;
            string layerId = inStore ? "meta" : "menu";
            CustomUIWindow window = null;
            CustomUIBuilder b = manager
                .CreateWindow("mod_config:" + mod.Manifest.ID, mod.Manifest.Name, layerId, manager.optionUIPrefab)
                .SetCloseOnEscape(!inStore);

            b.BeginScrollFlexible();
            b.BeginTabs();

            // ---- 标签页 1：六本基础册子的宽/高同行 ----
            b.BeginTab(T(TabSizes, "册子大小"));
            // 自动整理总开关放在这一页顶部：关掉后东西留在档案箱，可做装饰。
            DrawAutoFile(b, config);
            b.AddSeparator(null, 6);
            for (int i = 0; i < Books.All.Length; i++)
            {
                BookDef def = Books.All[i];
                if (def.Id == Books.OmniId)
                {
                    continue;   // 万能手册放到第二页
                }
                DrawSizeRow(b, config, def);
            }
            b.End();

            // ---- 标签页 2：万能手册 ----
            b.BeginTab(T(TabOmni, "万能手册"));
            DrawWantOmni(b, config);
            BookDef omni = Books.Find(Books.OmniId);
            if (omni != null)
            {
                DrawSizeRow(b, config, omni);
            }
            b.AddSeparator(null, 6);
            b.BeginRow(4f, TextAnchor.MiddleLeft);
            b.AddHeader(T(PickHeader, "收录哪些手册（勾上的才会进万能手册）"));
            b.End();
            DrawDocSelections(b, config);
            if (DocCatalog.Count == 0)
            {
                b.BeginRow(4f, TextAnchor.MiddleLeft);
                b.AddLabel(T("cfg_doc_hint", "手册清单会在进入存档后自动生成，请把存档载入后再打开本页。"));
                b.End();
            }
            b.End();

            b.EndTabs();
            b.End();

            // ---- 应用 / 重置 / 提示：照抄原版 ----
            b.AddSeparator(null, 2);
            b.BeginRow(12f, TextAnchor.MiddleCenter);
            b.AddFlexibleSpace();
            b.AddButton(LocHelper.GetLocalizedUI("ui_modcfg_reset"), delegate { Call(ResetMethod, window, config); }, "reset");
            b.Last.SetFixedWidth(220f);
            b.AddButton(LocHelper.GetLocalizedMainMenu("option_apply"), delegate { Call(ApplyMethod, window, config); }, "apply");
            b.Last.SetFixedWidth(220f);
            b.AddFlexibleSpace();
            b.End();
            b.BeginRow(0f, TextAnchor.MiddleCenter);
            b.AddFlexibleSpace();
            b.AddWarning(LocHelper.GetLocalizedUI("ui_mods_restart_note"), "restart_note");
            b.AddFlexibleSpace();
            b.End();

            window = b.Show();
            window.Get("restart_note")?.SetVisible(false);
            return window;
        }

        /// <summary>一本册子的宽 + 高画在同一行。控件 id 与原版一致，应用/重置照常读写。</summary>
        private static void DrawSizeRow(CustomUIBuilder b, ModConfig config, BookDef def)
        {
            ConfigEntry cols, rows;
            try
            {
                cols = config.Get(def.Id + "_cols");
                rows = config.Get(def.Id + "_rows");
            }
            catch (Exception)
            {
                return;
            }
            if (cols == null && rows == null)
            {
                return;
            }
            string name = T(def.NameKey, def.DefaultName);
            b.BeginRow(4f, TextAnchor.MiddleLeft);
            if (cols != null)
            {
                DrawSizeControl(b, config, cols, name + " · " + T("cfg_cols", "宽"));
            }
            if (rows != null)
            {
                b.AddSpace(10f);
                DrawSizeControl(b, config, rows, name + " · " + T("cfg_rows", "高"));
            }
            b.End();
        }

        /// <summary>一个容量控件：标签 + 滑块 + 输入框（沿用 Patch_ConfigRow 的 id 约定）。</summary>
        private static void DrawSizeControl(CustomUIBuilder b, ModConfig config, ConfigEntry raw, string label)
        {
            ConfigEntry<int> entry;
            if (!Settings.TryGetEntry(raw.Key, out entry))
            {
                return;
            }
            int min = Mathf.RoundToInt(raw.Min);
            int max = Mathf.RoundToInt(raw.Max);
            b.AddLabel(label);

            CustomUIElement input = null;
            CustomUIElement slider = null;
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
            b.Last.SetFixedWidth(120f);
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
            b.Last.SetFixedWidth(55f);
            input = b.Last;
        }

        /// <summary>自动整理总开关：与 want_omni 同款写法，复用原版 Apply/Reset 读写。</summary>
        private static void DrawAutoFile(CustomUIBuilder b, ModConfig config)
        {
            ConfigEntry raw;
            try { raw = config.Get(DocCatalog.AutoFileKey); } catch (Exception) { raw = null; }
            if (raw == null)
            {
                return;
            }
            bool initial = raw.BoxedValue is bool && (bool)raw.BoxedValue;
            string tip = T("cfg_auto_file", "放入档案箱后自动整理");
            b.BeginRow(4f, TextAnchor.MiddleLeft);
            b.AddLabel(tip);
            b.AddFlexibleSpace();
            b.AddToggle("", initial, null, raw.Key);
            b.End();
        }

        private static void DrawWantOmni(CustomUIBuilder b, ModConfig config)
        {
            ConfigEntry raw;
            try { raw = config.Get(DocCatalog.WantOmniKey); } catch (Exception) { raw = null; }
            if (raw == null)
            {
                return;
            }
            bool initial = raw.BoxedValue is bool && (bool)raw.BoxedValue;
            string tip = T("cfg_want_omni", "我要万能手册（开门时放柜台）");
            b.BeginRow(4f, TextAnchor.MiddleLeft);
            b.AddLabel(tip);
            b.AddFlexibleSpace();
            b.AddToggle("", initial, null, raw.Key);
            b.End();
        }

        /// <summary>所有 docsel_ 勾选，按 section（即来源 mod）分组，各节一个标题。</summary>
        private static void DrawDocSelections(CustomUIBuilder b, ModConfig config)
        {
            // 保持 section 首次出现的顺序。
            List<string> sections = new List<string>();
            Dictionary<string, List<ConfigEntry>> grouped = new Dictionary<string, List<ConfigEntry>>();
            for (int i = 0; i < config.Entries.Count; i++)
            {
                ConfigEntry e = config.Entries[i];
                if (e == null || e.Key == null || !e.Key.StartsWith(DocCatalog.SelectPrefix, StringComparison.Ordinal))
                {
                    continue;
                }
                string sec = e.Section ?? "";
                List<ConfigEntry> list;
                if (!grouped.TryGetValue(sec, out list))
                {
                    list = new List<ConfigEntry>();
                    grouped[sec] = list;
                    sections.Add(sec);
                }
                list.Add(e);
            }

            for (int s = 0; s < sections.Count; s++)
            {
                string sec = sections[s];
                b.BeginRow(4f, TextAnchor.MiddleLeft);
                b.AddHeader(sec);
                b.End();
                b.AddSeparator(null, 2);
                List<ConfigEntry> list = grouped[sec];
                for (int i = 0; i < list.Count; i++)
                {
                    ConfigEntry e = list[i];
                    string label = e.Label != null ? L(e) : e.Key.Substring(DocCatalog.SelectPrefix.Length);
                    b.BeginRow(4f, TextAnchor.MiddleLeft);
                    b.AddLabel(label);
                    b.AddFlexibleSpace();
                    b.AddToggle("", e.BoxedValue is bool && (bool)e.BoxedValue, null, e.Key);
                    b.End();
                }
                b.AddSpace(8f);
            }
        }

        private static string L(ConfigEntry e)
        {
            try { return e.Label(); } catch (Exception) { return e.Key; }
        }

        /// <summary>反射调用原版私有的 Apply / ResetWidgets，保证保存/重置逻辑一字不改。</summary>
        private static void Call(MethodInfo method, CustomUIWindow window, ModConfig config)
        {
            try
            {
                if (method != null)
                {
                    method.Invoke(null, new object[] { window, config });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] config ui call " + (method == null ? "?" : method.Name) + ": " + e.Message);
            }
        }
    }

    // 只接管本 mod；其它 mod 的设置窗口一行不改。
    [HarmonyPatch(typeof(ModConfigWindow), "Open")]
    internal static class Patch_ConfigOpen
    {
        private static bool Prefix(LoadedMod mod, ref CustomUIWindow __result)
        {
            try
            {
                if (mod == null || mod.Manifest == null || mod.Manifest.ID != ModEntry.ID)
                {
                    return true;
                }
                CustomUIWindow built = ConfigUI.Build(mod);
                if (built == null)
                {
                    // 自建失败就回退原版，不让设置窗口打不开。
                    return true;
                }
                __result = built;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] config ui open failed, fallback to vanilla: " + e);
                return true;
            }
        }
    }
}
