namespace JrTools.Dto
{
    public class MsBuildInfo
    {
        public string Version { get; set; }
        public string Path { get; set; }
        public string DisplayName => Version;
    }
}
