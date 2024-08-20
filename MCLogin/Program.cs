using System;
using System.IO;
using System.Net.Http;

namespace MCLauncher;

public static class Program
{
	public static void Main()
	{
        using HttpClientHandler handler = new();
        handler.AllowAutoRedirect = true;
        using HttpClient client = new(handler);

        Console.Write("username: "); var username = Console.ReadLine();
        Console.Write("password: "); var password = Console.ReadLine();
        var loginInfo = new Login(username, password);

        var minecraftV1_21 = new MinecraftLauncher("1.21", loginInfo, new(@"..\..\..\..\.minecraft"));
        minecraftV1_21.LaunchMinecraft(new());
    }
}