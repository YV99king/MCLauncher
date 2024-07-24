using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MCLogin;

public partial class Login
{
    private DateTime accessTokenExpiry;
    private readonly string email;
    private readonly string password;
    private string token;

    public Login(string email, string password)
    {
        this.email = email;
        this.password = password;
        token = GenerateAccessToken(email, password, out accessTokenExpiry);
    }

    public string AccessToken
    {
        get
        {
            if (accessTokenExpiry < DateTime.Now)
                token = GenerateAccessToken(email, password, out accessTokenExpiry);
            return token;
        }
    }
    public string Email => email;
    public string Password => password;
    public DateTime AccessTokenExpiry => accessTokenExpiry;

    public static string GenerateAccessToken(string email, string password, out DateTime accessTokenExpiry)
    {
        using HttpClientHandler handler = new();
        handler.AllowAutoRedirect = true;
        using HttpClient client = new(handler);
        client.BaseAddress = null;

        (string sFTTag, string urlPost) = GetPPFTAndUrlPost(client);
        string msAccessToken = GetMSLoginInfo(client, email, password, sFTTag, urlPost)["access_token"];
        (string xboxLiveToken, long xboxLiveUserHash) = GetXboxLiveLogin(client, msAccessToken);
        string HSTSToken = GetHSTSToken(client, xboxLiveToken);
        var minecraftLoginInfo = GetMinecraftLoginInfo(client, HSTSToken, xboxLiveUserHash);
        accessTokenExpiry = DateTime.Now + TimeSpan.FromSeconds(minecraftLoginInfo.RootElement.GetProperty("expires_in").GetInt32());
        return minecraftLoginInfo.RootElement.GetProperty("access_token").GetString();
    }
    private static (string sFTTag, string urlPost) GetPPFTAndUrlPost(HttpClient client)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://login.live.com/oauth20_authorize.srf?client_id=000000004C12AE6F&redirect_uri=https://login.live.com/oauth20_desktop.srf&scope=service::user.auth.xboxlive.com::MBI_SSL&display=touch&response_type=token&locale=en");
        var response = client.Send(request).Content.ReadAsStringAsync().Result;

        string sFTTag = GetPPFTValueRegex().Match(response).Value.Replace("value=", "").Trim('"');
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
    private static (string xboxLiveToken, long xboxLiveUserHash) GetXboxLiveLogin(HttpClient client, string msAccessToken)
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
            throw new Exception("xboxlive login failed");//TODO: implemnt error system
        var xboxLiveLoginJson = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
        string xboxLiveToken = xboxLiveLoginJson.RootElement.GetProperty("Token").GetString();
        long xboxLiveUserHash = Convert.ToInt64(xboxLiveLoginJson.RootElement.GetProperty("DisplayClaims").GetProperty("xui")[0].GetProperty("uhs").GetString());
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

        var xboxLiveHSTSJson = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
        return xboxLiveHSTSJson.RootElement.GetProperty("Token").GetString();
    }
    private static JsonDocument GetMinecraftLoginInfo(HttpClient client, string HSTSToken, long xboxLiveUserHash)
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

        return JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
    }
}
