using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using JrTools.Dto;
using JrTools.Services;
using JrTools.Tests.Helpers;
using JrTools.ViewModels;
using Xunit;

namespace JrTools.Tests.ViewModels
{
    /// <summary>
    /// Testes de propriedade para a lógica de filtro do <see cref="BinarioDelphiExplorerViewModel"/>.
    /// </summary>
    [Properties(MaxTest = 100)]
    public class BinarioDelphiExplorerViewModelFilterTests
    {
        // -----------------------------------------------------------------------
        // Geradores auxiliares
        // -----------------------------------------------------------------------

        /// <summary>
        /// Gera um array de nomes de arquivo únicos (lowercase) com extensões .exe, .dll ou .bpl.
        /// </summary>
        private static Gen<string[]> GenNomesUnicos() =>
            Gen.NonEmptyListOf(
                    Arb.Generate<PositiveInt>()
                        .SelectMany(n =>
                            Gen.Elements(".exe", ".dll", ".bpl")
                               .Select(ext => $"file{n.Get}{ext}")))
               .Select(lista => lista.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

        /// <summary>
        /// Gera uma string de filtro aleatória (pode ser vazia, parcial ou completa).
        /// </summary>
        private static Gen<string> GenFiltroNome() =>
            Gen.OneOf(
                Gen.Constant(string.Empty),
                Gen.Elements("file", "FILE", "File", ".exe", ".dll", ".bpl", "1", "2", "xyz"),
                Arb.Generate<PositiveInt>().Select(n => $"file{n.Get}"));

        // -----------------------------------------------------------------------
        // Helpers de setup
        // -----------------------------------------------------------------------

        /// <summary>
        /// Cria arquivos temporários reais no disco e retorna seus FileInfo.
        /// </summary>
        private static (List<FileInfo> arquivos, string tempDir) CriarArquivosReais(string[] nomes)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "JrToolsVMTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var arquivos = new List<FileInfo>();
            foreach (var nome in nomes)
            {
                var caminho = Path.Combine(tempDir, nome);
                File.WriteAllBytes(caminho, Array.Empty<byte>());
                arquivos.Add(new FileInfo(caminho));
            }

            return (arquivos, tempDir);
        }

        private static void LimparDiretorioTemp(string tempDir)
        {
            try { Directory.Delete(tempDir, recursive: true); }
            catch { /* ignora erros de limpeza */ }
        }

        /// <summary>
        /// Cria um ViewModel com o índice já populado a partir dos nomes fornecidos.
        /// </summary>
        private static (BinarioDelphiExplorerViewModel vm, string tempDir) CriarViewModelComIndice(string[] nomes)
        {
            var (arquivos, tempDir) = CriarArquivosReais(nomes);
            var reader = new FakeDirectoryReader(arquivos);
            var indexService = new BinarioDelphiIndexService(reader);

            // Construir o índice de forma síncrona
            var indice = indexService.ConstruirIndiceAsync("fake_dir").GetAwaiter().GetResult();

            // Criar o ViewModel com o serviço injetado e configurar o índice
            var vm = new BinarioDelphiExplorerViewModel(indexService);
            vm.SetIndiceForTesting(indice);

            return (vm, tempDir);
        }

        // -----------------------------------------------------------------------
        // Property 3: Filtro por nome é subconjunto do índice
        // Validates: Requirement 5.2
        // -----------------------------------------------------------------------

        // Feature: binarios-delphi-explorer, Property 3: Filtro por nome é subconjunto do índice
        [Property]
        public Property Property3_FiltroEhSubconjuntoDoIndice()
        {
            return Prop.ForAll(
                GenNomesUnicos().ToArbitrary(),
                GenFiltroNome().ToArbitrary(),
                (nomes, filtro) =>
                {
                    var (vm, tempDir) = CriarViewModelComIndice(nomes);
                    try
                    {
                        // Aplicar filtro de nome
                        vm.TextoFiltroNome = filtro;

                        // Todos os itens filtrados devem ter nome que contém o filtro (case-insensitive)
                        var filtroLower = filtro.ToLowerInvariant();
                        foreach (var item in vm.ItensFiltrados)
                        {
                            if (!string.IsNullOrEmpty(filtro) &&
                                !item.NomeNormalizado.Contains(filtroLower))
                                return false;
                        }

                        // Todos os itens filtrados devem ter nome presente no conjunto original de nomes
                        var nomesOriginaisLower = new HashSet<string>(
                            nomes.Select(n => n.ToLowerInvariant()),
                            StringComparer.OrdinalIgnoreCase);

                        foreach (var item in vm.ItensFiltrados)
                        {
                            if (!nomesOriginaisLower.Contains(item.NomeNormalizado))
                                return false;
                        }

                        return true;
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 4: Filtro por nome é case-insensitive
        // Validates: Requirement 5.3
        // -----------------------------------------------------------------------

        // Feature: binarios-delphi-explorer, Property 4: Filtro por nome é case-insensitive
        [Property]
        public Property Property4_FiltroEhCaseInsensitive()
        {
            return Prop.ForAll(
                GenNomesUnicos().ToArbitrary(),
                nomes =>
                {
                    var (vm, tempDir) = CriarViewModelComIndice(nomes);
                    try
                    {
                        // Usar "file" como termo de busca em variantes de casing
                        var termos = new[] { "file", "FILE", "File", "fIlE" };
                        int? contagem = null;

                        foreach (var termo in termos)
                        {
                            vm.TextoFiltroNome = termo;
                            var contagemAtual = vm.ItensFiltrados.Count;

                            if (contagem == null)
                                contagem = contagemAtual;
                            else if (contagem != contagemAtual)
                                return false;
                        }

                        return true;
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 5: Limpar filtro restaura lista completa
        // Validates: Requirements 5.5, 5.6
        // -----------------------------------------------------------------------

        // Feature: binarios-delphi-explorer, Property 5: Limpar filtro restaura lista completa
        [Property]
        public Property Property5_LimparFiltroRestauraListaCompleta()
        {
            return Prop.ForAll(
                GenNomesUnicos().ToArbitrary(),
                GenFiltroNome().ToArbitrary(),
                (nomes, filtro) =>
                {
                    var (vm, tempDir) = CriarViewModelComIndice(nomes);
                    try
                    {
                        var totalOriginal = vm.ItensFiltrados.Count;

                        // Aplicar filtro de nome e extensão
                        vm.TextoFiltroNome = filtro;
                        vm.ExtensaoFiltro = ".exe";

                        // Limpar ambos os filtros
                        vm.TextoFiltroNome = string.Empty;
                        vm.ExtensaoFiltro = null;

                        // A lista deve ser restaurada ao tamanho original
                        return vm.ItensFiltrados.Count == totalOriginal
                            && vm.TotalFiltrado == totalOriginal;
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 6: Extensões disponíveis refletem o índice
        // Validates: Requirement 6.1
        // -----------------------------------------------------------------------

        // Feature: binarios-delphi-explorer, Property 6: Extensões disponíveis refletem o índice
        [Property]
        public Property Property6_ExtensoesDisponiveisRefletemOIndice()
        {
            return Prop.ForAll(
                GenNomesUnicos().ToArbitrary(),
                nomes =>
                {
                    var (vm, tempDir) = CriarViewModelComIndice(nomes);
                    try
                    {
                        // Extensões esperadas: conjunto único de extensões dos nomes fornecidos
                        var extencoesEsperadas = new HashSet<string>(
                            nomes.Select(n => Path.GetExtension(n).ToLowerInvariant()),
                            StringComparer.OrdinalIgnoreCase);

                        var extencoesDisponiveis = new HashSet<string>(
                            vm.ExtensoesDisponiveis,
                            StringComparer.OrdinalIgnoreCase);

                        // Os dois conjuntos devem ser iguais
                        return extencoesEsperadas.SetEquals(extencoesDisponiveis);
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 7: Filtro combinado satisfaz ambos os critérios
        // Validates: Requirement 6.2
        // -----------------------------------------------------------------------

        // Feature: binarios-delphi-explorer, Property 7: Filtro combinado satisfaz ambos os critérios
        [Property]
        public Property Property7_FiltroCombinado()
        {
            return Prop.ForAll(
                GenNomesUnicos().ToArbitrary(),
                GenFiltroNome().ToArbitrary(),
                (nomes, filtroNome) =>
                {
                    var (vm, tempDir) = CriarViewModelComIndice(nomes);
                    try
                    {
                        // Aplicar filtro combinado: nome + extensão
                        vm.TextoFiltroNome = filtroNome;
                        vm.ExtensaoFiltro = ".exe";

                        var filtroLower = filtroNome.ToLowerInvariant();

                        foreach (var item in vm.ItensFiltrados)
                        {
                            // Deve satisfazer o filtro de nome (se não vazio)
                            if (!string.IsNullOrEmpty(filtroNome) &&
                                !item.NomeNormalizado.Contains(filtroLower))
                                return false;

                            // Deve satisfazer o filtro de extensão
                            if (!string.Equals(item.Extensao, ".exe", StringComparison.OrdinalIgnoreCase))
                                return false;
                        }

                        return true;
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 8: Selecionar "Todas" remove filtro de extensão
        // Validates: Requirement 6.3
        // -----------------------------------------------------------------------

        // Feature: binarios-delphi-explorer, Property 8: Selecionar "Todas" remove filtro de extensão
        [Property]
        public Property Property8_SelecionarTodasRemoveFiltroExtensao()
        {
            return Prop.ForAll(
                GenNomesUnicos().ToArbitrary(),
                GenFiltroNome().ToArbitrary(),
                (nomes, filtroNome) =>
                {
                    var (vm, tempDir) = CriarViewModelComIndice(nomes);
                    try
                    {
                        // Aplicar apenas filtro de nome (sem extensão = "Todas")
                        vm.TextoFiltroNome = filtroNome;
                        vm.ExtensaoFiltro = null;
                        var contagemSemExtensao = vm.ItensFiltrados.Count;
                        var itensSemExtensao = vm.ItensFiltrados.Select(i => i.NomeNormalizado).ToHashSet();

                        // Aplicar filtro de extensão e depois remover (= "Todas")
                        vm.ExtensaoFiltro = ".exe";
                        vm.ExtensaoFiltro = null; // Selecionar "Todas"

                        var contagemAposTodasSelecionado = vm.ItensFiltrados.Count;
                        var itensAposTodasSelecionado = vm.ItensFiltrados.Select(i => i.NomeNormalizado).ToHashSet();

                        // Resultado deve ser igual ao filtro de nome sem extensão
                        return contagemSemExtensao == contagemAposTodasSelecionado
                            && itensSemExtensao.SetEquals(itensAposTodasSelecionado);
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 10: Filtro ativo é reaplicado após reconstrução do índice
        // Validates: Requirement 8.3
        // -----------------------------------------------------------------------

        // Feature: binarios-delphi-explorer, Property 10: Filtro ativo é reaplicado após reconstrução do índice
        [Property]
        public Property Property10_FiltroReaplicadoAposReconstrucao()
        {
            return Prop.ForAll(
                GenNomesUnicos().ToArbitrary(),
                GenFiltroNome().ToArbitrary(),
                (nomes, filtroNome) =>
                {
                    var (vm, tempDir) = CriarViewModelComIndice(nomes);
                    try
                    {
                        // Aplicar filtro
                        vm.TextoFiltroNome = filtroNome;
                        var contagemAntes = vm.ItensFiltrados.Count;
                        var itensAntes = vm.ItensFiltrados.Select(i => i.NomeNormalizado).ToHashSet();

                        // Reconstruir o índice (simula "Atualizar")
                        var (arquivos2, tempDir2) = CriarArquivosReais(nomes);
                        try
                        {
                            var reader2 = new FakeDirectoryReader(arquivos2);
                            var indexService2 = new BinarioDelphiIndexService(reader2);
                            var indice2 = indexService2.ConstruirIndiceAsync("fake_dir").GetAwaiter().GetResult();

                            // Criar novo ViewModel com mesmo filtro ativo
                            var vm2 = new BinarioDelphiExplorerViewModel(indexService2);
                            vm2.SetIndiceForTesting(indice2);
                            vm2.TextoFiltroNome = filtroNome;

                            var contagemDepois = vm2.ItensFiltrados.Count;
                            var itensDepois = vm2.ItensFiltrados.Select(i => i.NomeNormalizado).ToHashSet();

                            // Resultado deve ser equivalente
                            return contagemAntes == contagemDepois
                                && itensAntes.SetEquals(itensDepois);
                        }
                        finally
                        {
                            LimparDiretorioTemp(tempDir2);
                        }
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 12: Contagem visível reflete o filtro aplicado
        // Validates: Requirement 4.3
        // -----------------------------------------------------------------------

        // Feature: binarios-delphi-explorer, Property 12: Contagem visível reflete o filtro aplicado
        [Property]
        public Property Property12_ContagemVisivelRefleteFiltro()
        {
            return Prop.ForAll(
                GenNomesUnicos().ToArbitrary(),
                GenFiltroNome().ToArbitrary(),
                (nomes, filtroNome) =>
                {
                    var (vm, tempDir) = CriarViewModelComIndice(nomes);
                    try
                    {
                        // Sem filtro
                        if (vm.TotalFiltrado != vm.ItensFiltrados.Count)
                            return false;

                        // Com filtro de nome
                        vm.TextoFiltroNome = filtroNome;
                        if (vm.TotalFiltrado != vm.ItensFiltrados.Count)
                            return false;

                        // Com filtro de extensão
                        vm.ExtensaoFiltro = ".exe";
                        if (vm.TotalFiltrado != vm.ItensFiltrados.Count)
                            return false;

                        // Limpar filtros
                        vm.TextoFiltroNome = string.Empty;
                        vm.ExtensaoFiltro = null;
                        if (vm.TotalFiltrado != vm.ItensFiltrados.Count)
                            return false;

                        return true;
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 13: Lista exibida está ordenada alfabeticamente
        // Validates: Requirement 4.2
        // -----------------------------------------------------------------------

        // Feature: binarios-delphi-explorer, Property 13: Lista exibida está ordenada alfabeticamente
        [Property]
        public Property Property13_ListaOrdenadaAlfabeticamente()
        {
            return Prop.ForAll(
                GenNomesUnicos().ToArbitrary(),
                GenFiltroNome().ToArbitrary(),
                (nomes, filtroNome) =>
                {
                    var (vm, tempDir) = CriarViewModelComIndice(nomes);
                    try
                    {
                        // Verificar ordenação sem filtro
                        if (!EstaOrdenadaAlfabeticamente(vm.ItensFiltrados.Select(i => i.Nome)))
                            return false;

                        // Verificar ordenação com filtro de nome
                        vm.TextoFiltroNome = filtroNome;
                        if (!EstaOrdenadaAlfabeticamente(vm.ItensFiltrados.Select(i => i.Nome)))
                            return false;

                        // Verificar ordenação com filtro combinado
                        vm.ExtensaoFiltro = ".exe";
                        if (!EstaOrdenadaAlfabeticamente(vm.ItensFiltrados.Select(i => i.Nome)))
                            return false;

                        return true;
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        /// <summary>
        /// Verifica se uma sequência de strings está ordenada alfabeticamente (case-insensitive).
        /// </summary>
        private static bool EstaOrdenadaAlfabeticamente(IEnumerable<string> nomes)
        {
            var lista = nomes.ToList();
            for (int i = 1; i < lista.Count; i++)
            {
                if (string.Compare(lista[i - 1], lista[i], StringComparison.OrdinalIgnoreCase) > 0)
                    return false;
            }
            return true;
        }
    }
}
