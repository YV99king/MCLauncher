using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MCLauncher;

public partial class Login
{
    private static readonly HttpClient _client;

    private DateTime accessTokenExpiry;
    private readonly string email;
    private readonly string password;
    private string token;

    static Login()
    {
        using HttpClientHandler handler = new();
        handler.AllowAutoRedirect = true;
        using HttpClient _client = new(handler);
        _client.BaseAddress = null;
    }
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
    /// minecraft bearer token
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
    /// generates minecraft bearer token
    /// </summary>
    /// <param name="email">the account's email</param>
    /// <param name="password">the accaunt's password</param>
    /// <param name="accessTokenExpiry">where to save the bearer token's expiry date</param>
    /// <returns>minecraft bearer token</returns>
    public static string GenerateAccessToken(string email, string password, out DateTime accessTokenExpiry)
    {
        (string sFTTag, string urlPost) = GetPPFTAndUrlPost();
        string msAccessToken = GetMSLoginInfo(email, password, sFTTag, urlPost)["access_token"];
        (string xboxLiveToken, ulong xboxLiveUserHash) = GetXboxLiveLogin(msAccessToken);
        string HSTSToken = GetHSTSToken(xboxLiveToken);
        var minecraftLoginInfo = GetMinecraftLoginInfo(HSTSToken, xboxLiveUserHash);
        accessTokenExpiry = DateTime.Now + TimeSpan.FromSeconds(minecraftLoginInfo.RootElement.GetProperty("expires_in").GetInt32());
        return minecraftLoginInfo.RootElement.GetProperty("access_token").GetString();
    }
    private static (string sFTTag, string urlPost) GetPPFTAndUrlPost()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://login.live.com/oauth20_authorize.srf?client_id=000000004C12AE6F&redirect_uri=https://login.live.com/oauth20_desktop.srf&scope=service::user.auth.xboxlive.com::MBI_SSL&display=touch&response_type=token&locale=en");
        var response = _client.Send(request).Content.ReadAsStringAsync().Result;

        string sFTTag = GetPPFTValueRegex().Match(response).Value.Replace("value=", "").Trim('"');
        string urlPost = getUrlPostRegex().Match(response).Value.Replace("urlPost:", "").Trim('\'');
        return (sFTTag, urlPost);
    }
    [GeneratedRegex("value=\"(.+?)\"")]
    private static partial Regex GetPPFTValueRegex();
    [GeneratedRegex("urlPost:'(.+?)'")]
    private static partial Regex getUrlPostRegex();
    private static Dictionary<string, string> GetMSLoginInfo(string email, string password, string sFTTag, string urlPost)
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
        var response = _client.Send(request);

        var msLoginInfo = new Dictionary<string, string>();
        foreach (var item in response.RequestMessage.RequestUri.OriginalString.Split('#')[1].Split('&'))
            msLoginInfo.Add(item.Split('=')[0], item.Split('=')[1]);
        msLoginInfo["access_token"] = Uri.UnescapeDataString(msLoginInfo["access_token"]);
        msLoginInfo["refresh_token"] = Uri.UnescapeDataString(msLoginInfo["refresh_token"]);

        if (!msLoginInfo.TryGetValue("access_token", out _))
            throw new ArgumentException("Username or password incorrect", nameof(password));

        return msLoginInfo;
    }
    private static (string xboxLiveToken, ulong xboxLiveUserHash) GetXboxLiveLogin(string msAccessToken)
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
        var response = _client.Send(request);

        if (!response.IsSuccessStatusCode)
            throw new Exception("xboxlive login failed");//TODO: implemnt error system
        var xboxLiveLoginJson = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
        string xboxLiveToken = xboxLiveLoginJson.RootElement.GetProperty("Token").GetString();
        ulong xboxLiveUserHash = Convert.ToUInt64(xboxLiveLoginJson.RootElement.GetProperty("DisplayClaims").GetProperty("xui")[0].GetProperty("uhs").GetString());
        return (xboxLiveToken, xboxLiveUserHash);
    }
    private static string GetHSTSToken(string xboxLiveToken)
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
        var response = _client.Send(request);

        var xboxLiveHSTSJson = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
        return xboxLiveHSTSJson.RootElement.GetProperty("Token").GetString();
    }
    private static JsonDocument GetMinecraftLoginInfo(string HSTSToken, ulong xboxLiveUserHash)
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
        var response = _client.Send(request);

        return JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
    }

    /// <summary>
    /// returns the account's profile information
    /// </summary>
    /// <returns>the account's profile information</returns>
    public JsonDocument GetProfileInfo()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
        request.Headers.Add("authorization", $"Bearer {AccessToken}");
        var response = _client.Send(request);
        return JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
    }

    /// <summary>
    /// returns a value whether the account has purchased minecraft
    /// </summary>
    /// <returns>whether the account has purchased minecraft</returns>
    public bool IsOwnMinecraft()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/entitlements/mcstore");
        request.Headers.Add("Authorization", $"Bearer {AccessToken}");
        var response = _client.Send(request);
        if (JsonDocument.Parse(response.Content.ReadAsStringAsync().Result).RootElement.GetProperty("items").GetArrayLength() == 0)
            return false;
        return true;
    }

    /// <summary>
    /// sets the acount's skin to the specified skin
    /// </summary>
    /// <param name="skinPath">path of the skin asset</param>
    /// <param name="type">the skin type</param>
    public void SetSkin(string skinPath, SkinType type = SkinType.classic)
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
        _client.Send(request);
    }
}

/// <summary>
/// specifies a skin type
/// </summary>
public enum SkinType
{
    /// <summary>
    /// generic skin (steve)
    /// </summary>
    classic = 0,
    /// <summary>
    /// slim skin (alex)
    /// </summary>
    slim
}
