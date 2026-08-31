using Xunit;

namespace JrTools.Tests.Helpers
{
    /// <summary>
    /// Agrupa os testes que sobrescrevem a variável de ambiente de processo LOCALAPPDATA
    /// (<see cref="JrTools.Tests.Services.BServerConfigHelperTests"/>,
    /// <see cref="JrTools.Tests.Services.AtualizacaoHelperTests"/>) numa única coleção.
    /// O xUnit executa testes de uma mesma coleção sempre em sequência (nunca em
    /// paralelo entre si), evitando que um teste restaure/leia LOCALAPPDATA enquanto
    /// outro ainda está com o valor sobrescrito.
    /// </summary>
    [CollectionDefinition("LocalAppData")]
    public class LocalAppDataTestCollection
    {
    }
}
