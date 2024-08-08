using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

    public void LaunchMinecraft(Dictionary<string, string> args)
    {
        using StreamReader jsonStream = new(Path.Combine(_minecraftPath.FullName, "versions", _version, _version + ".json"));
        var launchArgsJson = JsonDocument.Parse(jsonStream.ReadToEnd()).RootElement.GetProperty("arguments");
        StringBuilder cmdCommandBuilder = new($"/C {Path.Combine(_minecraftPath.FullName, "runtime", "java-runtime-delta", )}");
    }
    private string GetOSAndArchitecture()
    {
        if (Environment.Is64BitOperatingSystem)
        {
            if (OperatingSystem.IsWindows())
                return 
        }
    }
}