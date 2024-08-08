﻿using System;

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
        string platformString = "";

        switch (Environment.OSVersion.Platform)
        {
            case PlatformID.Win32NT:
                platformString = Environment.Is64BitProcess ? "windows-x64" : "windows-x86";
                break;
            case PlatformID.Unix:
                platformString = Environment.Is64BitProcess ? "linux" : "linux-i386";
                break;
            case PlatformID.MacOSX:
                platformString = Environment.Is64BitProcess ? "mac-os-arm64" : "mac-os";
                break;
            default:
                platformString = "gamecore";
                break;
        }

        return platformString;
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