using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MCLauncher;

internal class Utils
{
    public static string LauncherName => "MCLauncher";
    public static string LauncherVersion => "alpha";

    public static async Task<bool> DownloadFileAsync(HttpClient client, string url, string path, string sha1 = "", int retries = 2, bool overwrite = false, Action<string> log = null)
    {
        if (!overwrite && File.Exists(path))
            retries = 0;
        else if (File.Exists(path) && !string.IsNullOrWhiteSpace(sha1))
        {
            var sourceAsBytes = await File.ReadAllBytesAsync(path);
            var computedHash = Convert.ToString(System.Security.Cryptography.SHA1.HashData(sourceAsBytes));
            if (computedHash == sha1)
            {
                log($"Already downloaded {url} to {path}");
                return true;
            }
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

                if (!string.IsNullOrWhiteSpace(sha1))
                {
                    fs.Position = 0;
                    var computedHash = Convert.ToString(System.Security.Cryptography.SHA1.Create().ComputeHash(fs));
                    if (computedHash != sha1)
                    {
                        overwrite = true;
                        retries--;
                        continue;
                    }
                }

                log($"Downloaded {url} to {path}");
                return true;
            }
            catch (Exception)
            {
                continue;
            }

        log($"Could not download {url} to {path}");
        return false;
    }
}