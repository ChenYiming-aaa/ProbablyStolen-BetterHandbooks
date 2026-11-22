using System;
using UnityEngine;

namespace BetterHandbooks
{
    /// <summary>
    /// 更好的手册
    /// 补上游戏缺失的收纳工具：鉴定手册、彩票手册、生产手册、电话簿。
    /// 每本册子都由对得上号的人物上门送来；不新增数值、不改原版物品定义。
    /// </summary>
    public class ModEntry : IMod
    {
        internal const string ID = "better.handbooks";

        /// <summary>改名前的 mod id，用来读取旧的存档标记。</summary>
        internal const string LegacyId = "inspection.handbook";

        public void Init(ModManifest manifest)
        {
            // 必须尽早订阅：ModItemDirectory 是场景里的组件，初始化时才会发这个事件，
            // 晚于 OnEnable 订阅就永远收不到，物品也就注册不上。
            ModHook.OnModItemDirectoryInit += BookFactory.Register;
        }

        public void OnEnable()
        {
            // 设置项要在模组列表建立之前登记，否则列表里不会出现「设置」入口。
            Settings.Define();
            // 招牌箱不上门送，改在博士的柜台上卖。
            Shop.Hook();
            // 万能手册：勾了「我要」后开门直接放柜台。
            CounterDelivery.Hook();
            ModHook.OnGameLoadedLate += OnLoaded;
            Debug.Log("[BETTERHB] enabled " + ID);
        }

        public void OnDisable()
        {
            Shop.Unhook();
            CounterDelivery.Unhook();
            ModHook.OnGameLoadedLate -= OnLoaded;
            // 注意：这里刻意不退订 BookFactory.Register。
            // 物品工厂是「让存档里七件手册能被认出来」的数据层注册，不是行为开关；
            // Init 只在 DLL 加载时调用一次，若在这里退订，玩家在模组菜单停用再启用后，
            // Register 永远不会重新订阅，读档时这些 identifier 找不到工厂 →
            // 游戏按「未知 ID」处理，七件手册连同里面装的东西全变成紫色「?」占位物品。
            // 真正该随停用关闭的送货/柜台/自动整理钩子已在上面三行处理。
            Debug.Log("[BETTERHB] disabled");
        }

        private static void OnLoaded()
        {
            try
            {
                bool ok = true;
                for (int i = 0; i < Books.All.Length; i++)
                {
                    if (DirectoryMaster.GetFactory<GameItem>(Books.All[i].Id) == null)
                    {
                        ok = false;
                        Debug.LogWarning("[BETTERHB] factory missing: " + Books.All[i].Id);
                    }
                }
                Debug.Log("[BETTERHB] all factories registered: " + ok);
                // 物品目录已全部注册：扫描所有 DOCUMENT 按 mod 分组，动态生成勾选框。
                // 模组设置窗口是打开时才构建条目，所以这时追加来得及。
                // 读档后强制重扫：补上主菜单阶段可能还没注册完的 mod 物品目录。
                DocCatalog.ScanAndDefine(true);
                // 老档的档案箱在 OnEnable 前就造好了，工厂 Postfix 没赶上，这里补挂自动整理。
                AutoFile.AttachExisting();
                Grant.Run();
                Diagnostics.RunOnce();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BETTERHB] onload failed: " + e);
            }
        }
    }
}
