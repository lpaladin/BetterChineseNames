using System;
using System.IO;
using System.Reflection;
using System.Text;
using Colossal.IO.AssetDatabase;
using Colossal.Localization;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.UI;
using Game.UI.Localization;
using HarmonyLib;
using Unity.Entities;

namespace BetterChineseNames
{
    public class Mod : IMod
    {
        public const string ModId = nameof(BetterChineseNames);
        public static ILog log = LogManager.GetLogger($"{ModId}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        public static string ModPath { get; private set; }
        /// <summary>
        /// 用户自定义翻译的存放目录（位于 ModsData 下）
        /// </summary>
        public static string ModsDataPath { get; private set; }
        private Setting m_Setting;
        private Harmony m_Harmony;

        /// <summary>
        /// 获取 ModsData 目录路径
        /// </summary>
        private static string GetModsDataPath()
        {
            return Path.Combine(Colossal.PSI.Environment.EnvPath.kUserDataPath, "ModsData", ModId);
        }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                log.Info($"Current mod asset at {asset.path}");
                ModPath = Path.GetDirectoryName(asset.path);
                
                // 设置 ModsData 路径（用于存放用户自定义翻译）
                ModsDataPath = GetModsDataPath();
                log.Info($"ModsData path: {ModsDataPath}");
                
                var localesPath = Path.Combine(ModPath, "Locales");

                // 初始化 I18n 系统
                I18n.Initialize(localesPath);
            }

            // 应用 Harmony patch 来拦截本地化字典查询
            ApplyHarmonyPatches();

            // 创建并注册设置
            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();

            // 添加设置界面的简体中文本地化
            GameManager.instance.localizationManager.AddSource("zh-HANS", new LocaleZH(m_Setting));

            // 加载用户保存的设置
            AssetDatabase.global.LoadSettings(nameof(BetterChineseNames), m_Setting, new Setting(this));

            // 根据设置加载翻译
            I18n.LoadFromSettings(m_Setting);
        }

        /// <summary>
        /// 应用 Harmony patches 来拦截本地化字典
        /// </summary>
        private void ApplyHarmonyPatches()
        {
            try
            {
                log.Info("Applying Harmony patches...");
                m_Harmony = new Harmony("BetterChineseNames");

                // 1. Patch UILocalizationManager.Translate 方法（UI 层）
                PatchUITranslate();

                // 2. Patch NameSystem.GetRenderedLabelName 方法（实体名称层）
                PatchGetRenderedLabelName();

                log.Info("All Harmony patches applied.");
            }
            catch (System.Exception ex)
            {
                log.Error(ex, "Failed to apply Harmony patches");
            }
        }

        private void PatchUITranslate()
        {
            var originalMethod = typeof(UILocalizationManager).GetMethod(
                "Translate",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(cohtml.Net.ILocalizationManager.TranslationData) },
                null);

            var prefixMethod = typeof(LocalizationPatches).GetMethod(
                "Prefix_Translate",
                BindingFlags.Public | BindingFlags.Static);

            if (originalMethod != null && prefixMethod != null)
            {
                m_Harmony.Patch(originalMethod, new HarmonyMethod(prefixMethod));
                log.Info("Patched UILocalizationManager.Translate");
            }
            else
            {
                log.Warn($"Failed to patch Translate. Original: {originalMethod != null}, Prefix: {prefixMethod != null}");
            }
        }

        private void PatchGetRenderedLabelName()
        {
            var originalMethod = typeof(NameSystem).GetMethod(
                "GetRenderedLabelName",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(Entity) },
                null);

            var prefixMethod = typeof(LocalizationPatches).GetMethod(
                "Prefix_GetRenderedLabelName",
                BindingFlags.Public | BindingFlags.Static);

            if (originalMethod != null && prefixMethod != null)
            {
                m_Harmony.Patch(originalMethod, new HarmonyMethod(prefixMethod));
                log.Info("Patched NameSystem.GetRenderedLabelName");
            }
            else
            {
                log.Warn($"Failed to patch GetRenderedLabelName. Original: {originalMethod != null}, Prefix: {prefixMethod != null}");
            }
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));

            // 取消 Harmony patches
            if (m_Harmony != null)
            {
                m_Harmony.UnpatchAll("BetterChineseNames");
                m_Harmony = null;
                log.Info("Harmony patches removed.");
            }

            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}
