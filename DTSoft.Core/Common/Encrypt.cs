using System.Security.Cryptography;
using System.Text;

namespace DTSoft.Core.Common;

public abstract class Encrypt
{
    /// <summary>
    /// MD5加密字符串（32位大写）
    /// </summary>
    /// <param name="source">源字符串</param>
    /// <returns>加密后的字符串</returns>
    public static string Encrypt_MD5(string source)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(source);
        string result = BitConverter.ToString(MD5.HashData(bytes));
        return result.Replace("-", "");
    }
}
