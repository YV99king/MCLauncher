using System.Collections.Generic;

namespace MCLauncher;

public class ProfileInfo
{
    public string id;
    public string name;
    public List<Skin> skins;
    public List<Cape> capes;
    
    public record Cape
    {
        public string id;
        public string state;
        public string url;
        public string alias;
    }
    public record Skin
    {
        public string id;
        public string state;
        public string url;
        public string variant;
        public string alias;
    }
}