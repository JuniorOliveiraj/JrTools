using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using JrTools.Services;
using Xunit;

namespace JrTools.Tests.Services
{
    /// <summary>
    /// Testes de propriedade para <see cref="ViewPathMapperAdapter"/>.
    /// </summary>
    [Properties(MaxTest = 100)]
    public class ViewPathMapperAdapterTests
    {
        // -----------------------------------------------------------------------
        // Gerador auxiliar: produz strings de diretório não vazias
        // -----------------------------------------------------------------------
        private static Gen<string> GenDiretorioNaoVazio() =>
            Arb.Generate<NonEmptyString>()
                .Select(nes => nes.Get)
                .Where(s => !string.IsNullOrWhiteSpace(s));

        // -----------------------------------------------------------------------
        // Property 7: EnsureInitialized é idempotente para o mesmo diretório
        // **Valida: Requisito 3.3**
        // -----------------------------------------------------------------------

        // Feature: view-path-explorer, Property 7: EnsureInitialized é idempotente para o mesmo diretório
        [Property]
        public Property Property7_EnsureInitializedEhIdempotente()
        {
            return Prop.ForAll(
                GenDiretorioNaoVazio().ToArbitrary(),
                diretorio =>
                {
                    // Instanciar ViewPathMapperAdapter com ViewPathMapperService real
                    var service = new ViewPathMapperService();
                    var adapter = new ViewPathMapperAdapter(service);

                    // Capturar estado interno antes da primeira chamada
                    var initializedFieldBefore = GetPrivateField<bool>(service, "_initialized");
                    var diretorioIndexadoBefore = GetPrivateField<string?>(service, "_diretorioIndexado");

                    // Primeira chamada a EnsureInitialized
                    adapter.EnsureInitialized(diretorio);

                    // Capturar estado interno após a primeira chamada
                    var initializedFieldAfterFirst = GetPrivateField<bool>(service, "_initialized");
                    var diretorioIndexadoAfterFirst = GetPrivateField<string?>(service, "_diretorioIndexado");
                    var pagesCacheAfterFirst = GetPrivateField<object>(service, "_pagesCache");
                    var pagesByEntityViewAfterFirst = GetPrivateField<object>(service, "_pagesByEntityView");
                    var pagesByFormUrlAfterFirst = GetPrivateField<object>(service, "_pagesByFormUrl");

                    // Segunda chamada a EnsureInitialized com o mesmo diretório
                    adapter.EnsureInitialized(diretorio);

                    // Capturar estado interno após a segunda chamada
                    var initializedFieldAfterSecond = GetPrivateField<bool>(service, "_initialized");
                    var diretorioIndexadoAfterSecond = GetPrivateField<string?>(service, "_diretorioIndexado");
                    var pagesCacheAfterSecond = GetPrivateField<object>(service, "_pagesCache");
                    var pagesByEntityViewAfterSecond = GetPrivateField<object>(service, "_pagesByEntityView");
                    var pagesByFormUrlAfterSecond = GetPrivateField<object>(service, "_pagesByFormUrl");

                    // Verificar que o estado não mudou na segunda chamada
                    var stateUnchanged =
                        initializedFieldAfterFirst == initializedFieldAfterSecond &&
                        diretorioIndexadoAfterFirst == diretorioIndexadoAfterSecond &&
                        ReferenceEquals(pagesCacheAfterFirst, pagesCacheAfterSecond) &&
                        ReferenceEquals(pagesByEntityViewAfterFirst, pagesByEntityViewAfterSecond) &&
                        ReferenceEquals(pagesByFormUrlAfterFirst, pagesByFormUrlAfterSecond);

                    // Verificar que _initialized foi setado para true após a primeira chamada
                    var wasInitialized = initializedFieldAfterFirst == true;

                    // Verificar que _diretorioIndexado foi setado para o diretório correto
                    var correctDirectory = diretorioIndexadoAfterFirst == diretorio;

                    return stateUnchanged && wasInitialized && correctDirectory;
                });
        }

        /// <summary>
        /// Helper para acessar campos privados via reflexão.
        /// </summary>
        private static T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new InvalidOperationException($"Campo privado '{fieldName}' não encontrado.");

            var value = field.GetValue(obj);
            return value == null ? default! : (T)value;
        }
    }
}
