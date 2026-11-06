namespace JrTools.Enums
{
    /// <summary>
    /// Modos de filtragem de artefatos na interface do Instalador.
    /// </summary>
    public enum FiltroStatusArtefato
    {
        Todos = 0,
        SomentePendentes = 1,
        ApenasNovos = 2,
        ApenasModificados = 3,
        ApenasInstalados = 4,
        ApenasExcluidos = 5
    }
}
