using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        var versionJson = VersionJson.DeserializeJson(new FileStream(Path.Combine(_minecraftPath.FullName, "versions", _version), FileMode.Open));
        StringBuilder minecraftCommandBuilder = new($"/C ");
        if (options.executablePath != null)
            minecraftCommandBuilder.Append($"{options.executablePath} ");
        else
            minecraftCommandBuilder.Append(Path.Combine(_minecraftPath.FullName,
                                                        "runtime",
                                                        versionJson.javaVersion.component,
                                                        PlatformInfo.GetJavaPlatformName(),
                                                        versionJson.javaVersion.component,
                                                        @"bin\java.exe") + " ");
        if (options.jvmArguments != null)
            minecraftCommandBuilder.Append(options.jvmArguments);

    }

    public record Options
    {
        public string username;
        public string uuid;
        public string token;
        public string executablePath;
        public List<string> jvmArguments;
        public string launcherName;
        public string launcherVersion;
        public string gameDirectory;
        public bool demo = false;
        public bool customResolution = false;
        public string resolutionWidth;
        public string resolutionHeight;
        public string server;
        public int port;
        public string nativesDirectory;
        public bool enableLoggingConfig = false;
        public bool disableMultiplayer = false;
        public bool disableChat = false;
        public string quickPlayPath;
        public string quickPlaySingleplayer;
        public string quickPlayMultiplayer;
        public string quickPlayRealms;
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