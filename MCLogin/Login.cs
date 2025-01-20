using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MCLauncher;

/// <summary>
/// Provides access to Mojang's authentication services
/// </summary>
public partial class Login
{
    private static readonly JsonSerializerOptions s_serializerOptionsIncludeFields = new() { IncludeFields = true };

    private DateTime accessTokenExpiry;
    private readonly string email;
    private readonly string password;
    private string token;

    /// <summary>
    /// Initializes a new instance of the <see cref="Login"/> class
    /// </summary>
    /// <param name="email">the email of the account</param>
    /// <param name="password">the password of the account</param>
    public Login(string email, string password)
    {
        this.email = email;
        this.password = password;
        token = GenerateAccessToken(email, password, out accessTokenExpiry);
    }

    /// <summary>
    /// Minecraft bearer token
    /// </summary>
    public string AccessToken
    {
        get
        {
            if (accessTokenExpiry < DateTime.Now)
                token = GenerateAccessToken(email, password, out accessTokenExpiry);
            return token;
        }
    }
    /// <summary>
    /// the account's email
    /// </summary>
    public string Email => email;
    /// <summary>
    /// the account's password
    /// </summary>
    public string Password => password;
    /// <summary>
    /// when the bearer token will expire
    /// </summary>
    public DateTime AccessTokenExpiry => accessTokenExpiry;

    /// <summary>
    /// generates Minecraft bearer token
    /// </summary>
    /// <param name="email">the account's email</param>
    /// <param name="password">the account's password</param>
    /// <param name="accessTokenExpiry">where to save the bearer token's expiry date</param>
    /// <returns>Minecraft bearer token</returns>
    public static string GenerateAccessToken(string email, string password, out DateTime accessTokenExpiry)
    {
        HttpClient client = new(new HttpClientHandler()
        {
            AllowAutoRedirect = true
        });

        (string sFTTag, string urlPost) = GetPPFTAndUrlPost(client);
        string msAccessToken = GetMSLoginInfo(client, email, password, sFTTag, urlPost)["access_token"];
        (string xboxLiveToken, ulong xboxLiveUserHash) = GetXboxLiveLogin(client, msAccessToken);
        string HSTSToken = GetHSTSToken(client, xboxLiveToken);
        var minecraftLoginInfo = GetMinecraftLoginInfo(client, HSTSToken, xboxLiveUserHash);
        accessTokenExpiry = DateTime.Now + TimeSpan.FromSeconds((int)minecraftLoginInfo["expires_in"]);
        return (string)minecraftLoginInfo["access_token"];
    }
    private static (string sFTTag, string urlPost) GetPPFTAndUrlPost(HttpClient client)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://login.live.com/oauth20_authorize.srf?client_id=000000004C12AE6F&redirect_uri=https://login.live.com/oauth20_desktop.srf&scope=service::user.auth.xboxlive.com::MBI_SSL&display=touch&response_type=token&locale=en");
        var response1 = client.Send(request);
        var response = response1.Content.ReadAsStringAsync().Result;

        string sFTTag = GetPPFTValueRegex().Match(response.Split("sFTTag:")[1]).Value.Replace("value=", "").Trim('"');
        string urlPost = getUrlPostRegex().Match(response).Value.Replace("urlPost:", "").Trim('\'');
        return (sFTTag, urlPost);
    }
    [GeneratedRegex("value=\"(.+?)\"")]
    private static partial Regex GetPPFTValueRegex();
    [GeneratedRegex("urlPost:'(.+?)'")]
    private static partial Regex getUrlPostRegex();
    private static Dictionary<string, string> GetMSLoginInfo(HttpClient client, string email, string password, string sFTTag, string urlPost)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, urlPost)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>()
            {
                { "login", email },
                { "loginfmt", email },
                { "passwd", password },
                { "PPFT", sFTTag }
            })
        };
        var response = client.Send(request);

        var msLoginInfo = new Dictionary<string, string>();
        foreach (var item in response.RequestMessage.RequestUri.OriginalString.Split('#')[1].Split('&'))
            msLoginInfo.Add(item.Split('=')[0], item.Split('=')[1]);
        msLoginInfo["access_token"] = Uri.UnescapeDataString(msLoginInfo["access_token"]);
        msLoginInfo["refresh_token"] = Uri.UnescapeDataString(msLoginInfo["refresh_token"]);

        if (!msLoginInfo.TryGetValue("access_token", out _))
            throw new ArgumentException("Username or password incorrect", nameof(password));

        return msLoginInfo;
    }
    private static (string xboxLiveToken, ulong xboxLiveUserHash) GetXboxLiveLogin(HttpClient client, string msAccessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://user.auth.xboxlive.com/user/authenticate");
        request.Headers.Add("Accept", "application/json");
        request.Content = new StringContent("""
        {
            "Properties": {
                "AuthMethod": "RPS",
                "SiteName": "user.auth.xboxlive.com",
                "RpsTicket": "ACCESS_TOKEN_HERE"
            },
            "RelyingParty": "http://auth.xboxlive.com",
            "TokenType": "JWT"
        }
        """.Replace("ACCESS_TOKEN_HERE", msAccessToken), null, "application/json");
        var response = client.Send(request);

        if (!response.IsSuccessStatusCode)
            throw new XboxLiveException(0, "Xbox live login failed");
        var xboxLiveLoginJson = JsonNode.Parse(response.Content.ReadAsStringAsync().Result);
        string xboxLiveToken = (string)xboxLiveLoginJson["Token"];
        ulong xboxLiveUserHash = Convert.ToUInt64((string)xboxLiveLoginJson["DisplayClaims"]["xui"][0]["uhs"]);
        return (xboxLiveToken, xboxLiveUserHash);
    }
    private static string GetHSTSToken(HttpClient client, string xboxLiveToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://xsts.auth.xboxlive.com/xsts/authorize");
        request.Headers.Add("Accept", "application/json");
        request.Content = new StringContent("""
            {
                "Properties": {
                    "SandboxId": "RETAIL",
                    "UserTokens": [
                        "TOKEN_HERE_FROM_PREVIOUS_STEP"
                    ]
                },
                "RelyingParty": "rp://api.minecraftservices.com/",
                "TokenType": "JWT"
            }
            """.Replace("TOKEN_HERE_FROM_PREVIOUS_STEP", xboxLiveToken), null, "application/json");
        var response = client.Send(request);

        var xboxLiveHSTSJson = JsonNode.Parse(response.Content.ReadAsStringAsync().Result);
        return (string)xboxLiveHSTSJson["Token"];
    }
    private static JsonNode GetMinecraftLoginInfo(HttpClient client, string HSTSToken, ulong xboxLiveUserHash)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.minecraftservices.com/authentication/login_with_xbox")
        {
            Content = new StringContent("""
                        {
               "identityToken" : "XBL3.0 x=USER_HASH_HERE;XSTS_TOKEN_HERE",
               "ensureLegacyEnabled" : true
            }
            """.Replace("USER_HASH_HERE", xboxLiveUserHash.ToString()).Replace("XSTS_TOKEN_HERE", HSTSToken), null, "application/json")
        };
        var response = client.Send(request);

        return JsonNode.Parse(response.Content.ReadAsStringAsync().Result);
    }

    /// <summary>
    /// returns the account's profile information
    /// </summary>
    /// <returns>the account's profile information</returns>
    public ProfileInfo.ProfileInfoRoot GetProfileInfo(HttpClient client)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
        request.Headers.Add("authorization", $"Bearer {AccessToken}");
        var response = client.Send(request);
        return JsonSerializer.Deserialize<ProfileInfo.ProfileInfoRoot>(response.Content.ReadAsStream(), s_serializerOptionsIncludeFields);
    }

    /// <summary>
    /// returns a value whether the account has purchased Minecraft
    /// </summary>
    /// <returns>whether the account has purchased Minecraft</returns>
    public bool IsOwnMinecraft(HttpClient client)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/entitlements/mcstore");
        request.Headers.Add("Authorization", $"Bearer {AccessToken}");
        var response = client.Send(request);
        return JsonNode.Parse(response.Content.ReadAsStringAsync().Result)["items"] is JsonArray { Count: > 0 };
    }

    /// <summary>
    /// sets the account's skin to the specified skin
    /// </summary>
    /// <param name="skinPath">path of the skin asset</param>
    /// <param name="type">the skin type</param>
    public void SetSkin(HttpClient client, string skinPath, SkinType type = SkinType.classic)
    {
        HttpRequestMessage request;
        if (skinPath == null)
        {
            request = new HttpRequestMessage(HttpMethod.Delete, "https://api.minecraftservices.com/minecraft/profile/skins/active");
            request.Headers.Add("authorization", $"Bearer {AccessToken}");
        }
        else
        {
            request = new HttpRequestMessage(HttpMethod.Post, "https://api.minecraftservices.com/minecraft/profile/skins");
            request.Headers.Add("authorization", $"Bearer {AccessToken}");
            request.Content = new MultipartFormDataContent
            {
                { new StringContent(type == SkinType.classic ? "classic" : "slim"), "variant" },
                { new StreamContent(File.OpenRead(skinPath)), "file", skinPath }
            };
        }
        client.Send(request);
    }

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1822 // Mark members as static
#pragma warning disable IDE0051 // Remove unused private members
    private void SetCape() { }//TODO: implement SetCape
#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore CA1822 // Mark members as static
#pragma warning restore IDE0079 // Remove unnecessary suppression

    public class ProfileInfo
    {
        public record Cape
        {
            public string id;
            public string state;
            public string url;
            public string alias;
        }
        public record ProfileInfoRoot
        {
            public string id;
            public string name;
            public List<Skin> skins;
            public List<Cape> capes;
        }
        public record Skin
        {
            public string id;
            public string state;
            public string url;
            public string variant;
            public string alias;
        }
    }
}