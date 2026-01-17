using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Colossal.Json;
using Game;
using Game.Companies;
using Game.Economy;
using Game.Prefabs;
using Game.SceneFlow;
using Unity.Collections;
using Unity.Entities;

namespace BetterChineseNames
{
    /// <summary>
    /// UTF-8 with BOM 编码，用于确保 Excel 正确识别中文
    /// </summary>
    internal static class CsvEncoding
    {
        public static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
    }
    /// <summary>
    /// 名称类型的中英文映射（全局共用）
    /// </summary>
    public static class NameTypeMapping
    {
        /// <summary>
        /// 英文前缀到中文名称的映射
        /// </summary>
        public static readonly Dictionary<string, string> ToChinese = new Dictionary<string, string>
        {
            { "ALLEY_NAME", "巷名" },
            { "ANIMAL_NAME_DOG", "狗名" },
            { "BRIDGE_NAME", "桥名" },
            { "CITIZEN_NAME_FEMALE", "女名" },
            { "CITIZEN_NAME_MALE", "男名" },
            { "CITIZEN_SURNAME_FEMALE", "女姓" },
            { "CITIZEN_SURNAME_HOUSEHOLD", "家族姓" },
            { "CITIZEN_SURNAME_MALE", "男姓" },
            { "CITY_NAME", "城市名" },
            { "COMPANY_NAME", "品牌名" },
            { "DAM_NAME", "坝名" },
            { "DISTRICT_NAME", "区名" },
            { "HIGHWAY_NAME", "公路名" },
            { "STREET_NAME", "街名" },
        };

        /// <summary>
        /// 获取中文名称，如果没有映射则返回原名
        /// </summary>
        public static string GetChineseName(string englishName)
        {
            return ToChinese.TryGetValue(englishName, out var chinese) ? chinese : englishName;
        }
    }

    /// <summary>
    /// 名称导出工具类 - 用于导出品牌行业对应关系和原始名称
    /// </summary>
    public static class NameExporter
    {
        /// <summary>
        /// 需要导出的名称前缀列表
        /// </summary>
        private static readonly string[] NamePrefixes = new string[]
        {
            "Assets.ALLEY_NAME",
            "Assets.ANIMAL_NAME_DOG",
            "Assets.BRIDGE_NAME",
            "Assets.CITIZEN_NAME_FEMALE",
            "Assets.CITIZEN_NAME_MALE",
            "Assets.CITIZEN_SURNAME_FEMALE",
            "Assets.CITIZEN_SURNAME_HOUSEHOLD",
            "Assets.CITIZEN_SURNAME_MALE",
            "Assets.CITY_NAME",
            "Assets.DAM_NAME",
            "Assets.DISTRICT_NAME",
            "Assets.HIGHWAY_NAME",
            "Assets.STREET_NAME"
        };

        /// <summary>
        /// 品牌数据结构
        /// </summary>
        private class BrandData
        {
            public HashSet<string> Companies { get; } = new HashSet<string>();
            public HashSet<string> Industries { get; } = new HashSet<string>();
        }

        /// <summary>
        /// 收集所有品牌信息
        /// </summary>
        /// <returns>品牌名到品牌数据的映射，以及公司到品牌列表的映射</returns>
        private static (Dictionary<string, BrandData> brands, Dictionary<string, List<string>> companies) CollectBrandData()
        {
            var brandData = new Dictionary<string, BrandData>();
            var companyToBrands = new Dictionary<string, List<string>>();

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Mod.log.Error("World not available");
                return (brandData, companyToBrands);
            }

            var prefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();
            var entityManager = world.EntityManager;

            var prefabQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PrefabData>()
            );

            var prefabEntities = prefabQuery.ToEntityArray(Allocator.Temp);

            foreach (var prefabEntity in prefabEntities)
            {
                if (!entityManager.HasBuffer<CompanyBrandElement>(prefabEntity))
                    continue;

                if (!prefabSystem.TryGetPrefab<PrefabBase>(prefabEntity, out var prefabBase))
                    continue;

                string companyType = prefabBase.name;
                string industryInfo = GetIndustryInfo(entityManager, prefabEntity);

                if (!companyToBrands.ContainsKey(companyType))
                {
                    companyToBrands[companyType] = new List<string>();
                }

                var brandBuffer = entityManager.GetBuffer<CompanyBrandElement>(prefabEntity);
                foreach (var brandElement in brandBuffer)
                {
                    if (!prefabSystem.TryGetPrefab<PrefabBase>(brandElement.m_Brand, out var brandPrefab))
                        continue;

                    string brandName = brandPrefab.name;

                    if (!brandData.ContainsKey(brandName))
                    {
                        brandData[brandName] = new BrandData();
                    }

                    brandData[brandName].Companies.Add(companyType);

                    if (!string.IsNullOrEmpty(industryInfo))
                    {
                        brandData[brandName].Industries.Add(industryInfo);
                    }

                    if (!companyToBrands[companyType].Contains(brandName))
                    {
                        companyToBrands[companyType].Add(brandName);
                    }
                }
            }

            prefabEntities.Dispose();

            return (brandData, companyToBrands);
        }

        /// <summary>
        /// 从 localizationManager 导出名称翻译到 NamesTranslation 目录（CSV 格式）
        /// </summary>
        public static void ExportOriginalNames()
        {
            try
            {
                Mod.log.Info("开始导出名称翻译...");

                var locManager = GameManager.instance.localizationManager;
                var entries = locManager.activeDictionary.entries;

                // 从游戏 prefab 中获取品牌列表和行业信息
                var (brandData, _) = CollectBrandData();
                var brandNames = new HashSet<string>(brandData.Keys);
                Mod.log.Info($"从游戏 prefab 获取了 {brandNames.Count} 个品牌");

                // 按前缀分组，使用索引作为 key 存储值
                var groupedByIndex = new Dictionary<string, Dictionary<int, string>>();
                foreach (var prefix in NamePrefixes)
                {
                    groupedByIndex[prefix] = new Dictionary<int, string>();
                }

                // COMPANY_NAME 特殊处理：存储 (品牌名, 翻译) 对
                var companyData = new List<(string brandId, string translation)>();

                // 用于匹配 Assets.NAME[xxx] 格式的正则表达式
                var namePattern = new Regex(@"^Assets\.NAME\[([^\]]+)\]$");
                // 用于匹配冒号后面的数字索引
                var indexPattern = new Regex(@":(\d+)$");

                int totalCount = 0;

                foreach (var entry in entries)
                {
                    // 先检查是否是 Assets.NAME[品牌名] 格式
                    var match = namePattern.Match(entry.Key);
                    if (match.Success)
                    {
                        string brandName = match.Groups[1].Value;
                        if (brandNames.Contains(brandName))
                        {
                            companyData.Add((brandName, entry.Value));
                            totalCount++;
                        }
                        continue;
                    }

                    // 检查其他前缀
                    foreach (var prefix in NamePrefixes)
                    {
                        if (entry.Key.StartsWith(prefix))
                        {
                            var indexMatch = indexPattern.Match(entry.Key);
                            if (indexMatch.Success)
                            {
                                int index = int.Parse(indexMatch.Groups[1].Value);
                                groupedByIndex[prefix][index] = entry.Value;
                                totalCount++;
                                break;
                            }
                        }
                    }
                }

                // 按索引排序后转换为列表
                var groupedValues = new Dictionary<string, List<string>>();
                foreach (var group in groupedByIndex)
                {
                    if (group.Value.Count == 0)
                    {
                        groupedValues[group.Key] = new List<string>();
                        continue;
                    }

                    int maxIndex = group.Value.Keys.Max();
                    var list = new List<string>(maxIndex + 1);
                    for (int i = 0; i <= maxIndex; i++)
                    {
                        list.Add(group.Value.ContainsKey(i) ? group.Value[i] : "");
                    }
                    groupedValues[group.Key] = list;
                }

                // 硬编码的分组定义（使用中文文件名）
                var predefinedGroups = new List<(List<string> prefixes, string fileName)>
                {
                    (new List<string> { "Assets.ALLEY_NAME", "Assets.HIGHWAY_NAME", "Assets.STREET_NAME" }, "巷公路街名"),
                    (new List<string> { "Assets.BRIDGE_NAME", "Assets.DAM_NAME" }, "桥坝名"),
                    (new List<string> { "Assets.CITIZEN_SURNAME_FEMALE", "Assets.CITIZEN_SURNAME_HOUSEHOLD", "Assets.CITIZEN_SURNAME_MALE" }, "姓氏"),
                };

                // 收集已分组的前缀
                var groupedPrefixes = new HashSet<string>();
                foreach (var (prefixes, _) in predefinedGroups)
                {
                    foreach (var prefix in prefixes)
                    {
                        groupedPrefixes.Add(prefix);
                    }
                }

                // 构建最终分组列表
                var finalGroups = new List<(List<string> prefixes, string fileName)>();

                // 添加预定义分组
                foreach (var (prefixes, fileName) in predefinedGroups)
                {
                    var validGroup = prefixes.Where(p => groupedValues.ContainsKey(p) && groupedValues[p].Count > 0).ToList();
                    if (validGroup.Count > 0)
                    {
                        finalGroups.Add((validGroup, fileName));
                    }
                }

                // 添加未分组的前缀作为独立项
                foreach (var kvp in groupedValues)
                {
                    if (kvp.Value.Count > 0 && !groupedPrefixes.Contains(kvp.Key))
                    {
                        string shortName = GetShortName(kvp.Key);
                        string chineseName = NameTypeMapping.GetChineseName(shortName);
                        finalGroups.Add((new List<string> { kvp.Key }, chineseName));
                    }
                }

                // 创建输出目录
                string outputDir = Path.Combine(Mod.ModPath, "Locales", "NamesTranslation");
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // 导出每个分组到 CSV 文件
                int fileCount = 0;
                foreach (var (prefixes, fileName) in finalGroups)
                {
                    var orderedPrefixes = prefixes.OrderBy(x => x).ToList();
                    int itemCount = groupedValues[orderedPrefixes[0]].Count;

                    string filePath = Path.Combine(outputDir, fileName + ".csv");

                    // 构建 CSV 内容
                    var csvLines = new List<string>();

                    // 构建表头：官译_xxx, 你的翻译_xxx
                    var headerParts = new List<string>();
                    foreach (var prefix in orderedPrefixes)
                    {
                        string shortName = GetShortName(prefix);
                        string chineseName = NameTypeMapping.GetChineseName(shortName);
                        headerParts.Add(CsvEscape($"官译_（可空）{chineseName}"));
                    }
                    foreach (var prefix in orderedPrefixes)
                    {
                        string shortName = GetShortName(prefix);
                        string chineseName = NameTypeMapping.GetChineseName(shortName);
                        headerParts.Add(CsvEscape($"你的翻译_{chineseName}"));
                    }
                    csvLines.Add(string.Join(",", headerParts));

                    // 数据行
                    for (int i = 0; i < itemCount; i++)
                    {
                        var rowValues = new List<string>();
                        // 官译列
                        foreach (var prefix in orderedPrefixes)
                        {
                            rowValues.Add(CsvEscape(groupedValues[prefix][i]));
                        }
                        // 你的翻译列（初始内容与官译相同）
                        foreach (var prefix in orderedPrefixes)
                        {
                            rowValues.Add(CsvEscape(groupedValues[prefix][i]));
                        }
                        csvLines.Add(string.Join(",", rowValues));
                    }

                    File.WriteAllText(filePath, string.Join("\n", csvLines), CsvEncoding.Utf8WithBom);
                    Mod.log.Info($"导出 {fileName}.csv: {itemCount} 行");
                    fileCount++;
                }

                // 导出 COMPANY_NAME（品牌名）- 特殊处理
                if (companyData.Count > 0)
                {
                    string companyFileName = NameTypeMapping.GetChineseName("COMPANY_NAME");
                    string companyFilePath = Path.Combine(outputDir, companyFileName + ".csv");

                    var csvLines = new List<string>();

                    // 表头：品牌ID, 行业信息, 官译_品牌名, 你的翻译_品牌名
                    csvLines.Add("品牌ID,行业信息,官译_品牌名,你的翻译_品牌名");

                    // 数据行
                    foreach (var (brandId, translation) in companyData.OrderBy(x => x.brandId))
                    {
                        string industryInfo = GetBrandIndustryInfo(brandId, brandData);
                        csvLines.Add($"{CsvEscape(brandId)},{CsvEscape(industryInfo)},{CsvEscape(translation)},{CsvEscape(translation)}");
                    }

                    File.WriteAllText(companyFilePath, string.Join("\n", csvLines), CsvEncoding.Utf8WithBom);
                    Mod.log.Info($"导出 {companyFileName}.csv: {companyData.Count} 行");
                    fileCount++;
                }

                // 导出格式.csv（对应 general_editable.json）
                ExportFormatCsv(outputDir);
                fileCount++;

                Mod.log.Info($"名称翻译导出完成: {fileCount} 个 CSV 文件，共 {totalCount} 条记录");
                Mod.log.Info($"导出目录: {outputDir}");
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex, $"导出名称翻译失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导出格式.csv（对应 general_editable.json 的内容）
        /// </summary>
        private static void ExportFormatCsv(string outputDir)
        {
            try
            {
                var locManager = GameManager.instance.localizationManager;
                var entriesDict = locManager.activeDictionary.entries.ToDictionary(e => e.Key, e => e.Value);

                // 格式相关的 key 列表
                var formatKeys = new List<(string key, string label)>
                {
                    ("Assets.ADDRESS_NAME_FORMAT", "地址格式"),
                    ("Assets.CITIZEN_NAME_FORMAT", "市民姓名格式"),
                    ("Assets.NAMED_ADDRESS_NAME_FORMAT", "命名地址格式"),
                    ("Assets.ROUTE_NAME[Bus Line]", "公交线路"),
                    ("Assets.ROUTE_NAME[Cargo Airplane Route]", "货机航线"),
                    ("Assets.ROUTE_NAME[Cargo Ship Route]", "货船航线"),
                    ("Assets.ROUTE_NAME[Cargo Train Route]", "货运火车线路"),
                    ("Assets.ROUTE_NAME[Fishing Line]", "捕鱼线路"),
                    ("Assets.ROUTE_NAME[Oil Tanker Route]", "油轮航线"),
                    ("Assets.ROUTE_NAME[Passenger Airplane Line]", "客机航线"),
                    ("Assets.ROUTE_NAME[Passenger Ferry Line]", "摆渡航线"),
                    ("Assets.ROUTE_NAME[Passenger Ship Line]", "客船航线"),
                    ("Assets.ROUTE_NAME[Passenger Train Line]", "客运火车线路"),
                    ("Assets.ROUTE_NAME[Subway Line]", "地铁线路"),
                    ("Assets.ROUTE_NAME[Tram Line]", "有轨电车线路"),
                };

                var csvLines = new List<string>();

                // 表头
                csvLines.Add("Key,名称,官译,你的翻译");

                // 数据行
                foreach (var (key, label) in formatKeys)
                {
                    string value = "";
                    if (entriesDict.TryGetValue(key, out var v))
                    {
                        value = v;
                    }
                    csvLines.Add($"{CsvEscape(key)},{CsvEscape(label)},{CsvEscape(value)},{CsvEscape(value)}");
                }

                string filePath = Path.Combine(outputDir, "格式.csv");
                File.WriteAllText(filePath, string.Join("\n", csvLines), CsvEncoding.Utf8WithBom);
                Mod.log.Info($"导出 格式.csv: {formatKeys.Count} 行");
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex, "导出格式.csv失败");
            }
        }

        /// <summary>
        /// 获取品牌的行业信息字符串
        /// </summary>
        private static string GetBrandIndustryInfo(string brandId, Dictionary<string, BrandData> brandData)
        {
            try
            {
                if (brandData.TryGetValue(brandId, out var brandInfo))
                {
                    var parts = new List<string>();

                    if (brandInfo.Companies.Count > 0)
                    {
                        parts.Add("公司: " + string.Join("; ", brandInfo.Companies));
                    }

                    if (brandInfo.Industries.Count > 0)
                    {
                        parts.Add("行业: " + string.Join("; ", brandInfo.Industries));
                    }

                    return string.Join(" | ", parts);
                }
            }
            catch { }
            return "";
        }

        /// <summary>
        /// CSV 转义
        /// </summary>
        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }

        /// <summary>
        /// 获取前缀的简短名称（用于 CSV 文件名和表头）
        /// </summary>
        private static string GetShortName(string prefix)
        {
            // Assets.CITIZEN_NAME_FEMALE -> CITIZEN_NAME_FEMALE
            // Assets.COMPANY_NAME -> COMPANY_NAME
            if (prefix.StartsWith("Assets."))
            {
                return prefix.Substring(7);
            }
            return prefix;
        }

        /// <summary>
        /// 获取公司的行业信息
        /// </summary>
        private static string GetIndustryInfo(EntityManager entityManager, Entity companyEntity)
        {
            var parts = new List<string>();

            if (entityManager.HasComponent<IndustrialProcessData>(companyEntity))
            {
                var processData = entityManager.GetComponentData<IndustrialProcessData>(companyEntity);

                if (processData.m_Input1.m_Resource != Resource.NoResource)
                {
                    parts.Add($"Input1:{processData.m_Input1.m_Resource}");
                }
                if (processData.m_Input2.m_Resource != Resource.NoResource)
                {
                    parts.Add($"Input2:{processData.m_Input2.m_Resource}");
                }
                if (processData.m_Output.m_Resource != Resource.NoResource)
                {
                    parts.Add($"Output:{processData.m_Output.m_Resource}");
                }
            }

            if (entityManager.HasComponent<ServiceCompanyData>(companyEntity))
            {
                parts.Add("Type:Service");
            }

            return string.Join(", ", parts);
        }
    }
}
