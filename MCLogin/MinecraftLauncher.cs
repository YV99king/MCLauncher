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
    public Login Login { get; }
    public DirectoryInfo MinecraftPath { get; }
    public string Version { get; }
    public MinecraftLoader Loader { get; }

    public MinecraftLauncher(string version, Login login, DirectoryInfo minecraftPath, MinecraftLoader loader = MinecraftLoader.Vanila)
    {
        Login = login ?? throw new ArgumentNullException(nameof(login));
        MinecraftPath = minecraftPath ?? throw new ArgumentNullException(nameof(minecraftPath));
        Version = CheckVersionString(version) ? version : throw new ArgumentException("Invalid version ID", nameof(version));
        Loader = !string.IsNullOrWhiteSpace(loader.ToString()) ? loader : throw new ArgumentOutOfRangeException(nameof(loader));
    }

    public static bool CheckVersionString(string version)
    {
        return !string.IsNullOrEmpty(version);
    }

    public async Task InstallMinecraft()
    {
        if (Loader == MinecraftLoader.Vanila)
        {
            
        }
        else if (Loader != MinecraftLoader.custom)
            throw new NotImplementedException();

        VersionJsonRoot versionJson;
        using (var versionJsonStream = new FileStream(Path.Combine(MinecraftPath.FullName,
                                                                      "versions",
                                                                      Version,
                                                                      Version + ".json"),
                                                         FileMode.Open))
        {
            var versionJsonNode = JsonNode.Parse(versionJsonStream);
            if (versionJsonNode["inheritsFrom"] is { } inheritsFrom)
            {
                MinecraftLauncher inheritVersion = new((string)inheritsFrom, null, MinecraftPath); //TODO: fix login is null
                await inheritVersion.InstallMinecraft();
            }
            versionJson = DeserializeJson(versionJsonNode, MinecraftPath);
        }

        List<Task> tasks = [];
        tasks.Add(InstallLibrariesAsync(versionJson.id, versionJson.libraries));
        tasks.Add(InstallAssetsAsync(versionJson));

        HttpClient client = new();

        if (versionJson.logging.client != null)
            tasks.Add(Utils.DownloadFileAsync(client,
                                          url: versionJson.logging.client.file.url,
                                          path: Path.Combine(MinecraftPath.FullName, "assets", "log_configs", versionJson.logging.client.file.id),
                                          sha1: versionJson.logging.client.file.sha1,
                                          log: Console.WriteLine));

        if (versionJson.downloads.client != null)
            tasks.Add(Utils.DownloadFileAsync(client,
                                          url: versionJson.downloads.client.url,
                                          path: Path.Combine(MinecraftPath.FullName, "versions", versionJson.id, versionJson.id + ".jar"),
                                          //sha1: versionJson.downloads.client.sha1,
                                          log: Console.WriteLine));

        await Task.WhenAll(tasks);
    }
    private async Task InstallLibrariesAsync(string vesionId, List<Library> libraries)
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
                var libPath = library.GetLibraryPath(MinecraftPath.FullName, false);
                var libPathNative = library.GetLibraryPath(MinecraftPath.FullName, true);

                List<Task> tasks = [];

                tasks.Add(Utils.DownloadFileAsync(client, libUrl, libPath, log: Console.WriteLine));

                if (library.downloads == null)
                {
                    if (library.extract != null)
                    {
                        ExtractNativesFile(libPathNative, Path.Combine(MinecraftPath.FullName, "versions", vesionId, "natives"), library.extract);
                    }
                    return Task.WhenAll(tasks);
                }

                if (library.downloads.artifact is { } artifact && !string.IsNullOrWhiteSpace(artifact.url) && artifact.path != null)
                    tasks.Add(Utils.DownloadFileAsync(client, artifact.url, Path.Combine(MinecraftPath.FullName, "libraries", artifact.path), libSha1, overwrite: true, log: Console.WriteLine));

                if (libUrlNative != null)
                {
                    tasks.Add(Utils.DownloadFileAsync(client, libUrlNative, libPathNative, libSha1Native, overwrite: true, log: Console.WriteLine).ContinueWith(task =>
                        ExtractNativesFile(libPathNative, Path.Combine(MinecraftPath.FullName, "versions", vesionId, "natives"), library.extract)));
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

        await Utils.DownloadFileAsync(client, versionJson.assetIndex.url, Path.Combine(MinecraftPath.FullName, "assets", "indexes", versionJson.assets + ".json"), log: Console.WriteLine);
        KeyValuePair<string, JsonNode>[] assets;
        using (var assetIndexStream = new FileStream(Path.Combine(MinecraftPath.FullName, "assets", "indexes", versionJson.assets + ".json"), FileMode.Open))
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
                                                   path: Path.Combine(MinecraftPath.FullName,
                                                                      "assets",
                                                                      "objects",
                                                                      ((string)asset.Value["hash"])[..2],
                                                                      (string)asset.Value["hash"]),
                                                   sha1: (string)asset.Value["hash"],
                                                   log: Console.WriteLine);

                }
                catch (Exception)
                {
                    return Task.FromResult(false);
                }
            }));
        await Task.WhenAll(tasks);
    }

    public Process LaunchMinecraft(Options options)
    {
        var loginInfo = Login.GetProfileInfo();
        options.token ??= Login.AccessToken;
        options.uuid ??= loginInfo.id;
        options.username ??= loginInfo.name;

        options.nativesDirectory ??= Path.Combine(MinecraftPath.FullName, "versions", Version, "natives");

        VersionJsonRoot versionJson;
        using (Stream versionJsonStream = new FileStream(Path.Combine(MinecraftPath.FullName,
                                                                      "versions",
                                                                      Version,
                                                                      Version + ".json"),
                                                         FileMode.Open))
        {
            versionJson = DeserializeJson(versionJsonStream, MinecraftPath); 
        }

        StringBuilder minecraftCommandBuilder = new();
        if (options.executablePath != null)
            minecraftCommandBuilder.Append($"{options.executablePath} ");
        else
            minecraftCommandBuilder.Append(Path.Combine(MinecraftPath.FullName, "runtime", versionJson.javaVersion.component, PlatformInfo.JavaPlatformName, versionJson.javaVersion.component, "bin", "java") + " ");
        
        if (options.jvmArguments != null && options.jvmArguments.Count > 0)
            minecraftCommandBuilder.Append(string.Join(' ', options.jvmArguments) + ' ');
        if (versionJson.arguments.jvm != null)
            minecraftCommandBuilder.Append(ParseArgumentsList(versionJson.arguments.jvm, options, versionJson, MinecraftPath.FullName));
        else
        {
            minecraftCommandBuilder.Append("-Djava.library.path=").Append(Path.Combine(MinecraftPath.FullName, "versions", versionJson.id, "natives") + ' ');
            minecraftCommandBuilder.Append("-cp ");
            minecraftCommandBuilder.Append(GetLibrariesString(versionJson, MinecraftPath.FullName));
        }

        if (options.enableLoggingConfig && versionJson.logging.client != null)
            minecraftCommandBuilder.Append(versionJson.logging.client.argument.Replace("${path}",
                                                                                       Path.Combine(MinecraftPath.FullName,
                                                                                                    "assets",
                                                                                                    "log_configs",
                                                                                                    versionJson.logging.client.file.id)));

        minecraftCommandBuilder.Append(versionJson.mainClass + ' ');

        if (versionJson.minecraftArguments != null)
            minecraftCommandBuilder.Append(ParseArgumentsString(versionJson.minecraftArguments, options, versionJson, MinecraftPath.FullName));
        else
            minecraftCommandBuilder.Append(ParseArgumentsList(versionJson.arguments.game, options, versionJson, MinecraftPath.FullName));

        if (options.server != null)
        {
            minecraftCommandBuilder.Append("--server");
            minecraftCommandBuilder.Append(options.server);
            if (options.port != -1)
            {
                minecraftCommandBuilder.Append("--port");
                minecraftCommandBuilder.Append(options.port);
            }
        }

        if (options.disableMultiplayer)
            minecraftCommandBuilder.Append("--disableMultiplayer");
        if (options.disableChat)
            minecraftCommandBuilder.Append("--disableChat");

        var cmdLine = minecraftCommandBuilder.ToString();

        return PlatformInfo.StartProcess(cmdLine);
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
        ReplaceArgWithLazyEvaluation(ref argstr, "${natives_directory}", () => options.nativesDirectory);
        ReplaceArgWithLazyEvaluation(ref argstr, "${launcher_name}", () => !string.IsNullOrEmpty(options.launcherName) ? options.launcherName : Utils.LauncherName);
        ReplaceArgWithLazyEvaluation(ref argstr, "${launcher_version}", () => !string.IsNullOrEmpty(options.launcherVersion) ? options.launcherVersion : Utils.LauncherVersion);
        ReplaceArgWithLazyEvaluation(ref argstr, "${classpath}", () => GetLibrariesString(versionJson, minecraftPath));
        ReplaceArgWithLazyEvaluation(ref argstr, "${auth_player_name}", () => options.username);
        ReplaceArgWithLazyEvaluation(ref argstr, "${version_name}", () => versionJson.id);
        ReplaceArgWithLazyEvaluation(ref argstr, "${game_directory}", () => !string.IsNullOrEmpty(options.gameDirectory) ? options.gameDirectory : minecraftPath);
        ReplaceArgWithLazyEvaluation(ref argstr, "${assets_root}", () => Path.Combine(minecraftPath, "assets"));
        ReplaceArgWithLazyEvaluation(ref argstr, "${assets_index_name}", () => !string.IsNullOrEmpty(versionJson.assets) ? versionJson.assets : versionJson.id);
        ReplaceArgWithLazyEvaluation(ref argstr, "${auth_uuid}", () => options.uuid);
        ReplaceArgWithLazyEvaluation(ref argstr, "${auth_access_token}", () => options.token);
        ReplaceArgWithLazyEvaluation(ref argstr, "${user_type}", () => "msa");
        ReplaceArgWithLazyEvaluation(ref argstr, "${version_type}", () => versionJson.type);
        ReplaceArgWithLazyEvaluation(ref argstr, "${user_properties}", () => "{}");
        ReplaceArgWithLazyEvaluation(ref argstr, "${resolution_width}", () => options.resolutionWidth.ToString());
        ReplaceArgWithLazyEvaluation(ref argstr, "${resolution_height}", () => options.resolutionHeight.ToString());
        ReplaceArgWithLazyEvaluation(ref argstr, "${game_assets}", () => Path.Combine(minecraftPath, "assets", "virtual", "legacy"));
        ReplaceArgWithLazyEvaluation(ref argstr, "${auth_session}", () => options.token);
        ReplaceArgWithLazyEvaluation(ref argstr, "${library_directory}", () => Path.Combine(minecraftPath, "libraries"));
        ReplaceArgWithLazyEvaluation(ref argstr, "${classpath_separator}", () => PlatformInfo.ClasspathSeparator.ToString());
        ReplaceArgWithLazyEvaluation(ref argstr, "${quickPlayPath}", () => options.quickPlayPath);
        ReplaceArgWithLazyEvaluation(ref argstr, "${quickPlaySingleplayer}", () => options.quickPlaySingleplayer);
        ReplaceArgWithLazyEvaluation(ref argstr, "${quickPlayMultiplayer}", () => options.quickPlayMultiplayer);
        ReplaceArgWithLazyEvaluation(ref argstr, "${quickPlayRealms}", () => options.quickPlayRealms);
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
        if (options.customResolution)
            arguments += "--width" + options.resolutionWidth + "--height" + options.resolutionHeight;
        if (options.demo)
            arguments += "--demo";
        return arguments;
    }

    public record struct Options
    {
        public bool customResolution = false;
        public bool demo = false;
        public bool disableChat = false;
        public bool disableMultiplayer = false;
        public bool enableLoggingConfig = false;
        public string executablePath;
        public string gameDirectory;
        public List<string> jvmArguments;
        public string launcherName;
        public string launcherVersion;
        public string nativesDirectory;
        public int port = -1;
        public string quickPlayMultiplayer;
        public string quickPlayPath;
        public string quickPlayRealms;
        public string quickPlaySingleplayer;
        public int resolutionHeight;
        public int resolutionWidth;
        public string server;
        public string token;
        public string username;
        public string uuid;

        public Options() { }
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
        public static VersionJsonRoot DeserializeJson(string json, DirectoryInfo minecraftPath) =>
            DeserializeJson(JsonNode.Parse(json), minecraftPath);
        public static VersionJsonRoot DeserializeJson(Stream stream, DirectoryInfo minecraftPath) =>
            DeserializeJson(JsonNode.Parse(stream), minecraftPath);
        public static VersionJsonRoot DeserializeJson(JsonNode json, DirectoryInfo minecraftPath)
            => JsonSerializer.Deserialize<VersionJsonRoot>(InheritJson(json, minecraftPath), options);

        private static JsonNode InheritJson(JsonNode originalJson, DirectoryInfo minecraftPath)
        {
            if (!CheckVersionString((string)originalJson["inheritsFrom"]))
                return originalJson.DeepClone();

            JsonNode inheritedJson;
            using (var inheritedJsonStream = new FileStream(Path.Combine(minecraftPath.FullName,
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
                string libUrl = string.Join('/', [baseUrl, "libraries", .. basePath.Split('.'), libname, version]);
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

                        if (nativeClassifier.sha1 is { Length: > 0})
                            sha1 = nativeClassifier.sha1;
                        if (nativeClassifier.url is { } nativePath)
                            return string.Join('/', libUrl, nativePath); 
                    }
                    return null;
                }
                sha1 = downloads?.artifact?.sha1;
                return string.Join('/', libUrl, $"{string.Join('-', [libname, version ,.. nameParts[3..]])}.{fileEnding}");
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
                if (os != null && os.name != null)
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
                match = match && os != null && (os.arch == null || (!PlatformInfo.Is64Bit) == (os.arch == "x86"));
                if (match && features != null)
                    foreach (var feature in features)
                        switch (feature)
                        {
                            case Feature.has_custom_resolution:
                                match = options.customResolution; break;
                            case Feature.has_quick_plays_support:
                                match = options.quickPlayPath != null; break;
                            case Feature.is_demo_user:
                                match = options.demo; break;
                            case Feature.is_quick_play_multiplayer:
                                match = options.quickPlayMultiplayer != null; break;
                            case Feature.is_quick_play_realms:
                                match = options.quickPlayRealms != null; break;
                            case Feature.is_quick_play_singleplayer:
                                match = options.quickPlaySingleplayer != null; break;
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