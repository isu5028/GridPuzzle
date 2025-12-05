using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class EncryptionUtils
{
    // ★重要: このキー（パスワード）は自由に変更してください（必ず16文字 or 32文字）
    private static readonly string Key = "1234567890123456";

    // ★重要: 初期化ベクトル（16文字）。これも変更してください
    private static readonly string IV = "1234567890123456";

    // 暗号化 (文字列 -> 暗号化されたBase64文字列)
    public static string Encrypt(string plainText)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(Key);
        byte[] ivBytes = Encoding.UTF8.GetBytes(IV);

        using (Aes aes = Aes.Create())
        {
            aes.Key = keyBytes;
            aes.IV = ivBytes;

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }
    }

    // 復号化 (暗号化されたBase64文字列 -> 元の文字列)
    public static string Decrypt(string cipherText)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(Key);
        byte[] ivBytes = Encoding.UTF8.GetBytes(IV);
        byte[] cipherBytes = Convert.FromBase64String(cipherText);

        using (Aes aes = Aes.Create())
        {
            aes.Key = keyBytes;
            aes.IV = ivBytes;

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using (MemoryStream ms = new MemoryStream(cipherBytes))
            {
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader sr = new StreamReader(cs))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
        }
    }
}