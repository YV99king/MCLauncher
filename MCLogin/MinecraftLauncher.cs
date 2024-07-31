using System;
using System.IO;
using System.Text.Json;

namespace MCLauncher;

public class MinecraftLauncher
{
    private readonly Login _login;
    private readonly DirectoryInfo _minecraftPath;
    private readonly string _version;

    public MinecraftLauncher(string version, Login login, DirectoryInfo minecraftPath)
    {
        _login = login ?? throw new ArgumentNullException(nameof(login));
        _minecraftPath = minecraftPath ?? throw new ArgumentNullException(nameof(minecraftPath));
        _version = CheckVersionString(version) ? version : throw new ArgumentException("Invalid version ID", nameof(version));
    }

    public static bool CheckVersionString(string version)
    {
        return !string.IsNullOrEmpty(version);
    }

    public void LaunchMinecraft()
    {

        using StreamReader jsonStream = new(Path.Combine(_minecraftPath.FullName, "versions", _version, _version + ".json"));
        var versionJson = JsonDocument.Parse(jsonStream.ReadToEnd());

    }
}