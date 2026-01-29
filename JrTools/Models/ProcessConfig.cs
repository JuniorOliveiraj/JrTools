namespace JrTools.Models
{
    /// <summary>
    /// Configuração de um processo a ser monitorado
    /// </summary>
    public class ProcessConfig
    {
        public string Name { get; set; }
        public bool EnabledByDefault { get; set; }

        public ProcessConfig(string name, bool enabledByDefault)
        {
            Name = name;
            EnabledByDefault = enabledByDefault;
        }
    }
}
