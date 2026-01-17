using Game.Prefabs;
using Game.UI;
using System;
using System.Reflection;
using Unity.Entities;

namespace BetterChineseNames
{
    /// <summary>
    /// Harmony patches for localization system
    /// 在游戏获取本地化字符串之前，先从我们的字典中查找
    /// </summary>
    public static class LocalizationPatches
    {
        // 缓存 GetId 方法的委托（比 MethodInfo.Invoke 更快）
        private delegate string GetIdDelegate(NameSystem instance, Entity entity, bool useCustomName);
        private static GetIdDelegate s_GetIdFunc;
        private static bool s_GetIdInitialized;

        /// <summary>
        /// Prefix patch for UILocalizationManager.Translate
        /// 在 UI 翻译之前先检查我们的字典
        /// </summary>
        public static bool Prefix_Translate(string key, cohtml.Net.ILocalizationManager.TranslationData data)
        {
            if (I18n.CurrentLocaleDictionary.TryGetValue(key, out string value))
            {
                data.Set(value);
                return false; // 跳过原方法
            }
            return true; // 继续执行原方法
        }

        /// <summary>
        /// Prefix patch for NameSystem.GetRenderedLabelName
        /// 在获取实体标签名称之前先检查我们的字典
        /// </summary>
        public static bool Prefix_GetRenderedLabelName(NameSystem __instance, Entity entity, ref string __result)
        {
            // 先尝试获取自定义名称（保持原逻辑）
            if (__instance.TryGetCustomName(entity, out string customName))
            {
                __result = customName;
                return false;
            }

            // 使用缓存的委托获取 ID（GetId 是私有方法）
            string id = GetId(__instance, entity);
            if (id == null)
            {
                return true; // 反射失败，继续执行原方法
            }

            // 先从我们的字典中查找
            if (I18n.CurrentLocaleDictionary.TryGetValue(id, out string value))
            {
                __result = value;
                return false; // 跳过原方法
            }

            // 没找到，继续执行原方法
            return true;
        }

        /// <summary>
        /// Postfix patch for RandomLocalization.GetLocalizationIndexCount
        /// 如果我们有自定义的 indexCount，就用我们的值替换返回值
        /// 这样可以修改随机名称池的大小
        /// </summary>
        public static void Postfix_GetLocalizationIndexCount(PrefabBase prefab, string id, ref int __result)
        {
            Mod.log.Info($"Postfix_GetLocalizationIndexCount: {id}, {__result}");
            // 如果我们有自定义的 indexCount，使用我们的值
            if (id != null && I18n.CustomIndexCounts.TryGetValue(id, out int customCount))
            {
                __result = customCount;
                Mod.log.Info($"Postfix_GetLocalizationIndexCount: {id}, {__result} -> {customCount}");
            }
        }

        /// <summary>
        /// 通过缓存的委托调用 NameSystem.GetId 私有方法
        /// </summary>
        private static string GetId(NameSystem instance, Entity entity)
        {
            try
            {
                if (!s_GetIdInitialized)
                {
                    s_GetIdInitialized = true;
                    var methodInfo = typeof(NameSystem).GetMethod(
                        "GetId",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new[] { typeof(Entity), typeof(bool) },
                        null);

                    if (methodInfo != null)
                    {
                        // 创建开放实例委托（比 Invoke 快得多）
                        s_GetIdFunc = (GetIdDelegate)Delegate.CreateDelegate(
                            typeof(GetIdDelegate), methodInfo);
                    }
                }

                return s_GetIdFunc?.Invoke(instance, entity, true);
            }
            catch
            {
                // 忽略反射异常
            }
            return null;
        }
    }
}
