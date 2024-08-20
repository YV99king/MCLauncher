using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static MCLauncher.MinecraftLauncher.VersionJson;

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

    public void LaunchMinecraft(Options options)
    {
        var loginInfo = _login.GetProfileInfo();
        options.token ??= _login.AccessToken;
        options.uuid ??= loginInfo.id;
        options.username ??= loginInfo.name;

        var versionJson = DeserializeJson(new FileStream(Path.Combine(_minecraftPath.FullName, "versions", _version, _version+".json"), FileMode.Open));

        StringBuilder minecraftCommandBuilder = new($"/C ");
        if (options.executablePath != null)
            minecraftCommandBuilder.Append($"{options.executablePath} ");
        else
            minecraftCommandBuilder.Append(Path.Combine(_minecraftPath.FullName,
                                                        "runtime",
                                                        versionJson.javaVersion.component,
                                                        PlatformInfo.GetJavaPlatformName(),
                                                        versionJson.javaVersion.component,
                                                        "bin", "java") + " ");
        
        if (options.jvmArguments != null && options.jvmArguments.Count > 0)
            minecraftCommandBuilder.Append(string.Join(' ', options.jvmArguments) + ' ');
        if (versionJson.arguments.jvm != null)
            minecraftCommandBuilder.Append(ParseArgumentsList(versionJson.arguments.jvm, options, versionJson, _minecraftPath.FullName));
        else
        {
            minecraftCommandBuilder.Append("-Djava.library.path=").Append(Path.Combine(_minecraftPath.FullName, "versions", versionJson.id, "natives") + ' ');
            minecraftCommandBuilder.Append("-cp ");
            minecraftCommandBuilder.Append(GetLibrariesString(versionJson, _minecraftPath.FullName));
        }

        if (options.enableLoggingConfig && versionJson.logging.client != null)
            minecraftCommandBuilder.Append(versionJson.logging.client.argument.Replace("${path}",
                                                                                       Path.Combine(_minecraftPath.FullName,
                                                                                                    "assets",
                                                                                                    "log_configs",
                                                                                                    versionJson.logging.client.file.id)));

        minecraftCommandBuilder.Append(versionJson.mainClass + ' ');

        if (versionJson.minecraftArguments != null)
            minecraftCommandBuilder.Append(ParseArgumentsString(versionJson.minecraftArguments, options, versionJson, _minecraftPath.FullName));
        else
            minecraftCommandBuilder.Append(ParseArgumentsList(versionJson.arguments.game, options, versionJson, _minecraftPath.FullName));

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
        Console.WriteLine("start");
        PlatformInfo.StartProcess(minecraftCommandBuilder.ToString());
    }
    private static string ParseArgumentsList(List<Arguments.ArgumentInfo> args, Options options, VersionJsonRoot versionJson, string minecraftPath)
    {
        var builder = new StringBuilder();
        foreach (var arg in args)
            if (Rule.IsRuleListMatching(arg.rules, options))
                builder.Append(string.Join(' ', arg.value) + ' ');
        return ReplaceArguments(builder.ToString(), versionJson, minecraftPath, options, GetLibrariesString(versionJson, minecraftPath));
    }
    private static string ReplaceArguments(string argstr, VersionJsonRoot versionJson, string minecraftPath, Options options, string classpath)
    {
        argstr = argstr.Replace("${natives_directory}", options.nativesDirectory);
        argstr = argstr.Replace("${launcher_name}", !string.IsNullOrEmpty(options.launcherName) ? options.launcherName : Utils.LauncherName);
        argstr = argstr.Replace("${launcher_version}", !string.IsNullOrEmpty(options.launcherVersion) ? options.launcherVersion : Utils.LauncherVersion);
        argstr = argstr.Replace("${classpath}", classpath);
        argstr = argstr.Replace("${auth_player_name}", options.username);
        argstr = argstr.Replace("${version_name}", versionJson.id);
        argstr = argstr.Replace("${game_directory}", !string.IsNullOrEmpty(options.gameDirectory) ? options.gameDirectory : minecraftPath);
        argstr = argstr.Replace("${assets_root}", Path.Combine(minecraftPath, "assets"));
        argstr = argstr.Replace("${assets_index_name}", !string.IsNullOrEmpty(versionJson.assets) ? versionJson.assets : versionJson.id);
        argstr = argstr.Replace("${auth_uuid}", options.uuid);
        argstr = argstr.Replace("${auth_access_token}", options.token);
        argstr = argstr.Replace("${user_type}", "msa");
        argstr = argstr.Replace("${version_type}", versionJson.type);
        argstr = argstr.Replace("${user_properties}", "{}");
        argstr = argstr.Replace("${resolution_width}", options.resolutionWidth.ToString());
        argstr = argstr.Replace("${resolution_height}", options.resolutionHeight.ToString());
        argstr = argstr.Replace("${game_assets}", Path.Combine(minecraftPath, "assets", "virtual", "legacy"));
        argstr = argstr.Replace("${auth_session}", options.token);
        argstr = argstr.Replace("${library_directory}", Path.Combine(minecraftPath, "libraries"));
        argstr = argstr.Replace("${classpath_separator}", PlatformInfo.GetClasspathSeparator().ToString());
        argstr = argstr.Replace("${quickPlayPath}", options.quickPlayPath);
        argstr = argstr.Replace("${quickPlaySingleplayer}", options.quickPlaySingleplayer);
        argstr = argstr.Replace("${quickPlayMultiplayer}", options.quickPlayMultiplayer);
        argstr = argstr.Replace("${quickPlayRealms}", options.quickPlayRealms);
        return argstr;
    }
    private static string GetLibrariesString(VersionJsonRoot versionJson, string minecraftPath)
    {
        StringBuilder libString = new();
        foreach (var library in versionJson.libraries)
        {
            if (Rule.IsRuleListMatching(library.rules, default))
                continue;
            libString.Append(library.GetLibraryPath(minecraftPath, false) + PlatformInfo.GetClasspathSeparator());
            libString.Append(library.GetLibraryPath(minecraftPath, true) + PlatformInfo.GetClasspathSeparator());
        }
        if (versionJson.jar != null)
            libString.Append(Path.Combine(minecraftPath, "versions", versionJson.jar, $"{versionJson.jar}.jar"));
        else
            libString.Append(Path.Combine(minecraftPath, "versions", versionJson.id, $"{versionJson.id}.jar"));
        return libString.ToString();
    }
    private static string ParseArgumentsString(string arguments, Options options, VersionJsonRoot versionJson, string minecraftPath)
    {
        arguments = ReplaceArguments(arguments.Trim(), versionJson, minecraftPath, options, null);
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
        public static VersionJsonRoot DeserializeJson(string json) =>
            JsonSerializer.Deserialize<VersionJsonRoot>(json, options);
        public static VersionJsonRoot DeserializeJson(Stream stream) =>
            JsonSerializer.Deserialize<VersionJsonRoot>(stream, options);
        public static VersionJsonRoot DeserializeJson(JsonDocument json) =>
            JsonSerializer.Deserialize<VersionJsonRoot>(json, options);

        public static VersionJsonRoot InheritJson(VersionJsonRoot originalJson, DirectoryInfo minecraftPath)
        {
            VersionJsonRoot inheritedJson;
            using var inheritedJsonStream = new FileStream(Path.Combine(minecraftPath.FullName, "versions", originalJson.inheritsFrom, originalJson.inheritsFrom + ".json"), FileMode.Open);
            inheritedJson = DeserializeJson(inheritedJsonStream);
            throw new NotImplementedException(); //TODO: implement `InheritsFrom` method
        }

        public record Arguments
        {
            public List<ArgumentInfo> game;
            public List<ArgumentInfo> jvm;

            public record ArgumentInfo
            {
                public List<Rule> rules;
                public List<string> value;
            }
        }
        public record AssetIndex
        {
            public string id;
            public string sha1;
            public int size;
            public int totalSize;
            public string url;
        }
        public record JavaVersion
        {
            public string component;
            public int majorVersion;
        }
        public record Library
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
                string libdir =  Path.Combine(path, "libraries", Path.Combine(basePath.Split('.')), libname, version);
                if (includeNatives && natives != null)
                    switch (PlatformInfo.OperatingSystem)
                    {
                        case PlatformInfo.OS.Windows:
                            return Path.Combine(libdir, $"{libname}-{version}-{natives.windows}.jar");
                        case PlatformInfo.OS.Linux:
                            return Path.Combine(libdir, $"{libname}-{version}-{natives.linux}.jar");
                        case PlatformInfo.OS.MacOS:
                            return Path.Combine(libdir, $"{libname}-{version}-{natives.osx}.jar");
                    }
                return Path.Combine(libdir, $"{libname}-{version}.jar");
            }

            public record Extract
            {
                public List<string> exclude;
            }
            public record LibraryDownloads
            {
                public Artifact artifact;
                public Classifiers classifiers;

                public record Artifact
                {
                    public string path;
                    public string sha1;
                    public int size;
                    public string url;
                }
                public record Classifiers
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
            public record Natives
            {
                public string linux;
                public string osx;
                public string windows;
            }
        }
        public record Logging
        {
            public LoggingInfo client;

            public record LoggingInfo
            {
                public string argument;
                public File file;
                public string type;

                public record File
                {
                    public string id;
                    public string sha1;
                    public int size;
                    public string url;
                }
            }
        }
        public record MainExecutablesDownloads
        {
            public SourceInfo client;
            public SourceInfo server;

            public record SourceInfo
            {
                public string sha1;
                public int size;
                public string url;
            }
        }
        public record Rule
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

            public record OS
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
        public record VersionJsonRoot
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