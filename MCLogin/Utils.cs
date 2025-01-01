using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MCLauncher;

internal class Utils
{
    public static string LauncherName => "MCLauncher";
    public static string LauncherVersion => "alpha";

    public static async Task<bool> DownloadFileAsync(string url, string path, HttpClient client, bool overwrite = false, bool throwOnDownloadFailed = false)
    {
        using HttpResponseMessage response = await client.GetAsync(url);
        if (throwOnDownloadFailed && !response.IsSuccessStatusCode)
        {
            if (throwOnDownloadFailed)
                throw new HttpRequestException($"Failed to download {url}. Status code: {response.StatusCode}");
            return false;
        }

        if (!overwrite && File.Exists(path))
        {
            if (throwOnDownloadFailed)
                throw new IOException($"Downloaded file already exists path:{path}, url:{url}");
            return false; 
        }
        using FileStream fs = File.Create(path);
        await response.Content.CopyToAsync(fs);

        return true;
    }
}