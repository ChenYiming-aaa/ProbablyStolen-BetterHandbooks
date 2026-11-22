using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterHandbooks
{
    /// <summary>
    /// 一本册子的全部定义：物品 id、格子尺寸、贴图、收纳规则、谁会来送、什么时候该来。
    /// 新增一本册子＝在这里加一个条目，其余流程（注册、修补、赠送、日志）都是通用的。
    /// </summary>
    internal sealed class BookDef
    {
        internal string Id;              // 物品 identifier
        internal string Key;             // 本地化键前缀，如 "lot" -> lot_name / lot_desc / lot_d1..
        internal string DefaultName;
        internal string DefaultDesc;
        internal string[] DialogDefaults;  // 6 句台词，默认值；实际优先读本地化

        internal int Width;              // 内容格子列数
        internal int Height;             // 内容格子行数

        internal string SpriteKey;       // Textures/Items/<key>.png（空的时候）
        internal string SpriteKeyFilled; // 装了东西之后换的图；null = 不换
        internal string FallbackSprite;  // mod 贴图没加载时的原版贴图

        internal string GiverNameKey;    // LocHelper.GetLocalizedClientName 的键
        internal string GiverSprite;     // 空字符串 = 随机商人立绘
        internal bool GiverIsSecurity;   // 立绘取 SpriteDict.GetRandomSecSpriteName（治安部那批人）

        internal long UnitValue;         // 估价/售价。册子是 0；招牌箱卖 1 块
        internal bool ShopOnly;          // true = 不上门赠送，改由别处出售

        internal Func<GameItem, bool> Accept;   // 收纳规则
        internal Func<bool> Ready;              // 什么时候该送（条件满足后次日才来）

        /// <summary>
        /// 同一件物品按情况换一套台词。返回 "g"/"m"/"b" 之类的后缀，键名变成
        /// <Key>_<variant>_d1；返回 null/空 = 所有台词读 <Key>_d1。
        /// </summary>
        internal Func<string> DialogVariant;

        internal string NameKey { get { return Key + "_name"; } }
        internal string DescKey { get { return Key + "_desc"; } }
        internal string DialogKey(int index) { return Key + "_d" + index; }
    }

    /// <summary>
    /// 四本册子的定义与物品清单。所有分类判据都来自反编译核对，不靠猜：
    /// 鉴定纸＝BookHelper.InitPaperGuide；配方书＝InitGuide / InitBookItem；名片＝PaperUIManager.OpenXxxCard。
    /// </summary>
    internal static class Books
    {
        internal const string InspectionId = "inspection_book";
        internal const string LotteryId = "lottery_book";
        internal const string ProductionId = "production_book";
        internal const string PhoneId = "phonebook";
        internal const string ReceiptId = "receipt_book";
        internal const string SignBoxId = "sign_box";
        internal const string OmniId = "omni_book";

        /// <summary>治安部回执单。DOCUMENT，无专属标签，只能按 identifier 认。</summary>
        internal const string SecSlipId = "sec_slip";

        /// <summary>霓虹招牌 id 前缀。原版 10 种：水/食物/医疗/家居/材料/废机/枪械/重枪械/莓果/远征。</summary>
        internal const string SignPrefix = "sign_";

        /// <summary>
        /// 外部 mod 的招牌没有统一的 id 前缀，但经反编译核实都带同一个标签：
        /// SHOWCASE_SPECIAL_ITEM_TAG（值为该招牌指向的商品类型）。原版 8/10 种也带它。
        /// 单靠标签会漏掉 sign_berry / sign_expedition（原版）与 moneylending_sign（外部），
        /// 所以判据是「id 以 sign_ 开头」或「带 showcase 标签且 id 含 sign」的并集。
        /// id 含 sign 这一条同时挡掉 design/assignment 之类误判，且不误收共用 items_sign
        /// 图集但 id 里没有 sign 的黑灯 black_lamp。
        /// </summary>
        private const string ShowcaseTag = "SHOWCASE_SPECIAL_ITEM_TAG";

        /// <summary>鉴定指南纸（7 种）——原版 InitPaperGuide。</summary>
        internal static readonly string[] AppraisalGuides =
        {
            "cigarette_guide", "hand_guide", "aug_id_guide", "stamp_guide",
            "injector_guide", "fuel_guide", "ribwich_guide"
        };

        /// <summary>配方书（5 种）——原版 InitGuide / InitBookItem，讲怎么做东西。</summary>
        internal static readonly string[] RecipeGuides =
        {
            "water_guide", "hydroponic_guide", "chemistry_guide", "ingot_book", "wine_book"
        };

        /// <summary>名片（6 种）——全部走 PaperUIManager.OpenXxxCard。</summary>
        internal static readonly string[] BusinessCards =
        {
            "card_brewer", "card_mining", "card_pharma", "card_chem", "card_rev", "joe_card"
        };

        /// <summary>彩票：单张号码票 + 未兑旧票 + 刮刮乐单张/整叠 + 那张讲中奖概率的信息纸。</summary>
        internal static readonly string[] LotteryItems =
        {
            "lottery_ticket", "lottery_ticket_old", "scratcher", "scratcher_stack", "lottery_info"
        };

        /// <summary>回执手册收的东西。只收治安部的 sec_slip：slip / procurement_slip 是任务中间物。</summary>
        internal static readonly string[] SecSlips = { SecSlipId };

        /// <summary>
        /// 别的内容 mod 自带的说明书，按内容分成三组，各归各位：
        ///   - 生产类（配方/种植/制作）→ 默认进生产手册；
        ///   - 鉴定类（验真伪/比对）→ 默认进鉴定手册；
        ///   - 工具书（规则手册/参数目录）→ 没有专属册子，默认进万能手册。
        /// 每组都能在设置里改成"由万能手册收"。它们和鉴定纸一样带 GUIDE_PAPER_TAG，
        /// 不显式处理就会被鉴定手册当成鉴定纸误收。ID 来自各 mod 源码/反编译核对。
        /// </summary>
        internal static readonly string[] ExternalProductionManuals =
        {
            // 酿酒拓展：酿酒手册（配方自动收录）、酿酒水培手册（水培/果实品质/返种）
            "be_brewing_manual", "be_brewing_hydroponic_guide",
            // 香烟拓展：香烟手册（种植/制烟/交易）、香烟水培手册（水培/种子基因）
            "te_tobacco_manual", "te_tobacco_hydroponic_guide",
            // 深空装机：电脑装机与交易手册
            "pcrepair.computer_build_guide"
        };

        internal static readonly string[] ExternalAppraisalManuals =
        {
            // 香烟拓展：果味香烟真伪鉴定手册（克隆自原版鉴定纸 ribwich_guide）
            "te_cigarette_inspection_guide"
        };

        internal static readonly string[] ExternalToolBooks =
        {
            // 枪匠拓展：枪匠手册（规则/流程/价格）、枪械目录（型号/配件/口径）
            "gse_manual", "gse_catalog"
        };

        /// <summary>全部外部手册的合集，供供应商日志和万能手册的送达条件用。</summary>
        internal static readonly string[] ThirdPartyManuals = Combine(
            ExternalProductionManuals, ExternalAppraisalManuals, ExternalToolBooks);

        private static string[] Combine(params string[][] groups)
        {
            int total = 0;
            for (int g = 0; g < groups.Length; g++)
            {
                total += groups[g].Length;
            }
            string[] all = new string[total];
            int k = 0;
            for (int g = 0; g < groups.Length; g++)
            {
                for (int i = 0; i < groups[g].Length; i++)
                {
                    all[k++] = groups[g][i];
                }
            }
            return all;
        }

        /// <summary>借了鉴定标签、但不是鉴定物的东西（彩票信息纸只归彩票手册）。</summary>
        private static readonly HashSet<string> NotAppraisal = new HashSet<string> { "lottery_info", "labeler" };

        /// <summary>没有 GUIDE_PAPER_TAG，但确实属于鉴定流程的物品。</summary>
        private static readonly HashSet<string> AppraisalTools = new HashSet<string>
        {
            "cigarette_color", "cert1", "cert2", "cert3", "cert4"
        };

        private const string GuideTag = "GUIDE_PAPER_TAG";
        private const string MagnifierTag = "MAGNIFYING_GLASS";

        /// <summary>给别的 mod 作者用的约定标签：名片打上它就一定会被电话簿收。</summary>
        internal const string CardTag = "BETTERHB_CARD_TAG";

        internal static readonly BookDef[] All = new BookDef[]
        {
            new BookDef
            {
                Id = InspectionId, Key = "ins",
                DefaultName = "鉴定手册",
                DefaultDesc = "专门收集整理检查和估价用的所有指南的工具册子。非常方便！",
                DialogDefaults = new[]
                {
                    "听说你入了鉴定协会。",
                    "听着，我给你准备了这个。",
                    "<i>他从柜台底下抽出一本册子，推到你面前。</i>",
                    "检查和估价的那些指南，全塞进去。",
                    "别散在柜台上。散着，迟早被你自己当垃圾扔了。",
                    "眼睛放毒点，菜鸟。这行靠的就是这个。"
                },
                Width = 10, Height = 10,
                SpriteKey = "handbook", SpriteKeyFilled = "handbook_full", FallbackSprite = "tutorial_book",
                GiverNameKey = "name_the_mentor", GiverSprite = "mentor",
                Accept = IsAppraisalItem,
                Ready = AppraisalReady
            },
            new BookDef
            {
                Id = LotteryId, Key = "lot",
                DefaultName = "彩票手册",
                DefaultDesc = "专门收集整理没兑的和兑过的彩票的册子。非常方便！",
                DialogDefaults = new[]
                {
                    "你这柜台底下，攒了一堆没兑的刮刮乐吧。",
                    "我数过了。压在一个抽屉里，跟废纸一样。",
                    "<i>他把一本红皮的小册子推过柜台。</i>",
                    "彩票手册。没兑的、兑过的，分开放。",
                    "星期六开奖。到时候翻一遍，别又翻抽屉。",
                    "中了大奖，记得请我喝一杯。"
                },
                Width = 6, Height = 6,
                SpriteKey = "lottery", SpriteKeyFilled = "lottery_full", FallbackSprite = "tutorial_book",
                GiverNameKey = "name_lottery_representative", GiverSprite = "",
                Accept = candidate => IsAnyOf(candidate, LotteryItems),
                Ready = () => ShipScan.Count(LotteryItems) >= 1
            },
            new BookDef
            {
                Id = ProductionId, Key = "prod",
                DefaultName = "生产手册",
                DefaultDesc = "专门收集整理生产配方书的册子。非常方便！",
                DialogDefaults = new[]
                {
                    "配方书多起来了吧。",
                    "水、水培、化学、金属锭、酒——摊在柜台上，占地。",
                    "<i>他从夹克内袋抽出一本厚一点的册子。</i>",
                    "生产手册。跟鉴定那本分开放。",
                    "一本记怎么验货，一本记怎么做货。别混。",
                    "手稳点，菜鸟。"
                },
                Width = 10, Height = 10,
                SpriteKey = "production", SpriteKeyFilled = "production_full", FallbackSprite = "tutorial_book",
                GiverNameKey = "name_the_mentor", GiverSprite = "mentor",
                // 原版五本配方书 + 别的内容 mod 的生产类手册归生产手册。
                // 1.6.0：即使玩家在设置里把某本勾给了万能手册，这里也照样收——
                // 两本册子可以同时认领同一本手册，实际进哪本由玩家拖到哪本决定。
                Accept = candidate =>
                    IsAnyOf(candidate, RecipeGuides) || IsAnyOf(candidate, ExternalProductionManuals),
                Ready = () => ShipScan.Count(RecipeGuides) >= 1
                              || ShipScan.Count(ExternalProductionManuals) >= 1
            },
            new BookDef
            {
                Id = PhoneId, Key = "phn",
                DefaultName = "电话簿",
                DefaultDesc = "专门收集整理各路生意的名片的册子。非常方便！",
                DialogDefaults = new[]
                {
                    "名片收了一堆，要用的时候一张都找不着吧。",
                    "酿酒、采矿、制药、化工——全在你抽屉里躺着。",
                    "<i>他拍了拍柜台，把一本薄册子放下。</i>",
                    "电话簿。谁在哪一页，翻就有。",
                    "想找谁，别翻抽屉。翻这个。",
                    "打过去的时候，别提我。"
                },
                // 图标 32×48 占 2×3 格；内容默认 6×6，设置里可改。
                Width = 6, Height = 6,
                SpriteKey = "phonebook", SpriteKeyFilled = "phonebook_full", FallbackSprite = "tutorial_book",
                GiverNameKey = "name_supplier", GiverSprite = "",
                Accept = IsBusinessCard,
                Ready = () => ShipScan.Count(BusinessCards) >= 1
            },
            new BookDef
            {
                Id = ReceiptId, Key = "rcp",
                DefaultName = "回执手册",
                DefaultDesc = "专门收集整理治安部回执单的册子。非常方便！",
                // 兜底台词 = 好档（g）。实际按 DialogVariant 读 rcp_g_* / rcp_m_* / rcp_b_*。
                DialogDefaults = new[]
                {
                    "{0} 张回执，一次都没报错。这行里少见。",
                    "通缉名单上，你举报了 {2} 个。治安部记得每一个。",
                    "<i>他赞许地看着你，顺手抽出一本册子。</i>",
                    "这是回执手册。以后每一张都顺手夹进去。",
                    "这是我们合作的象征。"
                },
                // 图标 32×48 占 2×3 格；内容默认 6×6，设置里可改。
                Width = 6, Height = 6,
                SpriteKey = "receipt", SpriteKeyFilled = "receipt_full", FallbackSprite = "tutorial_book",
                GiverNameKey = "name_security_officer", GiverSprite = "",
                GiverIsSecurity = true,
                DialogVariant = ReceiptVariant,
                Accept = IsSecSlip,
                Ready = () => ShipScan.Count(SecSlips) >= 1
            },
            new BookDef
            {
                // 箱子：不上门送，只在博士那儿卖 1 块；买过一次就永久下架。
                Id = SignBoxId, Key = "sgn",
                DefaultName = "招牌箱",
                DefaultDesc = "专门收集整理霓虹招牌的箱子。便宜又结实。博士出品必属精品",
                DialogDefaults = new string[0],
                // 箱子图标 48×32 占 3×2 格；内容默认 10×8。装了招牌换成"塞满灯牌"那张。
                Width = 10, Height = 8,
                SpriteKey = "signbox", SpriteKeyFilled = "signbox_full", FallbackSprite = "tutorial_book",
                GiverNameKey = "", GiverSprite = "",
                UnitValue = 1L,
                ShopOnly = true,
                Accept = IsSign,
                Ready = () => false
            },
            new BookDef
            {
                // 收别的内容 mod 自带的说明书。触发条件是有这类手册出现在店里，
                // 没装那些 mod 就永远不来，不占现有册子的赠送名额（排在最后）。
                Id = OmniId, Key = "omni",
                DefaultName = "万能手册",
                DefaultDesc = "专门收集整理别处来的手册与说明书的册子。非常方便！",
                DialogDefaults = new[]
                {
                    "你店里那几本说明书，堆得比账本还高。",
                    "酿酒一本、香烟一本，修枪的、装电脑的，又各有一本。",
                    "<i>他抱着一摞册子放下，最上面那本写着「万能」。</i>",
                    "别家的手册，也归你管。",
                    "翻得开、塞得进，就不会丢。",
                    "别的册子各管各的。这本，管剩下的。"
                },
                Width = 10, Height = 10,
                SpriteKey = "omni", SpriteKeyFilled = "omni_full", FallbackSprite = "tutorial_book",
                GiverNameKey = "name_supplier", GiverSprite = "",
                Accept = IsOmniDocument,
                // 1.5.0：万能手册不再由供货商上门送——玩家在设置里勾了「我要万能手册」后，
                // 由 CounterDelivery 在开门时直接放柜台。这里恒不触发赠予流程。
                Ready = () => false
            }
        };

        internal static BookDef Find(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return null;
            }
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == identifier)
                {
                    return All[i];
                }
            }
            return null;
        }

        internal static bool IsOurs(string identifier)
        {
            return Find(identifier) != null;
        }

        // ---- 鉴定手册的收纳规则 ----

        private static bool IsAppraisalItem(GameItem candidate)
        {
            if (candidate == null)
            {
                return false;
            }
            string id = candidate.identifier;
            // 1.7.3：外部 mod 的招牌可能也带 GUIDE_PAPER_TAG（如 pcrepair.sign_computer），
            // 自动整理按册子顺序走，鉴定手册排在招牌箱前面，不挡就会被鉴定手册抢走。
            // 招牌一律归招牌箱。
            if (IsSignItem(candidate))
            {
                return false;
            }
            // 1.6.0：即使勾给了万能手册，鉴定手册也照收——同一个人可以被两本册子认领。
            if (NotAppraisal.Contains(id))
            {
                return false;
            }
            // 5 本配方书（水/水培/化学/金属锭/酒）同样带 GUIDE_PAPER_TAG——
            // 原版这个标签是"纸制指南"的统称，不是"鉴定纸"的专名。它们在 InitGuide 里，
            // 属于生产手册；不排除的话会被鉴定手册先收走。
            if (IsAnyOf(candidate, RecipeGuides))
            {
                return false;
            }
            // 外部手册：生产类和工具书不进鉴定手册；鉴定类（验真伪）无条件进这里。
            // 1.4.2 起写死，不再看万能手册的开关。
            if (IsAnyOf(candidate, ExternalProductionManuals) || IsAnyOf(candidate, ExternalToolBooks))
            {
                return false;
            }
            if (IsAnyOf(candidate, ExternalAppraisalManuals))
            {
                return true;
            }
            if (AppraisalTools.Contains(id))
            {
                return true;
            }
            return candidate.IsTag(GuideTag) || candidate.IsTag(MagnifierTag);
        }

        /// <summary>
        /// 万能手册（1.5.0 起）：收什么完全由玩家在设置里"按具体名字勾选"决定。
        /// DocCatalog 扫描出所有 DOCUMENT、按 mod 分组列成勾选框，玩家勾哪本这里就收哪本。
        /// 默认全不勾——不替玩家动物品。
        ///
        /// 1.6.0：不再和专属册子互斥。一本手册可以同时被专属册子和万能手册认领，
        /// 玩家拖到哪本就进哪本；自动整理时按"专属优先、万能兜底"的顺序选（见 AutoFile）。
        ///
        /// 防线仍然保留：本 mod 自己的册子不收（防套娃）、任务物不收（Tool 清单里
        /// 根本不会给任务物生成勾选框，这里再挡一层）。
        /// </summary>
        private static bool IsOmniDocument(GameItem candidate)
        {
            if (candidate == null)
            {
                return false;
            }
            string id = candidate.identifier;
            if (string.IsNullOrEmpty(id) || IsOurs(id))
            {
                return false;
            }
            if (!candidate.IsGameItemType("DOCUMENT") || IsTaskCriticalId(id))
            {
                return false;
            }
            // 唯一判据：玩家是否勾选了这本手册。
            return DocCatalog.IsSelected(id);
        }

        /// <summary>
        /// 任务关键文档：塞进册子可能卡住交付，一律不收。
        /// DocCatalog 也用它把任务物排除出勾选清单。
        /// </summary>
        internal static bool IsTaskCriticalId(string id)
        {
            string[] bad = { "contract", "certificate", "archive", "commission", "quest", "permit", "license", "order", "slip" };
            for (int i = 0; i < bad.Length; i++)
            {
                if (id.IndexOf(bad[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 名片判定。原版六张按 identifier；别的 mod 的名片没有统一标签，所以再放行两类：
        /// 显式打了 BETTERHB_CARD_TAG 的，和 identifier 里带 card 的文档类物品。
        /// 加 DOCUMENT 这个前提，是为了不误收 cardboard 一类杂物。
        /// </summary>
        private static bool IsBusinessCard(GameItem candidate)
        {
            if (candidate == null)
            {
                return false;
            }
            if (IsAnyOf(candidate, BusinessCards) || candidate.IsTag(CardTag))
            {
                return true;
            }
            string id = candidate.identifier;
            return id != null
                   && id.IndexOf("card", StringComparison.OrdinalIgnoreCase) >= 0
                   && candidate.IsGameItemType("DOCUMENT");
        }

        /// <summary>回执单：identifier 固定，原版没有标签可认（只有 DOCUMENT）。</summary>
        private static bool IsSecSlip(GameItem candidate)
        {
            return candidate != null && candidate.identifier == SecSlipId;
        }

        /// <summary>
        /// 招牌判定（1.7.3）：原版按 sign_ 前缀（sign_berry / sign_expedition 无 showcase 标签，
        /// 必须保留前缀分支）；外部 mod 的招牌 id 不带该前缀，但带 SHOWCASE_SPECIAL_ITEM_TAG，
        /// 且 id 里含 sign（be_brewery_sign / te_tobacco_sign / moneylending_sign /
        /// pcrepair.sign_computer / gse_sign_gunsmith 均已反编译核实）。
        /// 两条并集，排除自己的招牌箱（sign_box 也以 sign_ 开头，别让箱子装箱子）。
        /// </summary>
        internal static bool IsSignItem(GameItem candidate)
        {
            if (candidate == null)
            {
                return false;
            }
            string id = candidate.identifier;
            if (string.IsNullOrEmpty(id) || IsOurs(id))
            {
                return false;
            }
            if (id.StartsWith(SignPrefix, StringComparison.Ordinal))
            {
                return true;
            }
            return id.IndexOf("sign", StringComparison.OrdinalIgnoreCase) >= 0
                   && candidate.IsTag(ShowcaseTag);
        }

        private static bool IsSign(GameItem candidate)
        {
            return IsSignItem(candidate);
        }

        private static bool IsAnyOf(GameItem candidate, string[] ids)
        {
            if (candidate == null)
            {
                return false;
            }
            string id = candidate.identifier;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == id)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 治安官按玩家的"举报成绩"换一套台词。
        ///
        /// 能用到的真数据只有两项：
        ///   - wantedStatus 里 value == 1 的条目数：举报成功并归案的通缉犯，累计、不回退；
        ///   - PlayerStore.wrongfulReportCount：当前记着的错报次数，上限 5，且每 5 天 -1。
        /// 没有"成功举报总次数"和"累计赏金"字段，回执张数也不能当成功数用——
        /// **错报同样会生成 sec_slip**（Wrongful Report 分支里也是 DirectoryMaster.Item("sec_slip")）。
        ///
        /// 所以分档看的是"抓到了几个"和"当前还挂着几次错报"，不是精确正确率。
        /// </summary>
        private static string ReceiptVariant()
        {
            int wrong = 0;
            try
            {
                PlayerStore store = PlayerStore.Instance;
                if (store != null)
                {
                    wrong = store.wrongfulReportCount;
                }
            }
            catch (Exception)
            {
            }
            if (wrong >= 2)
            {
                return "b";
            }
            return wrong == 0 && WantedClosed() >= 3 ? "g" : "m";
        }

        /// <summary>通缉名单上已归案的人数（wantedStatus 里被标成 1 的）。</summary>
        internal static int WantedClosed()
        {
            try
            {
                PlayerStore store = PlayerStore.Instance;
                if (store == null || store.wantedStatus == null)
                {
                    return 0;
                }
                int count = 0;
                foreach (KeyValuePair<string, int> pair in store.wantedStatus)
                {
                    if (pair.Value == 1)
                    {
                        count++;
                    }
                }
                return count;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>把台词里的 {0} 回执张数 / {1} 错报次数 / {2} 已归案数填进去。</summary>
        internal static string FillNumbers(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0)
            {
                return text;
            }
            int wrong = 0;
            try
            {
                PlayerStore store = PlayerStore.Instance;
                if (store != null)
                {
                    wrong = store.wrongfulReportCount;
                }
            }
            catch (Exception)
            {
            }
            return text
                .Replace("{0}", ShipScan.Count(SecSlips).ToString())
                .Replace("{1}", wrong.ToString())
                .Replace("{2}", WantedClosed().ToString());
        }

        /// <summary>
        /// 鉴定手册的时机：鉴定服务未开启不送；当天刚加入则次日再送（别打断委员会那段对话）；
        /// 老档早开过服务，当天就能送。
        /// </summary>
        private static bool AppraisalReady()
        {
            PlayerStore store = PlayerStore.Instance;
            if (store == null)
            {
                return false;
            }
            bool introduced = store.isAppraisalIntroduced || store.appraiserLevel > 0;
            if (!introduced)
            {
                return false;
            }
            StoreStation station = StoreStation.Instance;
            if (station == null)
            {
                return false;
            }
            return station.dayCounter > store.appraisalIntroducedDay;
        }
    }
}
