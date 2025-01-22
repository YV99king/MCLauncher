using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static MCLauncher.MinecraftLauncher.VersionJson;

namespace MCLauncher;

public partial class MinecraftLauncher
{
    public ILoginProvider Login { get; }
    public string MinecraftDirectory { get; }
    public string Version { get; }
    public MinecraftLoader Loader { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MinecraftLauncher">.</see>
    /// </summary>
    /// <param name="version">The minecraft version to use.</param>
    /// <param name="minecraftDirectory">The path to the Minecraft directory. Fallbacks to <see cref="Utils.DefaultMinecraftDirectory"/> if the provided value is empty or invalid.</param>
    /// <param name="login">The <see cref="ILoginProvider"/> to use to login to Minecraft.</param>
    /// <param name="loader">The <see cref="MinecraftLoader"/> to use to launch the game.</param>
    /// <exception cref="ArgumentException">Parameter <paramref name="version"/> is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Invalid value passed argument <paramref name="loader"/>.</exception>
    public MinecraftLauncher(string version, string minecraftDirectory, ILoginProvider login = null, MinecraftLoader loader = MinecraftLoader.Vanila)
    {
        Version = CheckVersionString(version) ? version : throw new ArgumentException("Invalid version ID", nameof(version));
        MinecraftDirectory = Utils.NormalizePath(minecraftDirectory, Utils.DefaultMinecraftDirectory);
        Login = login ?? ILoginProvider.Empty;
        Loader = Enum.IsDefined(loader) ? loader : throw new ArgumentOutOfRangeException(nameof(loader));
    }

    public static bool CheckVersionString(string version)
    {
        return !string.IsNullOrEmpty(version);
    }

    public async Task InstallMinecraft()
    {
        HttpClient client = new();
        var versionId = Version;

        if (Loader == MinecraftLoader.Vanila)
        {
            const string versionManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";

            await Utils.DownloadFileAsync(client, versionManifestUrl, Path.Combine(MinecraftDirectory, "versions", "version_manifest_v2.json"), overwrite: true);
            JsonNode versionManifestJson;
            using (var manifestStream = new FileStream(Path.Combine(MinecraftDirectory, "versions", "version_manifest_v2.json"), FileMode.Open, FileAccess.Read))
            {
                versionManifestJson = JsonNode.Parse(manifestStream);
            }

            JsonNode versionInfo;
            if (Version == "release" || Version == "snapshot")
                versionInfo = versionManifestJson["versions"].AsArray().FirstOrDefault(node => (string)node["id"] == (string)versionManifestJson["latest"][Version]);
            else
                versionInfo = versionManifestJson["versions"].AsArray().FirstOrDefault(node => (string)node["id"] == Version);

            if (versionInfo is null)
                throw new InvalidOperationException($"Version '{Version}' is not a valid vanila version.");
            versionId = (string)versionInfo["id"];

            await Utils.DownloadFileAsync(client,
                                          url: (string)versionInfo["url"],
                                          path: Path.Combine(MinecraftDirectory, "versions", versionId, versionId + ".json"),
                                          sha1: (string)versionInfo["sha1"]);
        }
        else if (Loader != MinecraftLoader.custom)
            throw new NotImplementedException();

        VersionJsonRoot versionJson;
        using (var versionJsonStream = new FileStream(Path.Combine(MinecraftDirectory, "versions", versionId, versionId + ".json"), FileMode.Open, FileAccess.Read))
        {
            var versionJsonNode = JsonNode.Parse(versionJsonStream);
            if (versionJsonNode["inheritsFrom"] is { } inheritsFrom)
            {
                MinecraftLauncher inheritVersion = new((string)inheritsFrom, MinecraftDirectory, null); //TODO: fix login is null
                await inheritVersion.InstallMinecraft();
            }
            versionJson = DeserializeJson(versionJsonNode, MinecraftDirectory);
        }

        List<Task> tasks = [];
        tasks.Add(InstallLibrariesAsync(versionJson.id, versionJson.libraries));
        tasks.Add(InstallAssetsAsync(versionJson));
        tasks.Add(InstallJavaRuntimesAsync(versionJson.javaVersion));

        if (versionJson.logging.client != null)
            tasks.Add(Utils.DownloadFileAsync(client,
                                              url: versionJson.logging.client.file.url,
                                              path: Path.Combine(MinecraftDirectory, "assets", "log_configs", versionJson.logging.client.file.id),
                                              sha1: versionJson.logging.client.file.sha1));

        if (versionJson.downloads.client != null)
            tasks.Add(Utils.DownloadFileAsync(client,
                                              url: versionJson.downloads.client.url,
                                              path: Path.Combine(MinecraftDirectory, "versions", versionJson.id, versionJson.id + ".jar"),
                                              sha1: versionJson.downloads.client.sha1));

        await Task.WhenAll(tasks);
    }
    private async Task InstallLibrariesAsync(string versionId, List<Library> libraries)
    {
        HttpClient client = new();

        List<Task> tasks = new(libraries.Count);
        foreach (var library in libraries)
        {
            if (!Rule.IsRuleListMatching(library.rules, default))
                continue;

            tasks.Add(Task.Run(() =>
            {

                var libUrl = library.GetLibraryUrl(false, out var libSha1);
                var libUrlNative = library.GetLibraryUrl(true, out var libSha1Native);
                var libPath = library.GetLibraryPath(MinecraftDirectory, false);
                var libPathNative = library.GetLibraryPath(MinecraftDirectory, true);

                List<Task> tasks = [];

                tasks.Add(Utils.DownloadFileAsync(client, libUrl, libPath));

                if (library.downloads == null)
                {
                    if (library.extract != null)
                    {
                        ExtractNativesFile(libPathNative, Path.Combine(MinecraftDirectory, "versions", versionId, "natives"), library.extract);
                    }
                    return Task.WhenAll(tasks);
                }

                if (library.downloads.artifact is { } artifact && !string.IsNullOrWhiteSpace(artifact.url) && artifact.path != null)
                    tasks.Add(Utils.DownloadFileAsync(client, artifact.url, Path.Combine(MinecraftDirectory, "libraries", artifact.path), libSha1, overwrite: true));

                if (libUrlNative != null)
                {
                    tasks.Add(Utils.DownloadFileAsync(client, libUrlNative, libPathNative, libSha1Native, overwrite: true).ContinueWith(task =>
                        ExtractNativesFile(libPathNative, Path.Combine(MinecraftDirectory, "versions", versionId, "natives"), library.extract)));
                }

                return Task.WhenAll(tasks);

                static void ExtractNativesFile(string filename, string extractPath, Library.Extract extract)
                {
                    Directory.CreateDirectory(extractPath);

                    using var zipStream = new FileStream(filename, FileMode.Open);
                    using var zip = new ZipArchive(zipStream);
                    foreach (var entry in zip.Entries)
                    {
                        bool isExcluded = false;
                        foreach (var excluded in extract.exclude)
                            if (entry.Name.StartsWith(excluded))
                            {
                                isExcluded = true;
                                break;
                            }
                        if (!isExcluded)
                            entry.ExtractToFile(Path.Combine(extractPath, entry.Name));
                    }
                }
            }));
        }
        await Task.WhenAll(tasks);
    }
    private async Task InstallAssetsAsync(VersionJsonRoot versionJson)
    {
        HttpClient client = new();

        await Utils.DownloadFileAsync(client, versionJson.assetIndex.url, Path.Combine(MinecraftDirectory, "assets", "indexes", versionJson.assets + ".json"));
        KeyValuePair<string, JsonNode>[] assets;
        using (var assetIndexStream = new FileStream(Path.Combine(MinecraftDirectory, "assets", "indexes", versionJson.assets + ".json"), FileMode.Open))
        {
            assets = [.. JsonNode.Parse(assetIndexStream)["objects"].AsObject()];
        }

        List<Task> tasks = new(assets.Length);
        foreach (var asset in assets)
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    return Utils.DownloadFileAsync(client,
                                                   url: string.Join('/', "https://resources.download.minecraft.net",
                                                                         ((string)asset.Value["hash"])[..2],
                                                                         (string)asset.Value["hash"]),
                                                   path: Path.Combine(MinecraftDirectory,
                                                                      "assets",
                                                                      "objects",
                                                                      ((string)asset.Value["hash"])[..2],
                                                                      (string)asset.Value["hash"]),
                                                   sha1: (string)asset.Value["hash"]);

                }
                catch (Exception)
                {
                    return Task.FromResult(false);
                }
            }));
        await Task.WhenAll(tasks);
    }
    private async Task InstallJavaRuntimesAsync(JavaVersion javaVersion)
    {
        const string runtimesManifestUrl = "https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";
        HttpClient client = new();

        if (!File.Exists(Path.Combine(MinecraftDirectory, "versions", "jre_manifest.json")))
            await Utils.DownloadFileAsync(client,
                                          url: runtimesManifestUrl,
                                          path: Path.Combine(MinecraftDirectory, "versions", "jre_manifest.json"));
        JsonNode runtimesManifestJson;
        using (var runtimesManifestStream = new FileStream(Path.Combine(MinecraftDirectory, "versions", "jre_manifest.json"), FileMode.Open, FileAccess.Read))
        {
            runtimesManifestJson = JsonNode.Parse(runtimesManifestStream);
        }
        if (runtimesManifestJson[PlatformInfo.JavaPlatformName][javaVersion.component][0] == null)
            throw new Exception($"No JRE for Java {javaVersion.component} found.");

        await Utils.DownloadFileAsync(client,
                                      url: (string)runtimesManifestJson[PlatformInfo.JavaPlatformName][javaVersion.component][0]["manifest"]["url"],
                                      path: Path.Combine(MinecraftDirectory, "runtime", javaVersion.component, PlatformInfo.JavaPlatformName, "manifest.tmp"),
                                      sha1: (string)runtimesManifestJson[PlatformInfo.JavaPlatformName][javaVersion.component][0]["manifest"]["sha1"]);
        JsonNode jvmManifestJson;
        using (var jvmManifestStream = new FileStream(Path.Combine(MinecraftDirectory, "runtime", javaVersion.component, PlatformInfo.JavaPlatformName, "manifest.tmp"), FileMode.Open, FileAccess.Read))
        {
            jvmManifestJson = JsonNode.Parse(jvmManifestStream);
        }

        List<Task> tasks = [];
        List<(string path, string sha1)> jvmFiles = [];
        string basePath = Path.Combine(MinecraftDirectory, "runtime", javaVersion.component, PlatformInfo.JavaPlatformName, javaVersion.component);
        foreach (var (filePath, fileInfo) in jvmManifestJson["files"].AsObject())
        {
            if ((string)fileInfo["type"] == "file")
                tasks.Add(Utils.DownloadFileAsync(client,
                                                  url: (string)fileInfo["downloads"]["raw"]["url"],
                                                  path: basePath + PlatformInfo.PathSeparator + filePath,
                                                  sha1: (string)fileInfo["downloads"]["raw"]["sha1"]).ContinueWith(t =>
                                                  {
                                                      if ((bool)fileInfo["executable"])
                                                          try
                                                          {
                                                              PlatformInfo.StartProcess(true, "chmod", "+x", basePath + PlatformInfo.PathSeparator + filePath);
                                                          }
                                                          catch (Exception) { }
                                                      jvmFiles.Add((basePath + PlatformInfo.PathSeparator + filePath, (string)fileInfo["downloads"]["raw"]["sha1"])); // TODO: use lzma
                                                  }));
            else if ((string)fileInfo["type"] == "link")
                try
                {
                    Directory.CreateSymbolicLink(basePath + PlatformInfo.PathSeparator + filePath, (string)fileInfo["target"]);
                }
                catch (Exception) { }
        }

        tasks.Add(File.WriteAllTextAsync(Path.Combine(MinecraftDirectory, "runtime", javaVersion.component, PlatformInfo.JavaPlatformName, ".version"),
                                         (string)runtimesManifestJson[PlatformInfo.JavaPlatformName][javaVersion.component][0]["version"]["name"]));

        tasks.Add(File.WriteAllLinesAsync(Path.Combine(MinecraftDirectory, "runtime", javaVersion.component, PlatformInfo.JavaPlatformName, $"{javaVersion.component}.sha1"),
                                          from file in jvmFiles
                                          select $"{file.path} /#// {file.sha1} {File.GetCreationTimeUtc(file.path).Ticks * 100 /* a tick is 100 nanoseconds */ }"));

        await Task.WhenAll(tasks);
    }

    public Process LaunchMinecraft(Options options)
    {
        var loginInfo = Login.UpdateProfileInfo(new()); // TODO: cache client?
        options.Token ??= Login.AccessToken;
        options.Uuid ??= loginInfo.id;
        options.Username ??= loginInfo.name;

        options.NativesDirectory ??= Path.Combine(MinecraftDirectory, "versions", Version, "natives");

        VersionJsonRoot versionJson;
        using (Stream versionJsonStream = new FileStream(Path.Combine(MinecraftDirectory, "versions", Version, Version + ".json"), FileMode.Open, FileAccess.Read))
        {
            versionJson = DeserializeJson(versionJsonStream, MinecraftDirectory);
        }

        string javaExecutable;
        List<string> minecraftArgs = [];

        if (options.ExecutablePath != null)
            javaExecutable = options.ExecutablePath;
        else
            javaExecutable = Path.Combine(MinecraftDirectory, "runtime", versionJson.javaVersion.component, PlatformInfo.JavaPlatformName, versionJson.javaVersion.component, "bin", "java");

        if (options.JvmArguments != null && options.JvmArguments.Count > 0)
            minecraftArgs.Add(string.Join(' ', options.JvmArguments) + ' ');
        if (versionJson.arguments.jvm != null)
            minecraftArgs.Add(ParseArgumentsList(versionJson.arguments.jvm, options, versionJson, MinecraftDirectory));
        else
        {
            minecraftArgs.Add("-Djava.library.path=" + Path.Combine(MinecraftDirectory, "versions", versionJson.id, "natives"));
            minecraftArgs.Add("-cp ");
            minecraftArgs.Add(GetLibrariesString(versionJson, MinecraftDirectory));
        }

        if (options.EnableLoggingConfig && versionJson.logging.client != null)
            minecraftArgs.Add(versionJson.logging.client.argument.Replace("${path}", Path.Combine(MinecraftDirectory, "assets", "log_configs", versionJson.logging.client.file.id)));

        minecraftArgs.Add(versionJson.mainClass + ' ');

        if (versionJson.minecraftArguments != null)
            minecraftArgs.Add(ParseArgumentsString(versionJson.minecraftArguments, options, versionJson, MinecraftDirectory));
        else
            minecraftArgs.Add(ParseArgumentsList(versionJson.arguments.game, options, versionJson, MinecraftDirectory));

        if (options.QuickPlay?.Values[0] != null)
        {
            minecraftArgs.AddRange(["--server", options.QuickPlay?.Values[0]]);
            if (options.QuickPlay?.Values[1] != default)
                minecraftArgs.AddRange(["--port", options.QuickPlay?.Values[1]]);
        }

        if (options.DisableMultiplayer)
            minecraftArgs.Add("--disableMultiplayer");
        if (options.DisableChat)
            minecraftArgs.Add("--disableChat");

        return PlatformInfo.StartProcess(true, javaExecutable, minecraftArgs);
    }
    private static string ParseArgumentsList(List<Arguments.ArgumentInfo> args, Options options, VersionJsonRoot versionJson, string minecraftPath)
    {
        var builder = new StringBuilder();
        foreach (var arg in args)
            if (Rule.IsRuleListMatching(arg.rules, options))
                builder.Append(string.Join(' ', arg.value) + ' ');
        return ReplaceArguments(builder.ToString(), versionJson, minecraftPath, options);
    }
    private static string ReplaceArguments(string argstr, VersionJsonRoot versionJson, string minecraftPath, Options options)
    {
        ReplaceArgWithLazyEvaluation(ref argstr, "${natives_directory}", () => options.NativesDirectory);
        ReplaceArgWithLazyEvaluation(ref argstr, "${launcher_name}", () => options.LauncherName);
        ReplaceArgWithLazyEvaluation(ref argstr, "${launcher_version}", () => options.LauncherVersion);
        ReplaceArgWithLazyEvaluation(ref argstr, "${classpath}", () => GetLibrariesString(versionJson, minecraftPath));
        ReplaceArgWithLazyEvaluation(ref argstr, "${auth_player_name}", () => options.Username);
        ReplaceArgWithLazyEvaluation(ref argstr, "${version_name}", () => versionJson.id);
        ReplaceArgWithLazyEvaluation(ref argstr, "${game_directory}", () => Utils.NormalizePath(options.GameDirectory, minecraftPath));
        ReplaceArgWithLazyEvaluation(ref argstr, "${assets_root}", () => Path.Combine(minecraftPath, "assets"));
        ReplaceArgWithLazyEvaluation(ref argstr, "${assets_index_name}", () => !string.IsNullOrEmpty(versionJson.assets) ? versionJson.assets : versionJson.id);
        ReplaceArgWithLazyEvaluation(ref argstr, "${auth_uuid}", () => options.Uuid);
        ReplaceArgWithLazyEvaluation(ref argstr, "${auth_access_token}", () => options.Token);
        ReplaceArgWithLazyEvaluation(ref argstr, "${user_type}", () => "msa");
        ReplaceArgWithLazyEvaluation(ref argstr, "${version_type}", () => versionJson.type);
        ReplaceArgWithLazyEvaluation(ref argstr, "${user_properties}", () => "{}");
        ReplaceArgWithLazyEvaluation(ref argstr, "${resolution_width}", () => options.CustomResolution?.width.ToString());
        ReplaceArgWithLazyEvaluation(ref argstr, "${resolution_height}", () => options.CustomResolution?.height.ToString());
        ReplaceArgWithLazyEvaluation(ref argstr, "${game_assets}", () => Path.Combine(minecraftPath, "assets", "virtual", "legacy"));
        ReplaceArgWithLazyEvaluation(ref argstr, "${auth_session}", () => options.Token);
        ReplaceArgWithLazyEvaluation(ref argstr, "${library_directory}", () => Path.Combine(minecraftPath, "libraries"));
        ReplaceArgWithLazyEvaluation(ref argstr, "${classpath_separator}", PlatformInfo.ClasspathSeparator.ToString);
        ReplaceArgWithLazyEvaluation(ref argstr, "${quickPlayPath}", () => options.QuickPlayPath);
        ReplaceArgWithLazyEvaluation(ref argstr, "${quickPlaySingleplayer}", () => options.QuickPlay?.Values[0]);
        ReplaceArgWithLazyEvaluation(ref argstr, "${quickPlayMultiplayer}", () => $"{options.QuickPlay?.Values[0]}:{options.QuickPlay?.Values[1]}");
        ReplaceArgWithLazyEvaluation(ref argstr, "${quickPlayRealms}", () => options.QuickPlay?.Values[0]);
        return argstr;

        static void ReplaceArgWithLazyEvaluation(ref string argstr, string oldValue, Func<string> newValueFactory)
        {
            int index;
            string newValue;
            if ((index = argstr.IndexOf(oldValue)) > -1)
            {
                newValue = newValueFactory();
                do argstr = argstr.Remove(index, oldValue.Length).Insert(index, newValue);
                while ((index = argstr.IndexOf(oldValue)) > -1);
            }
        }
    }
    private static string GetLibrariesString(VersionJsonRoot versionJson, string minecraftPath)
    {
        StringBuilder libString = new();
        foreach (var library in versionJson.libraries)
        {
            if (!Rule.IsRuleListMatching(library.rules, default))
                continue;
            libString.Append(string.Join(PlatformInfo.ClasspathSeparator,
                                         library.GetLibraryPath(minecraftPath, false),
                                         library.GetLibraryPath(minecraftPath, true),
                                         ""));
            if (libString[^2] == ';' && libString[^1] == ';')
                libString.Remove(libString.Length - 1, 1);
        }
        if (versionJson.jar != null)
            libString.Append(Path.Combine(minecraftPath, "versions", versionJson.jar, $"{versionJson.jar}.jar"));
        else
            libString.Append(Path.Combine(minecraftPath, "versions", versionJson.id, $"{versionJson.id}.jar"));
        return libString.ToString();
    }
    private static string ParseArgumentsString(string arguments, Options options, VersionJsonRoot versionJson, string minecraftPath)
    {
        arguments = ReplaceArguments(arguments.Trim(), versionJson, minecraftPath, options);
        if (options.CustomResolution != null)
            arguments += "--width" + options.CustomResolution?.width + "--height" + options.CustomResolution?.height;
        if (options.Demo)
            arguments += "--demo";
        return arguments;
    }

    public record struct Options
    {
        public HttpClient Client { get; set; }
        public (int width, int height)? CustomResolution { get; set; } = null;
        public bool Demo { get; set; } = false;
        public bool DisableChat { get; set; } = false;
        public bool DisableMultiplayer { get; set; } = false;
        public bool EnableLoggingConfig { get; set; } = false;
        public string ExecutablePath { get; set; }
        public string GameDirectory { get; set; }
        public List<string> JvmArguments { get; set; }
        public string LauncherName { get; set; } = Utils.LauncherName;
        public string LauncherVersion { get; set; } = Utils.LauncherVersion;
        public string NativesDirectory { get; set; }
        public QuickPlayOptions? QuickPlay { get; set; } = null;
        public string QuickPlayPath { get; set; } // I have no idea what this does, used by the launcher for somthing...
        public string Token { get; set; }
        public string Username { get; set; }
        public string Uuid { get; set; }

        public Options() { }

        public readonly struct QuickPlayOptions
        {
            internal QuickPlayType Type { get; }
            internal string[] Values { get; }

            private QuickPlayOptions(QuickPlayType type, string[] values)
            {
                Type = type;
                Values = values;
            }

            public static QuickPlayOptions CreateSingleplayer(string worldname)
                => new(QuickPlayType.Singleplayer, [worldname]);
            public static QuickPlayOptions CreateMultiplayer(string address, int port)
                => new(QuickPlayType.Multiplayer, [address, port.ToString()]);
            public static QuickPlayOptions CreateRealms(string realmsId)
                => new(QuickPlayType.Realms, [realmsId]);

            internal enum QuickPlayType : byte
            {
                Singleplayer,
                Multiplayer,
                Realms
            }
        }
    }

    public class VersionJson
    {
        private static readonly JsonSerializerOptions options = new()
        {
            Converters = { new ArgumentInfoJsonConverter(), new FeaturesListJsonConverter() },
            IncludeFields = true
        };
        private class ArgumentInfoJsonConverter : JsonConverter<Arguments>
        {
            public override Arguments Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException("Expected start of an object.");

                var arguments = new Arguments();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        return arguments;

                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string propertyName = reader.GetString();
                        reader.Read();

                        if (propertyName == "game")
                        {
                            arguments.game = ReadArgumentInfoList(ref reader, options);
                        }
                        else if (propertyName == "jvm")
                        {
                            arguments.jvm = ReadArgumentInfoList(ref reader, options);
                        }
                    }
                }

                throw new JsonException("Unexpected JSON format for Arguments.");
            }
            private static List<Arguments.ArgumentInfo> ReadArgumentInfoList(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                var list = new List<Arguments.ArgumentInfo>();

                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndArray)
                            return list;

                        if (reader.TokenType == JsonTokenType.String)
                        {
                            string value = reader.GetString();
                            if (list.Count > 0 && list[^1].rules == null)
                            {
                                list[^1].value.Add(value);
                            }
                            else
                            {
                                list.Add(new Arguments.ArgumentInfo
                                {
                                    value = [value]
                                });
                            }
                        }
                        else if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            if (reader.TokenType != JsonTokenType.StartObject)
                                throw new JsonException("Expected start of an object.");

                            var argumentInfo = new Arguments.ArgumentInfo();
                            while (reader.Read())
                            {
                                if (reader.TokenType == JsonTokenType.EndObject)
                                    break;

                                if (reader.TokenType == JsonTokenType.PropertyName)
                                {
                                    var propertyName = reader.GetString();
                                    reader.Read();

                                    if (propertyName == "rules" || propertyName == "compatibilityRules")
                                        argumentInfo.rules = JsonSerializer.Deserialize<List<Rule>>(ref reader, options);
                                    else if (propertyName == "value")
                                    {
                                        argumentInfo.value = [];
                                        if (reader.TokenType == JsonTokenType.String)
                                        {
                                            argumentInfo.value.Add(reader.GetString());
                                        }
                                        else if (reader.TokenType == JsonTokenType.StartArray)
                                        {
                                            while (reader.Read())
                                            {
                                                if (reader.TokenType == JsonTokenType.EndArray)
                                                    break;

                                                if (reader.TokenType == JsonTokenType.String)
                                                {
                                                    argumentInfo.value.Add(reader.GetString());
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            list.Add(argumentInfo);
                        }
                    }
                }

                throw new JsonException("Unexpected JSON format for Arguments.");
            }

            public override void Write(Utf8JsonWriter writer, Arguments value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WritePropertyName("game");
                WriteArgumentInfoList(writer, value.game, options);

                writer.WritePropertyName("jvm");
                WriteArgumentInfoList(writer, value.jvm, options);

                writer.WriteEndObject();
            }
            private static void WriteArgumentInfoList(Utf8JsonWriter writer, List<Arguments.ArgumentInfo> list, JsonSerializerOptions options)
            {
                writer.WriteStartArray();

                foreach (var argumentInfo in list)
                {
                    if (argumentInfo.rules == null && argumentInfo.value.Count == 1)
                    {
                        writer.WriteStringValue(argumentInfo.value[0]);
                    }
                    else
                    {
                        writer.WriteStartObject();

                        if (argumentInfo.rules != null)
                        {
                            writer.WritePropertyName("rules");
                            JsonSerializer.Serialize(writer, argumentInfo.rules, options);
                        }

                        if (argumentInfo.value != null)
                        {
                            writer.WritePropertyName("value");

                            if (argumentInfo.value.Count == 1)
                            {
                                writer.WriteStringValue(argumentInfo.value[0]);
                            }
                            else
                            {
                                JsonSerializer.Serialize(writer, argumentInfo.value, options);
                            }
                        }

                        writer.WriteEndObject();
                    }
                }

                writer.WriteEndArray();
            }
        }
        public class FeaturesListJsonConverter : JsonConverter<List<Rule.Feature>>
        {
            public override List<Rule.Feature> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException("Expected start of an object.");

                var features = new List<Rule.Feature>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        return features;

                    string featureName = reader.GetString();
                    reader.Read();

                    features.Add(featureName switch
                    {
                        "is_demo_user" => Rule.Feature.is_demo_user,
                        "has_custom_resolution" => Rule.Feature.has_custom_resolution,
                        "has_quick_plays_support" => Rule.Feature.has_quick_plays_support,
                        "is_quick_play_singleplayer" => Rule.Feature.is_quick_play_singleplayer,
                        "is_quick_play_multiplayer" => Rule.Feature.is_quick_play_multiplayer,
                        "is_quick_play_realms" => Rule.Feature.is_quick_play_realms,
                        _ => throw new JsonException($"Unknown feature: {featureName}")
                    });
                }
                throw new JsonException("Unexpected end of JSON.");
            }

            public override void Write(Utf8JsonWriter writer, List<Rule.Feature> value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                foreach (var feature in value)
                {
                    string featureName = feature switch
                    {
                        Rule.Feature.is_demo_user => "is_demo_user",
                        Rule.Feature.has_custom_resolution => "has_custom_resolution",
                        Rule.Feature.has_quick_plays_support => "has_quick_plays_support",
                        Rule.Feature.is_quick_play_singleplayer => "is_quick_play_singleplayer",
                        Rule.Feature.is_quick_play_multiplayer => "is_quick_play_multiplayer",
                        Rule.Feature.is_quick_play_realms => "is_quick_play_realms",
                        _ => throw new JsonException($"Unknown feature: {feature}")
                    };

                    writer.WritePropertyName(featureName);
                    writer.WriteBooleanValue(true);
                }
            }
        }
        public static VersionJsonRoot DeserializeJson(string json, string minecraftPath) =>
            DeserializeJson(JsonNode.Parse(json), minecraftPath);
        public static VersionJsonRoot DeserializeJson(Stream stream, string minecraftPath) =>
            DeserializeJson(JsonNode.Parse(stream), minecraftPath);
        public static VersionJsonRoot DeserializeJson(JsonNode json, string minecraftPath)
            => JsonSerializer.Deserialize<VersionJsonRoot>(InheritJson(json, minecraftPath), options);

        private static JsonNode InheritJson(JsonNode originalJson, string minecraftPath)
        {
            if (!CheckVersionString((string)originalJson["inheritsFrom"]))
                return originalJson.DeepClone();

            JsonNode inheritedJson;
            using (var inheritedJsonStream = new FileStream(Path.Combine(minecraftPath,
                                                                         "versions",
                                                                         (string)originalJson["inheritsFrom"],
                                                                         originalJson["inheritsFrom"] + ".json"),
                                                            FileMode.Open))
            {
                inheritedJson = JsonNode.Parse(inheritedJsonStream, new JsonNodeOptions() { });
            }

            if (originalJson.AsObject().ContainsKey("libraries"))
            {
                HashSet<string> includedLibraries = originalJson["libraries"].AsArray().Select(node => string.Join(':', ((string)node["name"]).Split(':')[..^2])).ToHashSet();
                inheritedJson["libraries"] = new JsonArray((from lib in inheritedJson["libraries"].AsArray()
                                                            where !includedLibraries.TryGetValue(string.Join(':', ((string)lib["name"]).Split(':')[..^2]), out _)
                                                            select lib).ToArray());
            }

            foreach (var item in originalJson.AsObject())
            {
                if (item.Key == "libraries" || item.Key == "inheritsFrom")
                    continue;

                if (originalJson[item.Key] is JsonArray itemAsJsonArray)
                    foreach (var jsonArray in itemAsJsonArray)
                        (inheritedJson[item.Key] as JsonArray).Add(jsonArray.DeepClone());
                else if (originalJson[item.Key] is JsonObject itemAsJsonObject)
                    foreach (var jsonObject in itemAsJsonObject)
                    {
                        inheritedJson[item.Key][jsonObject.Key] = jsonObject.Value.DeepClone();
                    }
                else
                    inheritedJson[item.Key] = item.Value.DeepClone() ?? inheritedJson[item.Key];
            }

            return inheritedJson;
        }

        public record class Arguments
        {
            public List<ArgumentInfo> game;
            public List<ArgumentInfo> jvm;

            public record class ArgumentInfo
            {
                public List<Rule> rules;
                public List<string> value;
            }
        }
        public record class AssetIndex
        {
            public string id;
            public string sha1;
            public int size;
            public int totalSize;
            public string url;
        }
        public record class JavaVersion
        {
            public string component;
            public int majorVersion;
        }
        public record class Library
        {
            public LibraryDownloads downloads;
            public Extract extract;
            public string name;
            public Natives natives;
            public List<Rule> rules;
            public string url;

            public string GetLibraryPath(string path, bool includeNatives)
            {
                var nameParts = name.Split(':');
                string basePath = nameParts[0], libname = nameParts[1], version = nameParts[2];
                string libdir = Path.Combine([path, "libraries", .. basePath.Split('.'), libname, version]);
                int index;
                string fileEnding = "jar";
                if ((index = version.IndexOf('@')) != -1)
                {
                    fileEnding = version[(index + 1)..];
                    version = version[..index];
                }
                if (includeNatives)
                {
                    if (natives?.GetNativesString() is { } nativesString)
                    {
                        var nativeClassifier = nativesString switch
                        {
                            "natives-linux" => downloads.classifiers.nativesLinux,
                            "natives-osx" => downloads.classifiers.nativesOSX,
                            "natives-windows" => downloads.classifiers.nativesWindows,
                            _ => null
                        };

                        if (nativeClassifier.path is { } nativePath)
                            return Path.Combine(path, "libraries", nativePath);
                        else
                            return Path.Combine(libdir, $"{string.Join('-', [libname, version, .. nameParts[3..], nativesString])}.{fileEnding}");
                    }
                    return null;
                }
                return Path.Combine(libdir, $"{string.Join('-', [libname, version, .. nameParts[3..]])}.{fileEnding}");
            } //TODO: cleanup, may return empty string
            public string GetLibraryUrl(bool includeNatives, out string sha1)
            {
                sha1 = "";

                string baseUrl = url?.TrimEnd('/');
                if (url == null)
                    baseUrl = "https://libraries.minecraft.net";

                var nameParts = name.Split(':');
                string basePath = nameParts[0], libname = nameParts[1], version = nameParts[2];
                string libUrl = string.Join('/', [baseUrl, .. basePath.Split('.'), libname, version]);
                int index;
                string fileEnding = "jar";
                if ((index = version.IndexOf('@')) != -1)
                {
                    fileEnding = version[(index + 1)..];
                    version = version[..index];
                }
                if (includeNatives)
                {
                    if (natives?.GetNativesString() is { } nativesString)
                    {
                        var nativeClassifier = nativesString switch
                        {
                            "natives-linux" => downloads.classifiers.nativesLinux,
                            "natives-osx" => downloads.classifiers.nativesOSX,
                            "natives-windows" => downloads.classifiers.nativesWindows,
                            _ => null
                        };

                        if (nativeClassifier.sha1 is { Length: > 0 })
                            sha1 = nativeClassifier.sha1;
                        if (nativeClassifier.url is { } nativePath)
                            return string.Join('/', libUrl, nativePath);
                    }
                    return null;
                }
                sha1 = downloads?.artifact?.sha1;
                return string.Join('/', libUrl, $"{string.Join('-', [libname, version, .. nameParts[3..]])}.{fileEnding}");
            }

            public record class Extract
            {
                public List<string> exclude;
            }
            public record class LibraryDownloads
            {
                public Artifact artifact;
                public Classifiers classifiers;

                public record class Artifact
                {
                    public string path;
                    public string sha1;
                    public int size;
                    public string url;
                }
                public record class Classifiers
                {
                    public Artifact javadoc;
                    [JsonPropertyName("natives-linux")]
                    public Artifact nativesLinux;
                    [JsonPropertyName("natives-osx")]
                    public Artifact nativesOSX;
                    [JsonPropertyName("natives-windows")]
                    public Artifact nativesWindows;
                    public Artifact sources;
                }
            }
            public record class Natives
            {
                public string linux;
                public string osx;
                public string windows;

                public string GetNativesString()
                {
                    string arch = PlatformInfo.Is64Bit ? "64" : "32";
                    var nativesString = PlatformInfo.OperatingSystem switch
                    {
                        PlatformInfo.OS.Windows => windows.Replace("${arch}", arch),
                        PlatformInfo.OS.Linux => linux.Replace("${arch}", arch),
                        PlatformInfo.OS.MacOS => osx.Replace("${arch}", arch),
                        _ => null,
                    };
                    return nativesString != "" ? nativesString : null;
                }
            }
        }
        public record class Logging
        {
            public LoggingInfo client;

            public record class LoggingInfo
            {
                public string argument;
                public File file;
                public string type;

                public record class File
                {
                    public string id;
                    public string sha1;
                    public int size;
                    public string url;
                }
            }
        }
        public record class MainExecutablesDownloads
        {
            public SourceInfo client;
            public SourceInfo server;

            public record class SourceInfo
            {
                public string sha1;
                public int size;
                public string url;
            }
        }
        public record class Rule
        {
            public string action;
            public List<Feature> features;
            public OS os;

            public bool IsRuleMatching(Options options)
            {
                bool match = true;

                if (os != null && os.name != null) // whether the OS matches
                    switch (PlatformInfo.OperatingSystem)
                    {
                        case PlatformInfo.OS.Windows:
                            if (os.name != "windows")
                                match = false; break;
                        case PlatformInfo.OS.Linux:
                            if (os.name != "linux")
                                match = false; break;
                        case PlatformInfo.OS.MacOS:
                            if (os.name != "macos")
                                match = false; break;
                    }

                match = match && os != null && (os.arch == null || (!PlatformInfo.Is64Bit) == (os.arch == "x86")); // whether the architecture matches

                if (match && features != null) // whether the features match
                    foreach (var feature in features)
                        switch (feature)
                        {
                            case Feature.has_custom_resolution:
                                match = options.CustomResolution != null; break;
                            case Feature.has_quick_plays_support:
                                match = options.QuickPlayPath != null; break;
                            case Feature.is_demo_user:
                                match = options.Demo; break;
                            case Feature.is_quick_play_multiplayer:
                                match = options.QuickPlay?.Type == Options.QuickPlayOptions.QuickPlayType.Multiplayer; break;
                            case Feature.is_quick_play_realms:
                                match = options.QuickPlay?.Type == Options.QuickPlayOptions.QuickPlayType.Realms; break;
                            case Feature.is_quick_play_singleplayer:
                                match = options.QuickPlay?.Type == Options.QuickPlayOptions.QuickPlayType.Singleplayer; break;
                        }

                if (action == "allow")
                    return match;
                if (action == "disallow")
                    return !match;
                return match; //TODO: make sure this is the right behavior
            }
            public static bool IsRuleListMatching(List<Rule> rules, Options options)
            {
                if (rules == null)
                    return true;
                bool IsRuleMatch = true;
                foreach (var rule in rules)
                    if (!rule.IsRuleMatching(options))
                    {
                        IsRuleMatch = false;
                        break;
                    }
                return IsRuleMatch;
            }

            public record class OS
            {
                public string name;
                public string arch;
            }
            public enum Feature
            {
                None = 0,
                is_demo_user,
                has_custom_resolution,
                has_quick_plays_support,
                is_quick_play_singleplayer,
                is_quick_play_multiplayer,
                is_quick_play_realms
            }
        }
        public record class VersionJsonRoot
        {
            public Arguments arguments;
            public AssetIndex assetIndex;
            public string assets;
            public MainExecutablesDownloads downloads;
            public string id;
            public string inheritsFrom;
            public string jar;
            public JavaVersion javaVersion;
            public List<Library> libraries;
            public Logging logging;
            public string mainClass;
            public string minecraftArguments;
            public string type;
        }
    }
}