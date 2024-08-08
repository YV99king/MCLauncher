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

    public void InstallMinecraft()
    {

    }

    public void LaunchMinecraft(Dictionary<string, string> args)
    {
        var versionJson = VersionJson.DeserializeJson(new FileStream(Path.Combine(_minecraftPath.FullName, "versions", _version), FileMode.Open));
        StringBuilder minecraftCommandBuilder = new($"/C {Path.Combine(_minecraftPath.FullName, PlatformInfo.GetJavaPlatformName(), versionJson.javaVersion.component, )}");
    }

    private class VersionJson
    {
        public static VersionJsonDeserialization DeserializeJson(string json) =>
            JsonSerializer.Deserialize<VersionJsonDeserialization>(json);
        public static VersionJsonDeserialization DeserializeJson(Stream stream) =>
            JsonSerializer.Deserialize<VersionJsonDeserialization>(stream);
        public static VersionJsonDeserialization DeserializeJson(JsonDocument json) =>
            JsonSerializer.Deserialize<VersionJsonDeserialization>(json);
        public class VersionJsonDeserialization
        {
            public Arguments arguments { get; set; }
            public AssetIndex assetIndex { get; set; }
            public string assets { get; set; }
            public Downloads downloads { get; set; }
            public string id { get; set; }
            public JavaVersion javaVersion { get; set; }
            public List<Library> libraries { get; set; }
            public Logging logging { get; set; }
            public string mainClass { get; set; }
            public int minimumLauncherVersion { get; set; }
            public string type { get; set; }
        }
        public class Arguments
        {
            public List<object> game { get; set; }
            public List<object> jvm { get; set; }
        }
        public class Artifact
        {
            public string path { get; set; }
            public string sha1 { get; set; }
            public int size { get; set; }
            public string url { get; set; }
        }
        public class AssetIndex
        {
            public string id { get; set; }
            public string sha1 { get; set; }
            public int size { get; set; }
            public int totalSize { get; set; }
            public string url { get; set; }
        }
        public class Client
        {
            public string sha1 { get; set; }
            public int size { get; set; }
            public string url { get; set; }
            public string argument { get; set; }
            public File file { get; set; }
            public string type { get; set; }
        }
        public class Downloads
        {
            public Client client { get; set; }
            public Server server { get; set; }
            public Artifact artifact { get; set; }
        }
        public class File
        {
            public string id { get; set; }
            public string sha1 { get; set; }
            public int size { get; set; }
            public string url { get; set; }
        }
        public class JavaVersion
        {
            public string component { get; set; }
            public int majorVersion { get; set; }
        }
        public class Library
        {
            public Downloads downloads { get; set; }
            public string name { get; set; }
            public List<Rule> rules { get; set; }
        }
        public class Logging
        {
            public Client client { get; set; }
        }
        public class OS
        {
            public string name { get; set; }
        }
        public class Rule
        {
            public string action { get; set; }
            public OS os { get; set; }
        }
        public class Server
        {
            public string sha1 { get; set; }
            public int size { get; set; }
            public string url { get; set; }
        }
    }
}