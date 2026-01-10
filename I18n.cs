using Colossal;
using Colossal.Json;
using Game.SceneFlow;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BetterChineseNames
{
    /// <summary>
    /// 实现 IDictionarySource 接口，用于向游戏提供翻译条目
    /// </summary>
    public class LocaleSource : IDictionarySource
    {
        private readonly Dictionary<string, string> m_Entries = new Dictionary<string, string>();

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return m_Entries;
        }

        public void Unload()
        {
            m_Entries.Clear();
        }

        /// <summary>
        /// 添加翻译条目
        /// </summary>
        public void AddEntries(Dictionary<string, string> entries)
        {
            foreach (var entry in entries)
            {
                m_Entries[entry.Key] = entry.Value;
            }
        }

        /// <summary>
        /// 清空所有条目
        /// </summary>
        public void Clear()
        {
            m_Entries.Clear();
        }

        /// <summary>
        /// 获取当前条目数量
        /// </summary>
        public int Count => m_Entries.Count;
    }

    /// <summary>
    /// 国际化管理器 - 负责加载和管理中文翻译
    /// </summary>
    public static class I18n
    {
        private const string LOCALE_ID = "zh-HANS";
        private static LocaleSource s_LocaleSource;
        private static string s_LocalesPath;

        /// <summary>
        /// 用于在 UI 中显示错误的日志记录器
        /// </summary>
        private static readonly Colossal.Logging.ILog s_UILog = 
            Colossal.Logging.LogManager.GetLogger("BetterChineseNames.I18n").SetShowsErrorsInUI(true);

        /// <summary>
        /// 当前语言的自定义翻译字典，供 Harmony patch 使用
        /// </summary>
        public static Dictionary<string, string> CurrentLocaleDictionary { get; private set; } = new Dictionary<string, string>();

        /// <summary>
        /// 初始化 I18n 系统
        /// </summary>
        public static void Initialize(string localesPath)
        {
            s_LocalesPath = localesPath;
            s_LocaleSource = new LocaleSource();
            GameManager.instance.localizationManager.AddSource(LOCALE_ID, s_LocaleSource);
            Mod.log.Info($"I18n initialized with locales path: {localesPath}");

            // 首次加载时检查 NamesTranslation 目录，如果不存在则执行重置
            EnsureTranslationFilesExist();
        }

        /// <summary>
        /// 获取用户自定义翻译目录路径（位于 ModsData 下）
        /// </summary>
        private static string GetCustomTranslationDirectory()
        {
            return Path.Combine(Mod.ModsDataPath, "NamesTranslation");
        }

        /// <summary>
        /// 确保翻译文件存在，如果不存在则从预置翻译复制
        /// </summary>
        private static void EnsureTranslationFilesExist()
        {
            string presetsDir = Path.Combine(s_LocalesPath, "NamesTranslation", "预置翻译");

            // 检查预置翻译目录是否存在
            if (!Directory.Exists(presetsDir))
            {
                Mod.log.Warn($"预置翻译目录不存在: {presetsDir}");
                return;
            }

            // 检查是否需要初始化自定义翻译目录（现在位于 ModsData 下）
            string customDir = GetCustomTranslationDirectory();
            bool needsInit = !Directory.Exists(customDir) ||
                             !Directory.GetFiles(customDir, "*.csv").Any();

            if (needsInit)
            {
                Mod.log.Info("首次加载或自定义翻译目录为空，从预置翻译恢复...");
                ResetCustomTranslations();
            }
        }

        /// <summary>
        /// 撤销所有自定义翻译（从预置翻译的甲目录复制到自定义翻译目录）
        /// </summary>
        public static void ResetCustomTranslations()
        {
            try
            {
                string defaultPresetDir = Path.Combine(s_LocalesPath, "NamesTranslation", "预置翻译", "甲");
                string customDir = GetCustomTranslationDirectory();

                if (!Directory.Exists(defaultPresetDir))
                {
                    Mod.log.Warn($"默认预置翻译目录不存在: {defaultPresetDir}");
                    return;
                }

                // 确保目标目录存在（位于 ModsData 下）
                if (!Directory.Exists(customDir))
                {
                    Directory.CreateDirectory(customDir);
                }

                // 复制所有 CSV 文件从默认预置到自定义目录
                foreach (var sourceFile in Directory.GetFiles(defaultPresetDir, "*.csv"))
                {
                    string fileName = Path.GetFileName(sourceFile);
                    string destFile = Path.Combine(customDir, fileName);
                    File.Copy(sourceFile, destFile, overwrite: true);
                }

                // 创建提示文件
                CreateHintFiles(customDir);

                Mod.log.Info($"已从预置翻译(甲)恢复所有翻译文件到自定义目录: {customDir}");
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex, "重置自定义翻译失败");
            }
        }

        /// <summary>
        /// 在自定义翻译目录中创建提示文件
        /// </summary>
        private static void CreateHintFiles(string customDir)
        {
            try
            {
                // 创建提示文件
                string[] hintFiles = new[]
                {
                    "可以编辑来自定义翻译（内含原文）",
                    "如果改错了可以从设置里重置"
                };

                foreach (var fileName in hintFiles)
                {
                    string hintFile = Path.Combine(customDir, fileName);
                    if (!File.Exists(hintFile))
                    {
                        File.WriteAllText(hintFile, "");
                    }
                }
            }
            catch (Exception ex)
            {
                Mod.log.Warn($"创建提示文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前应使用的翻译目录路径
        /// </summary>
        private static string GetTranslationDirectory(Setting setting)
        {
            if (setting.IsCustomMode)
            {
                // 自定义模式：使用 ModsData 下的可编辑目录
                return GetCustomTranslationDirectory();
            }
            else
            {
                // 预置模式：使用 mod 目录内的预置翻译目录
                string translationDir = Path.Combine(s_LocalesPath, "NamesTranslation");
                return Path.Combine(translationDir, setting.GetPresetPath());
            }
        }

        /// <summary>
        /// 根据设置加载翻译文件
        /// </summary>
        public static void LoadFromSettings(Setting setting)
        {
            if (s_LocaleSource == null)
            {
                Mod.log.Error("I18n not initialized!");
                return;
            }

            s_LocaleSource.Clear();
            // 清空 Harmony patch 使用的字典
            CurrentLocaleDictionary = new Dictionary<string, string>();
            int totalEntries = 0;
            var loadErrors = new List<string>();

            // 加载 general.json（官方翻译修正）
            if (setting.EnableOfficialFixes)
            {
                var count = LoadJsonFile(Path.Combine(s_LocalesPath, "general.json"));
                totalEntries += count;
                Mod.log.Info($"Loaded general.json: {count} entries");
            }

            // 加载翻译文件
            {
                string translationDir = GetTranslationDirectory(setting);

                // 加载格式翻译（格式.csv 在每个预置/自定义翻译目录下）
                string formatCsvPath = Path.Combine(translationDir, "格式.csv");
                var formatEntries = LoadFormatCsv(formatCsvPath);
                if (formatEntries.Count > 0)
                {
                    s_LocaleSource.AddEntries(formatEntries);
                    // 同时添加到 Harmony patch 字典
                    foreach (var entry in formatEntries)
                    {
                        CurrentLocaleDictionary[entry.Key] = entry.Value;
                    }
                    totalEntries += formatEntries.Count;
                    Mod.log.Info($"Loaded 格式.csv: {formatEntries.Count} entries from {formatCsvPath}");
                }
                else
                {
                    string errorMsg = $"格式.csv 加载失败: {formatCsvPath}";
                    Mod.log.Warn(errorMsg);
                    loadErrors.Add(errorMsg);
                }

                // 加载名称翻译 CSV 文件
                if (Directory.Exists(translationDir))
                {
                    totalEntries += LoadNameTranslations(setting, translationDir, loadErrors);
                    Mod.log.Info($"Using translation directory: {translationDir}");
                }
                else
                {
                    string errorMsg = $"翻译目录不存在: {translationDir}";
                    Mod.log.Warn(errorMsg);
                    loadErrors.Add(errorMsg);
                }
            }

            GameManager.instance.localizationManager.ReloadActiveLocale();

            Mod.log.Info($"Total loaded entries: {totalEntries} (Harmony dict: {CurrentLocaleDictionary.Count})");

            // 如果有加载错误，在 UI 中显示
            if (loadErrors.Count > 0)
            {
                string errorSummary = $"更好的中文译名: {loadErrors.Count} 个文件加载失败\n" +
                    string.Join("\n", loadErrors) + 
                    "\n\n请点击【继续】，然后前往 设置 → 更好的中文译名 → 自定义 → 撤销所有自定义翻译 来重置文件。";
                s_UILog.Error(errorSummary);
            }
        }

        /// <summary>
        /// 加载名称翻译（从 CSV 文件）
        /// </summary>
        private static int LoadNameTranslations(Setting setting, string translationDir, List<string> loadErrors)
        {
            int totalCount = 0;

            // 定义 CSV 文件和对应的设置、key 前缀
            var csvMappings = new List<(string fileName, bool enabled, string[] keyPrefixes)>
            {
                ("巷公路街名.csv", true, new[] { "Assets.ALLEY_NAME", "Assets.HIGHWAY_NAME", "Assets.STREET_NAME" }),
                ("桥坝名.csv", true, new[] { "Assets.BRIDGE_NAME", "Assets.DAM_NAME" }),
                ("姓氏.csv", true, new[] { "Assets.CITIZEN_SURNAME_FEMALE", "Assets.CITIZEN_SURNAME_HOUSEHOLD", "Assets.CITIZEN_SURNAME_MALE" }),
                ("城市名.csv", setting.EnableCityNames, new[] { "Assets.CITY_NAME" }),
                ("区名.csv", setting.EnableDistrictNames, new[] { "Assets.DISTRICT_NAME" }),
                ("男名.csv", setting.EnableCitizenMaleNames, new[] { "Assets.CITIZEN_NAME_MALE" }),
                ("女名.csv", setting.EnableCitizenFemaleNames, new[] { "Assets.CITIZEN_NAME_FEMALE" }),
                ("狗名.csv", setting.EnableDogNames, new[] { "Assets.ANIMAL_NAME_DOG" }),
                ("品牌名.csv", setting.EnableCompanyNames, new[] { "Assets.NAME" }),
            };

            // 检查各分组的开关
            bool roadNamesEnabled = setting.EnableStreetNames || setting.EnableAlleyNames || setting.EnableHighwayNames;
            bool bridgeDamEnabled = setting.EnableBridgeNames || setting.EnableDamNames;
            bool surnamesEnabled = setting.EnableCitizenMaleSurnames || setting.EnableCitizenFemaleSurnames || setting.EnableCitizenHouseholdSurnames;

            foreach (var (fileName, baseEnabled, keyPrefixes) in csvMappings)
            {
                // 检查分组开关
                bool enabled = baseEnabled;
                if (fileName == "巷公路街名.csv") enabled = roadNamesEnabled;
                else if (fileName == "桥坝名.csv") enabled = bridgeDamEnabled;
                else if (fileName == "姓氏.csv") enabled = surnamesEnabled;

                if (!enabled) continue;

                string filePath = Path.Combine(translationDir, fileName);
                if (!File.Exists(filePath))
                {
                    string errorMsg = $"{fileName} 文件不存在: {filePath}";
                    Mod.log.Warn(errorMsg);
                    loadErrors.Add(errorMsg);
                    continue;
                }

                var entries = LoadNameCsv(filePath, keyPrefixes, setting);
                if (entries.Count > 0)
                {
                    s_LocaleSource.AddEntries(entries);
                    // 同时添加到 Harmony patch 字典
                    foreach (var entry in entries)
                    {
                        CurrentLocaleDictionary[entry.Key] = entry.Value;
                    }
                    totalCount += entries.Count;
                    Mod.log.Info($"Loaded {fileName}: {entries.Count} entries");
                }
                else
                {
                    string errorMsg = $"{fileName} 加载失败或为空: {filePath}";
                    Mod.log.Warn(errorMsg);
                    loadErrors.Add(errorMsg);
                }
            }

            return totalCount;
        }

        /// <summary>
        /// 加载名称 CSV 文件并转换为 locale dict
        /// CSV 格式: 官译_xxx, 你的翻译_xxx, ...
        /// </summary>
        private static Dictionary<string, string> LoadNameCsv(string filePath, string[] keyPrefixes, Setting setting)
        {
            var result = new Dictionary<string, string>();

            try
            {
                // 使用共享读取模式，允许文件被 Excel 等程序占用时也能读取
                var lines = ReadAllLinesShared(filePath);
                if (lines.Length < 2) return result;

                // 解析表头，找到"你的翻译_"列
                var headers = ParseCsvLine(lines[0]);
                var translationColumns = new List<(int index, string typeName)>();

                for (int i = 0; i < headers.Count; i++)
                {
                    if (headers[i].StartsWith("你的翻译_"))
                    {
                        string typeName = headers[i].Substring("你的翻译_".Length);
                        translationColumns.Add((i, typeName));
                    }
                }

                // 特殊处理品牌名：第一列是品牌ID
                bool isCompanyName = filePath.EndsWith("品牌名.csv");
                int brandIdColumn = isCompanyName ? 0 : -1;

                // 处理数据行
                for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
                {
                    var values = ParseCsvLine(lines[lineIndex]);
                    if (values.Count == 0) continue;

                    int rowIndex = lineIndex - 1; // 从 0 开始的行索引

                    foreach (var (colIndex, typeName) in translationColumns)
                    {
                        if (colIndex >= values.Count) continue;

                        string translation = values[colIndex];
                        if (string.IsNullOrEmpty(translation)) continue;

                        // 检查该类型是否启用
                        if (!IsTypeEnabled(typeName, setting)) continue;

                        // 构建 key
                        string key;
                        if (isCompanyName && brandIdColumn >= 0)
                        {
                            // 品牌名使用 Assets.NAME[品牌ID] 格式
                            string brandId = values[brandIdColumn];
                            key = $"Assets.NAME[{brandId}]";
                        }
                        else
                        {
                            // 其他名称使用 前缀:索引 格式
                            string prefix = GetKeyPrefixFromTypeName(typeName, keyPrefixes);
                            key = $"{prefix}:{rowIndex}";
                        }

                        result[key] = translation;
                    }
                }
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex, $"Failed to load CSV: {filePath}");
            }

            return result;
        }

        /// <summary>
        /// 检查某个类型是否启用
        /// </summary>
        private static bool IsTypeEnabled(string typeName, Setting setting)
        {
            return typeName switch
            {
                "巷名" => setting.EnableAlleyNames,
                "公路名" => setting.EnableHighwayNames,
                "街名" => setting.EnableStreetNames,
                "桥名" => setting.EnableBridgeNames,
                "坝名" => setting.EnableDamNames,
                "女姓" => setting.EnableCitizenFemaleSurnames,
                "家族姓" => setting.EnableCitizenHouseholdSurnames,
                "男姓" => setting.EnableCitizenMaleSurnames,
                "男名" => setting.EnableCitizenMaleNames,
                "女名" => setting.EnableCitizenFemaleNames,
                "城市名" => setting.EnableCityNames,
                "区名" => setting.EnableDistrictNames,
                "品牌名" => setting.EnableCompanyNames,
                "狗名" => setting.EnableDogNames,
                _ => true
            };
        }

        /// <summary>
        /// 从中文类型名获取 key 前缀
        /// </summary>
        private static string GetKeyPrefixFromTypeName(string typeName, string[] keyPrefixes)
        {
            var mapping = new Dictionary<string, string>
            {
                { "巷名", "Assets.ALLEY_NAME" },
                { "公路名", "Assets.HIGHWAY_NAME" },
                { "街名", "Assets.STREET_NAME" },
                { "桥名", "Assets.BRIDGE_NAME" },
                { "坝名", "Assets.DAM_NAME" },
                { "女姓", "Assets.CITIZEN_SURNAME_FEMALE" },
                { "家族姓", "Assets.CITIZEN_SURNAME_HOUSEHOLD" },
                { "男姓", "Assets.CITIZEN_SURNAME_MALE" },
                { "男名", "Assets.CITIZEN_NAME_MALE" },
                { "女名", "Assets.CITIZEN_NAME_FEMALE" },
                { "城市名", "Assets.CITY_NAME" },
                { "区名", "Assets.DISTRICT_NAME" },
                { "品牌名", "Assets.NAME" },
                { "狗名", "Assets.ANIMAL_NAME_DOG" },
            };

            if (mapping.TryGetValue(typeName, out var prefix))
            {
                return prefix;
            }

            // 如果没有映射，尝试使用传入的 keyPrefixes 中的第一个
            return keyPrefixes.Length > 0 ? keyPrefixes[0] : "Assets.UNKNOWN";
        }

        /// <summary>
        /// 加载格式 CSV 文件
        /// CSV 格式: key, 官译, 你的翻译
        /// </summary>
        private static Dictionary<string, string> LoadFormatCsv(string filePath)
        {
            var result = new Dictionary<string, string>();

            try
            {
                if (!File.Exists(filePath)) return result;

                // 使用共享读取模式，允许文件被 Excel 等程序占用时也能读取
                var lines = ReadAllLinesShared(filePath);
                if (lines.Length < 2) return result;

                // 解析表头，找到 key 列和"你的翻译"列
                // 注意：去掉可能的 BOM 字符
                var headers = ParseCsvLine(lines[0].TrimStart('\uFEFF'));
                int keyColumn = headers.FindIndex(h => 
                    h.Equals("key", StringComparison.OrdinalIgnoreCase) || 
                    h.TrimStart('\uFEFF').Equals("key", StringComparison.OrdinalIgnoreCase));
                int translationColumn = headers.FindIndex(h => h.StartsWith("你的翻译"));

                if (keyColumn < 0 || translationColumn < 0)
                {
                    Mod.log.Warn($"格式.csv 表头解析失败: keyColumn={keyColumn}, translationColumn={translationColumn}, headers=[{string.Join(", ", headers)}]");
                    return result;
                }

                // 处理数据行
                for (int i = 1; i < lines.Length; i++)
                {
                    var values = ParseCsvLine(lines[i]);
                    if (values.Count <= Math.Max(keyColumn, translationColumn)) continue;

                    string key = values[keyColumn];
                    string translation = values[translationColumn];

                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(translation))
                    {
                        result[key] = translation;
                    }
                }
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex, $"Failed to load format CSV: {filePath}");
            }

            return result;
        }

        /// <summary>
        /// 读取文件所有行，允许文件被其他程序（如 Excel）占用时也能读取
        /// 支持 UTF-8（带或不带 BOM）和 GB2312 编码
        /// 参考: https://stackoverflow.com/questions/3825390/effective-way-to-find-any-files-encoding
        /// </summary>
        private static string[] ReadAllLinesShared(string filePath)
        {
            // 首先读取文件字节
            byte[] fileBytes;
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fileBytes = new byte[stream.Length];
                stream.Read(fileBytes, 0, fileBytes.Length);
            }

            // 检测编码并读取内容
            string fileName = Path.GetFileName(filePath);
            string content = DetectEncodingAndDecode(fileBytes, fileName);
            
            // 分割成行
            return content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        }

        /// <summary>
        /// 检测编码并解码字节数组
        /// 使用 BOM 检测 + UTF-8 解码尝试 + GB2312 回退
        /// 参考: https://stackoverflow.com/questions/3825390/effective-way-to-find-any-files-encoding
        /// </summary>
        private static string DetectEncodingAndDecode(byte[] bytes, string fileName = null)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            string logPrefix = string.IsNullOrEmpty(fileName) ? "File" : fileName;

            // 1. 检查 BOM
            var bomEncoding = GetEncodingByBOM(bytes);
            if (bomEncoding != null)
            {
                Mod.log.Info($"[Encoding] {logPrefix}: detected {bomEncoding.EncodingName} by BOM");
                return bomEncoding.GetString(bytes);
            }

            // 2. 尝试使用 UTF-8 解码（使用 ExceptionFallback 来检测是否有效）
            try
            {
                var utf8Verifier = Encoding.GetEncoding("utf-8", 
                    new EncoderExceptionFallback(), 
                    new DecoderExceptionFallback());
                string result = utf8Verifier.GetString(bytes);
                Mod.log.Info($"[Encoding] {logPrefix}: detected UTF-8 (no BOM, valid UTF-8)");
                return result;
            }
            catch
            {
                // UTF-8 解码失败，使用 GB2312
                Mod.log.Info($"[Encoding] {logPrefix}: UTF-8 decode failed, trying GB2312...");
            }

            // 3. 回退到 GB2312 (使用自定义解码器，避免依赖 System.Text.Encoding.CodePages)
            Mod.log.Info($"[Encoding] {logPrefix}: using GB2312 fallback (custom decoder)");
            return Gb2312Decoder.Decode(bytes);
        }

        /// <summary>
        /// 通过 BOM（字节顺序标记）检测编码
        /// 参考: https://stackoverflow.com/questions/3825390/effective-way-to-find-any-files-encoding
        /// </summary>
        private static Encoding GetEncodingByBOM(byte[] bytes)
        {
            if (bytes.Length < 2)
                return null;

            // UTF-8 BOM: EF BB BF
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8;

            // UTF-32 LE BOM: FF FE 00 00 (必须在 UTF-16 LE 之前检查)
            if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
                return Encoding.UTF32;

            // UTF-16 LE BOM: FF FE
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode;

            // UTF-16 BE BOM: FE FF
            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode;

            // UTF-32 BE BOM: 00 00 FE FF
            if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
                return new UTF32Encoding(true, true);

            // UTF-7 BOM: 2B 2F 76
            if (bytes.Length >= 3 && bytes[0] == 0x2B && bytes[1] == 0x2F && bytes[2] == 0x76)
                return Encoding.UTF7;

            return null; // 没有 BOM
        }

        /// <summary>
        /// 解析 CSV 行，处理引号转义
        /// 支持逗号和 Tab 作为分隔符（自动检测）
        /// </summary>
        private static List<string> ParseCsvLine(string line, char? detectedDelimiter = null)
        {
            // 自动检测分隔符：如果包含 Tab 且 Tab 数量 >= 逗号数量，则使用 Tab
            char delimiter = detectedDelimiter ?? DetectDelimiter(line);
            
            var result = new List<string>();
            bool inQuotes = false;
            var currentField = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // 转义的引号
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    result.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            result.Add(currentField.ToString());
            return result;
        }

        /// <summary>
        /// 检测 CSV 行的分隔符（逗号或 Tab）
        /// </summary>
        private static char DetectDelimiter(string line)
        {
            int commaCount = 0;
            int tabCount = 0;
            bool inQuotes = false;

            foreach (char c in line)
            {
                if (c == '"') inQuotes = !inQuotes;
                else if (!inQuotes)
                {
                    if (c == ',') commaCount++;
                    else if (c == '\t') tabCount++;
                }
            }

            // 如果 Tab 数量 >= 逗号数量且有 Tab，则使用 Tab
            return (tabCount > 0 && tabCount >= commaCount) ? '\t' : ',';
        }

        /// <summary>
        /// 加载单个 JSON 文件并返回加载的条目数
        /// </summary>
        private static int LoadJsonFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Mod.log.Warn($"File not found: {filePath}");
                    return 0;
                }

                var json = File.ReadAllText(filePath);
                var dict = JSON.Load(json).Make<Dictionary<string, string>>();
                s_LocaleSource.AddEntries(dict);
                // 同时添加到 Harmony patch 字典
                foreach (var entry in dict)
                {
                    CurrentLocaleDictionary[entry.Key] = entry.Value;
                }
                return dict.Count;
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex, $"Failed to load locale file: {filePath}");
                return 0;
            }
        }
    }
}
