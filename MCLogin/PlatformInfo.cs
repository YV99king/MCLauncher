using System;

namespace MCLauncher;

/// <summary>
/// Provides information about the current platform, including the operating system and Java platform name.
/// </summary>
public static class PlatformInfo
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
    /// Returns the Java platform name based on the current operating system and process architecture.
    /// </summary>
    /// <returns>The Java platform name as a string.</returns>
    public static string GetJavaPlatformName()
    {
        string platformString = Environment.OSVersion.Platform switch
        {
            PlatformID.Win32NT => Environment.Is64BitProcess ? "windows-x64" : "windows-x86",
            PlatformID.Unix => Environment.Is64BitProcess ? "linux" : "linux-i386",
            PlatformID.MacOSX => Environment.Is64BitProcess ? "mac-os-arm64" : "mac-os",
            _ => "gamecore",
        };
        return platformString;
    }

    /// <summary>
    /// Returns the OS's classpath seperator (';' or ':').
    /// </summary>
    /// <returns>the OS's classpath seperator (';' or ':').</returns>
    public static char GetClasspathSeparator()
    {
        if (OperatingSystem == OS.Windows)
            return ';';
        else
            return ':';
    }

    /// <summary>
    /// Returns the OS's path seperator ('\' or '/').
    /// </summary>
    /// <returns>the OS's path seperator ('\' or '/').</returns>
    public static char GetPathSeparator()
    {
        if (OperatingSystem == OS.Windows)
            return '\\';
        else
            return '/';
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