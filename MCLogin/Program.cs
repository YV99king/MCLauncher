﻿using System;
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

        //Console.Write("username: "); var username = Console.ReadLine();
        //Console.Write("password: "); var password = Console.ReadLine();
        //var loginInfo = new Login(username, password);

        Console.WriteLine(MinecraftLauncher.VersionJson.DeserializeJson(new FileStream(@"..\..\..\..\.minecraft\versions\1.21\1.21.json", FileMode.Open)));
    }
}