using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
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
        var versionJson = JsonDocument.Parse(jsonStream.ReadToEnd());
        var launchArgsJson = versionJson.RootElement.GetProperty("arguments");
        string javaComponent  = versionJson.RootElement.GetProperty("javaVersion").GetProperty("component").GetString();
        StringBuilder cmdCommandBuilder = new($"/C {Path.Combine(_minecraftPath.FullName, "runtime", javaComponent, GetJvmPlatformName(), javaComponent, "bin", "java.exe")}");
        foreach (var arg in launchArgsJson.GetProperty("game").EnumerateArray())
        {
            if (arg.ValueKind == JsonValueKind.Object)
            {
                bool isRulesMatch = true;
                foreach (var rule in arg.GetProperty("rules").EnumerateArray())
                {
                    
                }
            }
        }
    }
    private static bool IsRulesMatch(JsonDocument rules) => true;
    private static string GetJvmPlatformName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (Environment.Is64BitOperatingSystem)
                return "windows-x64";
            else
                return "windows-x86";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (Environment.Is64BitOperatingSystem)
                return "linux";
            else
                return "linux-i386";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                return "mac-os-arm64";
            else
                return "mac-os";
        }
        else return "gamecore";
    }
}