using JrTools.Dto;
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

        [Fact]
        public void DescricaoCompleta_ContemDadosFormatados()
        {
            var dto = new ArtefatoDto
            {
                Identificador = "MINHA_PAGINA",
                Guia = "Pages",
                Camada = "20",
                Status = StatusArtefato.Novo
            };

            Assert.Equal("MINHA_PAGINA [Pages] [Novo] (Camada 20)", dto.DescricaoCompleta);
            Assert.Equal("Camada 20", dto.CamadaFormatada);
        }
    }
}
