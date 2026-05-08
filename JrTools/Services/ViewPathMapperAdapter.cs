using System.Collections.Generic;

namespace JrTools.Services
{
    /// <summary>
    /// Adapter que implementa <see cref="IViewPathMapper"/> delegando para <see cref="ViewPathMapperService"/>.
    /// Permite injeção de dependência e substituição por fake em testes.
    /// </summary>
    public class ViewPathMapperAdapter : IViewPathMapper
    {
        private readonly ViewPathMapperService _service;

        public ViewPathMapperAdapter() : this(new ViewPathMapperService()) { }

        public ViewPathMapperAdapter(ViewPathMapperService service)
        {
            _service = service;
        }

        /// <inheritdoc/>
        public void EnsureInitialized(string diretorio)
            => _service.EnsureInitialized(diretorio);

        /// <inheritdoc/>
        public Dictionary<string, List<string>> MapearCaminhosEmLote(
            IReadOnlyCollection<string> visoes, string diretorio)
            => _service.MapearCaminhosEmLote(visoes, diretorio);
    }
}
