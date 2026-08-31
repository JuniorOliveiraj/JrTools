using JrTools.Pages;
using Xunit;

namespace JrTools.Tests.Pages
{
    /// <summary>
    /// Testes para <see cref="HomePage.FormatarSaldoHoras"/> — regressão do bug em que o saldo
    /// do Banco de Horas aparecia como "-0h 504m" em vez de "-5h 04m" pra saldos como "-05:04".
    /// </summary>
    public class HomePageTests
    {
        [Theory]
        [InlineData("-05:04", "-5h 04m")]
        [InlineData("-15:04", "-15h 04m")]
        [InlineData("042:30", "42h 30m")]
        [InlineData("00:00", "0h 00m")]
        [InlineData("5:07", "5h 07m")]
        public void FormatarSaldoHoras_ComFormatoValido_FormataCorretamente(string entrada, string esperado)
        {
            Assert.Equal(esperado, HomePage.FormatarSaldoHoras(entrada));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void FormatarSaldoHoras_ComEntradaVaziaOuNula_RetornaZerado(string? entrada)
        {
            Assert.Equal("0h 00m", HomePage.FormatarSaldoHoras(entrada));
        }

        [Fact]
        public void FormatarSaldoHoras_SemDoisPontos_UsaValorComoHoras()
        {
            Assert.Equal("5h 00m", HomePage.FormatarSaldoHoras("5"));
        }
    }
}
