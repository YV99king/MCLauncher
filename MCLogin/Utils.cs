using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MCLauncher;

internal class Utils
{
    public static string LauncherName => "MCLauncher";
    public static string LauncherVersion => "alpha";

    public static async Task<bool> DownloadFileAsync(HttpClient client, string url, string path, string sha1 = "", int retries = 5, bool overwrite = false, Action<string> log = null)
    {
        log ??= _ => { };

        string computedHash = "";
        if (File.Exists(path) && IsValidHash(sha1))
        {
            var sourceAsBytes = await File.ReadAllBytesAsync(path);
            computedHash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(sourceAsBytes));
            if (computedHash.Equals(sha1, StringComparison.CurrentCultureIgnoreCase))
            {
                log($"Already downloaded {url} to {path}.");
                return true;
            }
        }

        if (!overwrite && File.Exists(path))
        {
            log($"File in {path} already exists && `overwrite` set to false.");
            return false;
        }

        while (retries > 0)
            try
            {
                using HttpResponseMessage response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    retries--;
                    continue;
                }

                path = Path.GetFullPath(path);
                if (!Directory.Exists(Path.GetDirectoryName(path)))
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                using FileStream fs = File.Open(path, FileMode.OpenOrCreate);
                await response.Content.CopyToAsync(fs);

                if (IsValidHash(sha1))
                {
                    fs.Position = 0;
                    computedHash = Convert.ToHexString(System.Security.Cryptography.SHA1.Create().ComputeHash(fs));
                    if (!computedHash.Equals(sha1, StringComparison.CurrentCultureIgnoreCase))
                    {
                        overwrite = true;
                        retries--;
                        continue;
                    }
                }

                log($"Downloaded {url} to {path}.");
                return true;
            }
            catch (Exception)
            {
                continue;
            }


        log($"Could not download {url} with hash {computedHash} to {path} with hash {sha1}.");
        return false;

        static bool IsValidHash(string sha1)
        {
            if (string.IsNullOrWhiteSpace(sha1))
                return false;
            if (sha1.Length != (System.Security.Cryptography.SHA1.HashSizeInBits / 4))
                return false;
            foreach (var letter in sha1)
                if ("0123456789abcdefABDEF".Contains(letter))
                    return false;

            return true;
        }
    }
}