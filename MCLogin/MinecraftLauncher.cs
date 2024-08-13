using System;
using System.Collections.Generic;
using System.IO;
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
        StringBuilder minecraftCommandBuilder = new($"/C {Path.Combine(_minecraftPath.FullName, "runtime", versionJson.javaVersion.component, PlatformInfo.GetJavaPlatformName(), versionJson.javaVersion.component, @"bin\java.exe")}");
    }

    public class Options(string username, string uuid, string token)
    {
        public string username = username;
        public string uuid = uuid;
        public string token = token;
        public string executablePath;
        public string defaultExecutablePath;
        public List<string> jvmArguments;
        public string launcherName;
        public string launcherVersion;
        public string gameDirectory;
        public bool demo;
        public bool customResolution;
        public string resolutionWidth;
        public string resolutionHeight;
        public string server;
        public string port;
        public string nativesDirectory;
        public bool enableLoggingConfig;
        public bool disableMultiplayer;
        public bool disableChat;
        public string quickPlayPath;
        public string quickPlaySingleplayer;
        public string quickPlayMultiplayer;
        public string quickPlayRealms;
    }

    private class VersionJson
    {
        private static readonly JsonSerializerOptions options = new()
        {
            Converters = { new ArgumentInfoJsonConverter() },
            WriteIndented = true
        };
        private class ArgumentInfoJsonConverter : JsonConverter<Arguments>
        {
            public override Arguments Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var arguments = new Arguments();

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        return arguments;
                    }

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
                        {
                            break;
                        }

                        if (reader.TokenType == JsonTokenType.String)
                        {
                            if (list.Count > 0 && list[^1].rules == null)
                                list[^1].value.Add(reader.GetString());
                            else
                                list.Add(new Arguments.ArgumentInfo
                                {
                                    value = [reader.GetString()]
                                });
                        }
                        else if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            var argumentInfo = JsonSerializer.Deserialize<Arguments.ArgumentInfo>(ref reader, options);
                            list.Add(argumentInfo);
                        }
                    }
                }

                return list;
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
        public static VersionJsonRoot DeserializeJson(string json) =>
            JsonSerializer.Deserialize<VersionJsonRoot>(json, options);
        public static VersionJsonRoot DeserializeJson(Stream stream) =>
            JsonSerializer.Deserialize<VersionJsonRoot>(stream, options);
        public static VersionJsonRoot DeserializeJson(JsonDocument json) =>
            JsonSerializer.Deserialize<VersionJsonRoot>(json, options);


#pragma warning disable IDE1006 // Naming Styles
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
            public string name;
            public List<Rule> rules;
            public Extract extract;
            public Natives natives;

            public record Extract
            {
                public List<string> exclude;
            }
            public record LibraryDownloads
            {
                public Artifact artifact;

                public record Artifact
                {
                    public string path;
                    public string sha1;
                    public int size;
                    public string url;
                }
                public record Classifiers
                {
                    [JsonPropertyName("natives-linux")]
                    public NativesSource nativeslinux;
                    [JsonPropertyName("natives-osx")]
                    public NativesSource nativesosx;
                    [JsonPropertyName("natives-windows")]
                    public NativesSource nativeswindows;

                    public record NativesSource
                    {
                        public string path;
                        public string sha1;
                        public int size;
                        public string url;
                    }
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
            public JavaVersion javaVersion;
            public List<Library> libraries;
            public Logging logging;
            public string mainClass;
            public string minecraftArguments;
            public string type;
        }
#pragma warning restore IDE1006 // Naming Styles
    }
}