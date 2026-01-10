using System;
using System.Text;

namespace BetterChineseNames
{
    /// <summary>
    /// 自定义 GB2312/GBK (CP936) 解码器
    /// 用于替代 System.Text.Encoding.CodePages 包，避免与游戏引擎冲突
    /// </summary>
    internal static class Gb2312Decoder
    {
        /// <summary>
        /// 将 GB2312/GBK 编码的字节数组解码为字符串
        /// </summary>
        /// <param name="bytes">要解码的字节数组</param>
        /// <returns>解码后的字符串</returns>
        public static string Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            var sb = new StringBuilder(bytes.Length);
            int i = 0;

            while (i < bytes.Length)
            {
                byte b = bytes[i];

                // ASCII 范围 (0x00-0x7F)
                if (b <= 0x7F)
                {
                    // 查找单字节映射（主要用于 0x80 的欧元符号，但 0x80 > 0x7F 所以这里都是直接 ASCII）
                    char c = Cp936Table.SingleByte[b];
                    sb.Append(c != '\0' ? c : (char)b);
                    i++;
                }
                // 0x80 是特殊的单字节字符（欧元符号）
                else if (b == 0x80)
                {
                    char c = Cp936Table.SingleByte[b];
                    sb.Append(c != '\0' ? c : '\uFFFD'); // 替换字符
                    i++;
                }
                // GBK 双字节范围: 首字节 0x81-0xFE
                else if (b >= 0x81 && b <= 0xFE)
                {
                    if (i + 1 < bytes.Length)
                    {
                        byte b2 = bytes[i + 1];
                        ushort key = (ushort)((b << 8) | b2);

                        if (Cp936Table.DoubleByte.TryGetValue(key, out char c))
                        {
                            sb.Append(c);
                        }
                        else
                        {
                            // 未知字符，使用替换字符
                            sb.Append('\uFFFD');
                        }
                        i += 2;
                    }
                    else
                    {
                        // 不完整的双字节序列
                        sb.Append('\uFFFD');
                        i++;
                    }
                }
                else
                {
                    // 其他未定义范围，使用替换字符
                    sb.Append('\uFFFD');
                    i++;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 将 GB2312/GBK 编码的字节数组解码为字符串
        /// </summary>
        /// <param name="bytes">要解码的字节数组</param>
        /// <param name="index">开始位置</param>
        /// <param name="count">要解码的字节数</param>
        /// <returns>解码后的字符串</returns>
        public static string Decode(byte[] bytes, int index, int count)
        {
            if (bytes == null || bytes.Length == 0 || count == 0)
                return string.Empty;

            if (index < 0 || index >= bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (count < 0 || index + count > bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            // 创建子数组并解码
            var subArray = new byte[count];
            Array.Copy(bytes, index, subArray, 0, count);
            return Decode(subArray);
        }
    }
}
