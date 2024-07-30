using System;
using System.Net.Http;

namespace MCLauncher;

public static class Program
{
	public static void Main()
	{
        using HttpClientHandler handler = new();
        handler.AllowAutoRedirect = true;
        using HttpClient client = new(handler);

    Login:
        Console.Write("username: "); var username =  Console.ReadLine();
        Console.Write("password: "); var password = Console.ReadLine();
        var loginInfo = new Login(username, password);
        var profileInfoRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
        profileInfoRequest.Headers.Add("Authorization", $"Bearer {loginInfo.AccessToken}");
        var profileInfoResponse = client.Send(profileInfoRequest);
        Console.WriteLine(profileInfoResponse.Content.ReadAsStringAsync().Result);
        goto Login;
    }
}