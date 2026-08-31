namespace JrTools.Dto
{
    public class AtualizacaoDisponivelDto
    {
        public string Tag { get; set; } = string.Empty;
        public int RunNumber { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
    }
}
