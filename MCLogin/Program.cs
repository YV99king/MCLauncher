using System;
using System.IO;
using System.Linq;
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

        Console.Write("version: "); var version = Console.ReadLine();
        var minecraftV1_21 = new MinecraftLauncher(version, loginInfo, new(@"..\..\..\..\.minecraft"), MinecraftLoader.Vanila);
        minecraftV1_21.InstallMinecraft().Wait();
        var mcProc = minecraftV1_21.LaunchMinecraft(new());

        Console.WriteLine("aftermath:"); // from here on it's just information checks (windows only)

        var originalCP = "C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\com\\github\\oshi\\oshi-core\\6.4.10\\oshi-core-6.4.10.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\com\\google\\code\\gson\\gson\\2.10.1\\gson-2.10.1.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\com\\google\\guava\\failureaccess\\1.0.1\\failureaccess-1.0.1.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\com\\google\\guava\\guava\\32.1.2-jre\\guava-32.1.2-jre.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\com\\ibm\\icu\\icu4j\\73.2\\icu4j-73.2.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\com\\mojang\\authlib\\6.0.54\\authlib-6.0.54.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\com\\mojang\\blocklist\\1.0.10\\blocklist-1.0.10.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\com\\mojang\\brigadier\\1.2.9\\brigadier-1.2.9.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\com\\mojang\\datafixerupper\\8.0.16\\datafixerupper-8.0.16.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\com\\mojang\\logging\\1.2.7\\logging-1.2.7.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\com\\mojang\\patchy\\2.2.10\\patchy-2.2.10.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\com\\mojang\\text2speech\\1.17.9\\text2speech-1.17.9.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\commons-codec\\commons-codec\\1.16.0\\commons-codec-1.16.0.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\commons-io\\commons-io\\2.15.1\\commons-io-2.15.1.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\commons-logging\\commons-logging\\1.2\\commons-logging-1.2.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\io\\netty\\netty-buffer\\4.1.97.Final\\netty-buffer-4.1.97.Final.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\io\\netty\\netty-codec\\4.1.97.Final\\netty-codec-4.1.97.Final.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\io\\netty\\netty-common\\4.1.97.Final\\netty-common-4.1.97.Final.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\io\\netty\\netty-handler\\4.1.97.Final\\netty-handler-4.1.97.Final.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\io\\netty\\netty-resolver\\4.1.97.Final\\netty-resolver-4.1.97.Final.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\io\\netty\\netty-transport-classes-epoll\\4.1.97.Final\\netty-transport-classes-epoll-4.1.97.Final.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\io\\netty\\netty-transport-native-unix-common\\4.1.97.Final\\netty-transport-native-unix-common-4.1.97.Final.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\io\\netty\\netty-transport\\4.1.97.Final\\netty-transport-4.1.97.Final.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\it\\unimi\\dsi\\fastutil\\8.5.12\\fastutil-8.5.12.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\net\\java\\dev\\jna\\jna-platform\\5.14.0\\jna-platform-5.14.0.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\net\\java\\dev\\jna\\jna\\5.14.0\\jna-5.14.0.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\net\\sf\\jopt-simple\\jopt-simple\\5.0.4\\jopt-simple-5.0.4.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\apache\\commons\\commons-compress\\1.26.0\\commons-compress-1.26.0.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\apache\\commons\\commons-lang3\\3.14.0\\commons-lang3-3.14.0.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\apache\\httpcomponents\\httpclient\\4.5.13\\httpclient-4.5.13.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\apache\\httpcomponents\\httpcore\\4.4.16\\httpcore-4.4.16.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\apache\\logging\\log4j\\log4j-api\\2.22.1\\log4j-api-2.22.1.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\apache\\logging\\log4j\\log4j-core\\2.22.1\\log4j-core-2.22.1.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\apache\\logging\\log4j\\log4j-slf4j2-impl\\2.22.1\\log4j-slf4j2-impl-2.22.1.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\jcraft\\jorbis\\0.0.17\\jorbis-0.0.17.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\joml\\joml\\1.10.5\\joml-1.10.5.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-freetype\\3.3.3\\lwjgl-freetype-3.3.3.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-freetype\\3.3.3\\lwjgl-freetype-3.3.3-natives-windows.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-freetype\\3.3.3\\lwjgl-freetype-3.3.3-natives-windows-arm64.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-freetype\\3.3.3\\lwjgl-freetype-3.3.3-natives-windows-x86.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-glfw\\3.3.3\\lwjgl-glfw-3.3.3.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-glfw\\3.3.3\\lwjgl-glfw-3.3.3-natives-windows.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-glfw\\3.3.3\\lwjgl-glfw-3.3.3-natives-windows-arm64.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-glfw\\3.3.3\\lwjgl-glfw-3.3.3-natives-windows-x86.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-jemalloc\\3.3.3\\lwjgl-jemalloc-3.3.3.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-jemalloc\\3.3.3\\lwjgl-jemalloc-3.3.3-natives-windows.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-jemalloc\\3.3.3\\lwjgl-jemalloc-3.3.3-natives-windows-arm64.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-jemalloc\\3.3.3\\lwjgl-jemalloc-3.3.3-natives-windows-x86.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-openal\\3.3.3\\lwjgl-openal-3.3.3.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-openal\\3.3.3\\lwjgl-openal-3.3.3-natives-windows.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-openal\\3.3.3\\lwjgl-openal-3.3.3-natives-windows-arm64.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-openal\\3.3.3\\lwjgl-openal-3.3.3-natives-windows-x86.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-opengl\\3.3.3\\lwjgl-opengl-3.3.3.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-opengl\\3.3.3\\lwjgl-opengl-3.3.3-natives-windows.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-opengl\\3.3.3\\lwjgl-opengl-3.3.3-natives-windows-arm64.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-opengl\\3.3.3\\lwjgl-opengl-3.3.3-natives-windows-x86.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-stb\\3.3.3\\lwjgl-stb-3.3.3.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-stb\\3.3.3\\lwjgl-stb-3.3.3-natives-windows.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-stb\\3.3.3\\lwjgl-stb-3.3.3-natives-windows-arm64.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-stb\\3.3.3\\lwjgl-stb-3.3.3-natives-windows-x86.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-tinyfd\\3.3.3\\lwjgl-tinyfd-3.3.3.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-tinyfd\\3.3.3\\lwjgl-tinyfd-3.3.3-natives-windows.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-tinyfd\\3.3.3\\lwjgl-tinyfd-3.3.3-natives-windows-arm64.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl-tinyfd\\3.3.3\\lwjgl-tinyfd-3.3.3-natives-windows-x86.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl\\3.3.3\\lwjgl-3.3.3.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl\\3.3.3\\lwjgl-3.3.3-natives-windows.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl\\3.3.3\\lwjgl-3.3.3-natives-windows-arm64.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lwjgl\\lwjgl\\3.3.3\\lwjgl-3.3.3-natives-windows-x86.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\lz4\\lz4-java\\1.8.0\\lz4-java-1.8.0.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\libraries\\org\\slf4j\\slf4j-api\\2.0.9\\slf4j-api-2.0.9.jar;C:\\Users\\user\\source\\repos\\MCLogin\\.minecraft\\versions\\1.21\\1.21.jar";
        var splitOriginal = originalCP.Split(';').Select(s => Path.GetRelativePath(@"C:\Users\user\source\repos\MCLogin\.minecraft", s));
        var outCP = mcProc.StartInfo.Arguments.Split(' ').First(s => s.Contains(';'));
        var splitout = outCP.Split(';').Select(s => string.IsNullOrEmpty(s) ? "" : Path.GetRelativePath(@"..\..\..\..\.minecraft", s));
        bool hasDoubleSeperator = false;

        Console.WriteLine("only in out:");
        foreach (var item in splitout)
        {
            if (string.IsNullOrEmpty(item))
                hasDoubleSeperator = true;
            else if (!splitOriginal.Contains(item))
                Console.WriteLine(item);
        }

        Console.WriteLine("only in original:");
        foreach (var item in splitOriginal)
        {
            if (!splitout.Contains(item))
                Console.WriteLine(item);
        }

        Console.WriteLine($"has double seperator: {hasDoubleSeperator}");

        Console.Write("clean up (y/n): "); bool? clean = Console.ReadLine() switch
        {
            "y" => true,
            "Y" => true,
            "n" => false,
            "N" => false,
            _ => null
        };

        if (clean == true)
            Directory.Delete(@"C:\Users\user\source\repos\MCLogin\.minecraft", true);
        else if (clean == false)
            Console.WriteLine("Cleanup aborted.");
        else
            Console.WriteLine("invalid input: " + clean);
    }
}