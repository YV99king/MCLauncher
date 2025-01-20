using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MCLauncher;

/// <summary>
/// Provides information about the current platform, including the operating system and Java platform name.
/// </summary>
internal static class PlatformInfo
{
    static PlatformInfo()
    {
        OperatingSystem = Environment.OSVersion.Platform switch
        {
            PlatformID.Win32NT => OS.Windows,
            PlatformID.Unix => OS.MacOS,
            PlatformID.MacOSX => OS.Linux,
            _ => OS.unsupported,
        };
    }

    /// <summary>
    /// Indicates whether the current operating system is 64-bit.
    /// </summary>
    public static bool Is64Bit { get; } = Environment.Is64BitOperatingSystem;

    /// <summary>
    /// The current operating system.
    /// </summary>
    public static OS OperatingSystem { get; }

    /// <summary>
    /// Returns the OS's classpath separator (':' or ':').
    /// </summary>
    public static char ClasspathSeparator => OperatingSystem == OS.Windows ? ';' : ':';

    /// <summary>
    /// Returns the Java platform name based on the current operating system and process architecture
    /// </summary>
    public static string JavaPlatformName { get; } = Environment.OSVersion.Platform switch
    {
        PlatformID.Win32NT => Environment.Is64BitProcess ? "windows-x64" : "windows-x86",
        PlatformID.Unix => Environment.Is64BitProcess ? "linux" : "linux-i386",
        PlatformID.MacOSX => Environment.Is64BitProcess ? "mac-os-arm64" : "mac-os",
        _ => "gamecore",
    };

    /// <summary>
    /// Returns the OS's path seperator ('\' or '/').
    /// </summary>
    public static char PathSeparator => OperatingSystem == OS.Windows ? '\\' : '/';

    public static Process StartProcess(string command, bool redirectStandardStream = true)
    {
        int argsIndex = command.IndexOf(' ');

        Process process = new() { StartInfo = new(fileName: command[..argsIndex], arguments: command[(argsIndex + 1)..]) };

        if (redirectStandardStream)
        {
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
        }

        return process.Start() ? process : null;
    }
    public static Process StartProcess(string exec, bool redirectStandardStream = true, params IEnumerable<string> args)
    {
        Process process = new() { StartInfo = new(exec, args) };

        if (redirectStandardStream)
        {
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
        }

        return process.Start() ? process : null;
    }

    /// <summary>
    /// The supported operating systems.
    /// </summary>
    public enum OS
    {
        /// <summary>
        /// Unknown or unsupported operating system.
        /// </summary>
        unsupported = 0,
        /// <summary>
        /// Windows operating system.
        /// </summary>
        Windows = 1,
        /// <summary>
        /// MacOS operating system.
        /// </summary>
        MacOS = 2,
        /// <summary>
        /// Linux operating system.
        /// </summary>
        Linux = 3
    }
}