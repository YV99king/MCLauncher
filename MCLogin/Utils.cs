using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MCLauncher;

internal class Utils
{
    public static string LauncherName => "MCLauncher";
    public static string LauncherVersion => "alpha";

    public static async Task<bool> DownloadFileAsync(HttpClient client, string url, string path, string sha1 = "", int retries = 5, bool overwrite = false)
    {
        while (retries > 0)
        {
            using HttpResponseMessage response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                retries--;
                continue;
            }

            path = Path.GetFullPath(path);
            if (!overwrite && File.Exists(path))
            {
                return false;
            }
            if (!Directory.Exists(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            using FileStream fs = File.Create(path);
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

            return true;
        }
        return false;
    }
}