using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace MCLauncher;

public enum MinecraftLoader
{
    Vanila = 0,
    Custom,
    Fabric,
    Forge,
    Quilt,
    NeoForge,
    LiteLoader
}
public static class MinecraftLoaderExtentions
{
    public static string ResolveVersion(this MinecraftLoader loader, string version = "release", string loaderVersion = null)
    {
        HttpClient client = new();

        if (loader == MinecraftLoader.Vanila)
        {
            if (version == "release" || version == "snapshot")
            {
                return "1.21"; //TODO: Implement auto version detection
            }
            return version;
        }

        if (loader == MinecraftLoader.Custom) return version; // Custom versions are not resolved by version numbers
        if (version == "release" || version == "snapshot") version = MinecraftLoader.Vanila.ResolveVersion(version);

        switch (loader)
        {
            case MinecraftLoader.Fabric:
                {
                    const string fabricV2BaseUrl = "https://meta.fabricmc.net/v2/";
                    loaderVersion ??= (string)JsonNode.Parse(client.Send(new(HttpMethod.Get, $"{fabricV2BaseUrl}versions/loader/{version}")).Content.ReadAsStream())
                                                      .AsArray().FirstOrDefault(node => (node as JsonObject)["loader"] != null, JsonNode.Parse("""{"loader":{"version":""}}"""))
                                                      ["loader"]["version"];
                    return $"fabric-{version}-{loaderVersion}";
                }
            case MinecraftLoader.Forge:
                {
                    if (!version.Contains('-'))
                        version += "-recommended";
                    if (version.EndsWith("-latest") || version.EndsWith("-recommended"))
                    {

                    }
                    return "fotge-" + version;
                }
            case MinecraftLoader.Quilt:
                {
                    const string quiltV3BaseUrl = "https://meta.quiltmc.org/v3/";
                    loaderVersion ??= (string)JsonNode.Parse(client.Send(new(HttpMethod.Get, $"{quiltV3BaseUrl}versions/loader/{version}")).Content.ReadAsStream())
                                                      .AsArray().FirstOrDefault(node => (node as JsonObject)["loader"] != null, JsonNode.Parse("""{"loader":{"version":""}}"""))
                                                      ["loader"]["version"];
                    return $"quilt-{version}-{loaderVersion}";
                }
            case MinecraftLoader.NeoForge:
                {
                    
                    break;
                }
            /*case MinecraftLoader.LiteLoader:
                {

                    break;
                }*/ //TODO: Implement LiteLoader?
            default:
                throw new ArgumentOutOfRangeException(nameof(loader));
        }
    }
}