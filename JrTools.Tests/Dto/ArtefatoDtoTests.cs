using JrTools.Dto;
using JrTools.Enums;
using Xunit;

namespace JrTools.Tests.Dto
{
    public class ArtefatoDtoTests
    {
        [Theory]
        [InlineData(StatusArtefato.Novo, true)]
        [InlineData(StatusArtefato.Modificado, true)]
        [InlineData(StatusArtefato.Instalado, false)]
        [InlineData(StatusArtefato.Excluido, false)]
        [InlineData(StatusArtefato.NaoVerificado, false)]
        public void IsPendente_RetornaVerdadeiroApenasParaNovoOuModificado(StatusArtefato status, bool esperado)
        {
            var dto = new ArtefatoDto { Status = status };
            Assert.Equal(esperado, dto.IsPendente);
            Assert.Equal(esperado, status.IsPendente());
        }

        [Theory]
        [InlineData("FileOnly", StatusArtefato.Novo)]
        [InlineData("Novo", StatusArtefato.Novo)]
        [InlineData("fileonly", StatusArtefato.Novo)]
        [InlineData("Diferent", StatusArtefato.Modificado)]
        [InlineData("Different", StatusArtefato.Modificado)]
        [InlineData("Modificado", StatusArtefato.Modificado)]
        [InlineData("Equal", StatusArtefato.Instalado)]
        [InlineData("Instalado", StatusArtefato.Instalado)]
        [InlineData("DatabaseOnly", StatusArtefato.Excluido)]
        [InlineData("Excluido", StatusArtefato.Excluido)]
        [InlineData("Excluído", StatusArtefato.Excluido)]
        [InlineData("", StatusArtefato.NaoVerificado)]
        [InlineData(null, StatusArtefato.NaoVerificado)]
        [InlineData("QualquerOutraCoisa", StatusArtefato.NaoVerificado)]
        public void ParseStatus_ConverteStringsLegadasParaEnumCorreto(string? entrada, StatusArtefato esperado)
        {
            var resultado = StatusArtefatoExtensions.ParseStatus(entrada);
            Assert.Equal(esperado, resultado);
        }

        [Theory]
        [InlineData(StatusArtefato.Novo, "Novo")]
        [InlineData(StatusArtefato.Modificado, "Modificado")]
        [InlineData(StatusArtefato.Instalado, "Instalado")]
        [InlineData(StatusArtefato.Excluido, "Excluído")]
        [InlineData(StatusArtefato.NaoVerificado, "Não Verificado")]
        public void StatusFormatado_RetornaDescricaoCorreta(StatusArtefato status, string descricaoEsperada)
        {
            var dto = new ArtefatoDto { Status = status };
            Assert.Equal(descricaoEsperada, dto.StatusFormatado);
            Assert.Equal(descricaoEsperada, status.ToDescricao());
        }

        [Theory]
        [InlineData("Pages", TipoArtefato.Page)]
        [InlineData("Páginas", TipoArtefato.Page)]
        [InlineData("Views", TipoArtefato.View)]
        [InlineData("Visões", TipoArtefato.View)]
        [InlineData("Menus", TipoArtefato.Menu)]
        [InlineData("Tasks", TipoArtefato.Task)]
        [InlineData("Roles", TipoArtefato.Role)]
        [InlineData("Scripts", TipoArtefato.Script)]
        [InlineData("Filters", TipoArtefato.Filter)]
        [InlineData("DataSources", TipoArtefato.DataSource)]
        public void ParseTipo_ConverteNomePastaParaTipoCorreto(string entrada, TipoArtefato esperado)
        {
            var resultado = TipoArtefatoExtensions.ParseTipo(entrada);
            Assert.Equal(esperado, resultado);
        }

        [Theory]
        [InlineData("10", CamadaArtefato.Camada10)]
        [InlineData("20", CamadaArtefato.Camada20)]
        [InlineData("30", CamadaArtefato.Camada30)]
        [InlineData("40", CamadaArtefato.Camada40)]
        [InlineData("50", CamadaArtefato.Camada50)]
        [InlineData("99", CamadaArtefato.Desconhecida)]
        public void ParseCamada_ConverteStringParaEnumCorreto(string entrada, CamadaArtefato esperado)
        {
            var resultado = CamadaArtefatoExtensions.ParseCamada(entrada);
            Assert.Equal(esperado, resultado);
        }

        [Fact]
        public void DescricaoCompleta_ContemDadosFormatados()
        {
            var dto = new ArtefatoDto
            {
                Identificador = "MINHA_PAGINA",
                Tipo = TipoArtefato.Page,
                Camada = CamadaArtefato.Camada20,
                Status = StatusArtefato.Novo
            };

            Assert.Equal("MINHA_PAGINA [Pages] [Novo] (Camada 20)", dto.DescricaoCompleta);
            Assert.Equal("Camada 20", dto.CamadaFormatada);
            Assert.Equal("Pages", dto.Guia);
        }
    }
}
