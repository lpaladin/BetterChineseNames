using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI.Widgets;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BetterChineseNames
{
    [FileLocation("ModsSettings\\" + nameof(BetterChineseNames) + "\\" + nameof(BetterChineseNames))]
    [SettingsUITabOrder(kTabNames, kTabCustomize, kTabGeneral)]
    [SettingsUIGroupOrder(kGroupPreset, kGroupRoadNames, kGroupCitizenNames, kGroupCustomize, kGroupDevTools, kGroupGeneral)]
    [SettingsUIShowGroupName(kGroupPreset, kGroupRoadNames, kGroupCitizenNames, kGroupCustomize, kGroupDevTools)]
    public class Setting : ModSetting
    {
        // Tab 定义
        public const string kTabNames = "Names";
        public const string kTabCustomize = "Customize";
        public const string kTabGeneral = "General";

        // Group 定义
        public const string kGroupPreset = "PresetGroup";
        public const string kGroupRoadNames = "RoadNames";
        public const string kGroupCitizenNames = "CitizenNames";
        public const string kGroupCustomize = "CustomizeGroup";
        public const string kGroupDevTools = "DevTools";
        public const string kGroupGeneral = "GeneralGroup";

        public Setting(IMod mod) : base(mod)
        {
        }

        #region 预置翻译选择

        /// <summary>
        /// 自定义模式的特殊值
        /// </summary>
        public const string kCustomPreset = "__custom__";

        /// <summary>
        /// 默认预置名称
        /// </summary>
        public const string kDefaultPreset = "甲";

        private string m_PresetSelection = kDefaultPreset;

        [SettingsUISection(kTabNames, kGroupPreset)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetPresetDropdownItems))]
        public string PresetSelection
        {
            get => m_PresetSelection;
            set
            {
                if (m_PresetSelection != value)
                {
                    m_PresetSelection = value;
                    // 切换预置时立刻重新加载翻译
                    I18n.LoadFromSettings(this);
                    Mod.log.Info($"预置翻译已切换为: {value}");
                }
            }
        }

        /// <summary>
        /// 是否选择了自定义模式
        /// </summary>
        public bool IsCustomMode => PresetSelection == kCustomPreset;

        /// <summary>
        /// 获取当前预置翻译的路径（相对于 NamesTranslation 目录）
        /// </summary>
        public string GetPresetPath()
        {
            if (IsCustomMode)
            {
                return null;
            }
            return Path.Combine("预置翻译", PresetSelection);
        }

        /// <summary>
        /// 获取可用的预置翻译列表
        /// </summary>
        public static string[] GetAvailablePresets()
        {
            if (string.IsNullOrEmpty(Mod.ModPath))
            {
                return new string[0];
            }

            string presetsDir = Path.Combine(Mod.ModPath, "Locales", "NamesTranslation", "预置翻译");
            if (!Directory.Exists(presetsDir))
            {
                return new string[0];
            }
            
            return Directory.GetDirectories(presetsDir)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name)
                .ToArray();
        }

        /// <summary>
        /// 生成预置翻译下拉选项
        /// </summary>
        public static DropdownItem<string>[] GetPresetDropdownItems()
        {
            var items = new List<DropdownItem<string>>();

            // 添加自定义选项
            items.Add(new DropdownItem<string> { value = kCustomPreset, displayName = "自定义" });

            // 添加从文件夹读取的预置选项
            foreach (var preset in GetAvailablePresets())
            {
                items.Add(new DropdownItem<string> { value = preset, displayName = preset });
            }

            return items.ToArray();
        }

        #endregion

        #region 通用设置 Tab

        [SettingsUISection(kTabGeneral, kGroupGeneral)]
        public bool EnableOfficialFixes { get; set; } = true;

        #endregion

        #region 名称翻译 Tab - 道路名称

        [SettingsUISection(kTabNames, kGroupRoadNames)]
        public bool EnableStreetNames { get; set; } = true;

        [SettingsUISection(kTabNames, kGroupRoadNames)]
        public bool EnableAlleyNames { get; set; } = true;

        [SettingsUISection(kTabNames, kGroupRoadNames)]
        public bool EnableHighwayNames { get; set; } = true;

        [SettingsUISection(kTabNames, kGroupRoadNames)]
        public bool EnableBridgeNames { get; set; } = true;

        [SettingsUISection(kTabNames, kGroupRoadNames)]
        public bool EnableDamNames { get; set; } = true;

        [SettingsUISection(kTabNames, kGroupRoadNames)]
        public bool EnableCityNames { get; set; } = true;

        [SettingsUISection(kTabNames, kGroupRoadNames)]
        public bool EnableDistrictNames { get; set; } = true;

        [SettingsUISection(kTabNames, kGroupRoadNames)]
        public bool EnableCompanyNames { get; set; } = true;

        #endregion

        #region 名称翻译 Tab - 市民名称

        [SettingsUISection(kTabNames, kGroupCitizenNames)]
        public bool EnableCitizenMaleNames { get; set; } = true;

        [SettingsUISection(kTabNames, kGroupCitizenNames)]
        public bool EnableCitizenFemaleNames { get; set; } = true;

        [SettingsUISection(kTabNames, kGroupCitizenNames)]
        public bool EnableCitizenMaleSurnames { get; set; } = true;

        [SettingsUISection(kTabNames, kGroupCitizenNames)]
        public bool EnableCitizenFemaleSurnames { get; set; } = true;

        [SettingsUISection(kTabNames, kGroupCitizenNames)]
        public bool EnableCitizenHouseholdSurnames { get; set; } = true;

        [SettingsUISection(kTabNames, kGroupCitizenNames)]
        public bool EnableDogNames { get; set; } = true;

        #endregion

        #region 自定义 Tab

        [SettingsUIButton]
        [SettingsUISection(kTabCustomize, kGroupCustomize)]
        public bool OpenTranslationFolder
        {
            set
            {
                // 打开可编辑的自定义翻译目录（位于 ModsData 下）
                string folderPath = Path.Combine(Mod.ModsDataPath, "NamesTranslation");
                if (Directory.Exists(folderPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folderPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    Mod.log.Warn($"翻译文件夹不存在: {folderPath}");
                }
            }
        }

        [SettingsUIButton]
        [SettingsUISection(kTabCustomize, kGroupCustomize)]
        public bool ReloadTranslations
        {
            set
            {
                I18n.LoadFromSettings(this);
                Mod.log.Info("翻译已重新载入");
            }
        }

        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUISection(kTabCustomize, kGroupCustomize)]
        public bool ResetAllCustomTranslations
        {
            set
            {
                I18n.ResetCustomTranslations();
                I18n.LoadFromSettings(this);
                Mod.log.Info("所有自定义翻译已撤销");
            }
        }

        #endregion

        #region 开发者工具

        [SettingsUIButton]
        [SettingsUISection(kTabCustomize, kGroupDevTools)]
        public bool ExportOriginalNames
        {
            set
            {
                NameExporter.ExportOriginalNames();
            }
        }

        #endregion

        public override void SetDefaults()
        {
            // 预置翻译选择
            PresetSelection = kDefaultPreset;

            // 通用设置
            EnableOfficialFixes = true;

            // 道路名称
            EnableStreetNames = true;
            EnableAlleyNames = true;
            EnableHighwayNames = true;
            EnableBridgeNames = true;
            EnableDamNames = true;
            EnableCityNames = true;
            EnableDistrictNames = true;
            EnableCompanyNames = true;

            // 市民名称
            EnableCitizenMaleNames = true;
            EnableCitizenFemaleNames = true;
            EnableCitizenMaleSurnames = true;
            EnableCitizenFemaleSurnames = true;
            EnableCitizenHouseholdSurnames = true;
            EnableDogNames = true;
        }
    }

    /// <summary>
    /// 设置界面的简体中文本地化
    /// </summary>
    public class LocaleZH : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleZH(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                // 设置页面标题
                { m_Setting.GetSettingsLocaleID(), "更好的中文译名" },

                // Tab 名称
                { m_Setting.GetOptionTabLocaleID(Setting.kTabGeneral), "其他" },
                { m_Setting.GetOptionTabLocaleID(Setting.kTabNames), "名称翻译" },
                { m_Setting.GetOptionTabLocaleID(Setting.kTabCustomize), "自定义" },

                // 分组名称
                { m_Setting.GetOptionGroupLocaleID(Setting.kGroupPreset), "预置翻译" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kGroupGeneral), "通用设置" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kGroupRoadNames), "道路/地点名称" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kGroupCitizenNames), "市民/动物名称" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kGroupCustomize), "自定义翻译" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kGroupDevTools), "开发者工具" },

                // 预置翻译选择
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PresetSelection)), "翻译预置" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PresetSelection)), "选择使用哪套预置翻译。选择\"自定义\"后，请前往\"自定义\"标签页进行配置。" },

                // 通用设置
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableOfficialFixes)), "启用官方翻译修正" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableOfficialFixes)), "修正官方翻译中的一些问题，如\"当前热门\"等" },

                // 道路名称
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableStreetNames)), "街道名" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableStreetNames)), "使用中国化的街道名称" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableAlleyNames)), "小巷名" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableAlleyNames)), "使用中国化的小巷名称" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableHighwayNames)), "高速公路名" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableHighwayNames)), "使用中国化的高速公路名称" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableBridgeNames)), "桥梁名" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableBridgeNames)), "使用中国化的桥梁名称" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableDamNames)), "大坝名" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableDamNames)), "使用中国化的大坝名称" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableCityNames)), "城市名" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableCityNames)), "使用中国化的城市名称" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableDistrictNames)), "行政区名" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableDistrictNames)), "使用中国化的行政区名称" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableCompanyNames)), "公司名" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableCompanyNames)), "使用中国化的公司名称" },

                // 市民名称
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableCitizenMaleNames)), "男性名字" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableCitizenMaleNames)), "使用中国化的男性名字" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableCitizenFemaleNames)), "女性名字" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableCitizenFemaleNames)), "使用中国化的女性名字" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableCitizenMaleSurnames)), "男性姓氏" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableCitizenMaleSurnames)), "使用中国化的男性姓氏" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableCitizenFemaleSurnames)), "女性姓氏" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableCitizenFemaleSurnames)), "使用中国化的女性姓氏" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableCitizenHouseholdSurnames)), "家庭姓氏" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableCitizenHouseholdSurnames)), "使用中国化的家庭姓氏" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableDogNames)), "狗名" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableDogNames)), "使用中国化的狗名" },

                // 自定义
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenTranslationFolder)), "打开翻译文件夹" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenTranslationFolder)), "打开翻译 CSV 文件所在的文件夹，用于自定义翻译" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ReloadTranslations)), "重新载入翻译" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ReloadTranslations)), "从文件重新载入所有翻译" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetAllCustomTranslations)), "撤销所有自定义翻译" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetAllCustomTranslations)), "将 NamesTranslation 文件夹恢复为默认内容" },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.ResetAllCustomTranslations)), "确定要撤销所有自定义翻译吗？这将覆盖您所有的修改。" },

                // 开发者工具
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ExportOriginalNames)), "导出原始名称" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ExportOriginalNames)), "从游戏中导出原始名称翻译到 NamesTranslation 目录。会覆盖所有自定义翻译。" },
            };
        }

        public void Unload()
        {
        }
    }
}
