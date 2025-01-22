using System;
using System.Net.Http;

namespace MCLauncher;

public interface ILoginProvider
{
    static ILoginProvider Empty => new EmptyLogin();

    /// <summary>
    /// Minecraft's access token
    /// </summary>
    string AccessToken { get; }
    /// <summary>
    /// the account's Profile Information
    /// </summary>
    public ProfileInfo Profile { get; }

    ProfileInfo UpdateProfileInfo(HttpClient client);
    bool IsOwnMinecraft(HttpClient client);
    void SetSkin(HttpClient client, string skinPath, SkinType type = SkinType.classic);
    void SetCape(HttpClient client);

    private struct EmptyLogin : ILoginProvider
    {
        public readonly string AccessToken => "";
        public readonly ProfileInfo Profile => default;
        public readonly ProfileInfo UpdateProfileInfo(HttpClient client) => default;
        public readonly bool IsOwnMinecraft(HttpClient client) => false;
        public readonly void SetSkin(HttpClient client, string skinPath, SkinType type = SkinType.classic) { }
        public readonly void SetCape(HttpClient client) { }
    }
}