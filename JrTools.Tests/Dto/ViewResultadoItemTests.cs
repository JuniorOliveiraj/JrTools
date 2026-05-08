using System.Collections.Generic;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using JrTools.Dto;
using Xunit;

namespace JrTools.Tests.Dto
{
    /// <summary>
    /// Testes de propriedade para <see cref="ViewResultadoItem"/>.
    /// </summary>
    [Properties(MaxTest = 100)]
    public class ViewResultadoItemTests
    {
        // -----------------------------------------------------------------------
        // Property 6: TotalCaminhos é sempre igual a Caminhos.Count
        // Feature: view-path-explorer, Property 6: TotalCaminhos é sempre igual a Caminhos.Count
        // Validates: Requisito 5.6
        // -----------------------------------------------------------------------

        [Property]
        public Property Property6_TotalCaminhosIgualCaminhosCount()
        {
            return Prop.ForAll(
                Arb.Default.List<string>(),
                caminhos =>
                {
                    var item = new ViewResultadoItem
                    {
                        Caminhos = caminhos ?? new List<string>()
                    };

                    return item.TotalCaminhos == item.Caminhos.Count;
                });
        }

        // -----------------------------------------------------------------------
        // Testes de exemplo complementares
        // -----------------------------------------------------------------------

        [Fact]
        public void SemCaminhos_DeveSerTrue_QuandoCaminhosVazio()
        {
            var item = new ViewResultadoItem();
            Assert.True(item.SemCaminhos);
            Assert.Equal(0, item.TotalCaminhos);
        }

        [Fact]
        public void SemCaminhos_DeveSerFalse_QuandoCaminhosNaoVazio()
        {
            var item = new ViewResultadoItem
            {
                Caminhos = new List<string> { "Página > Widget [IWFuncionarios]" }
            };
            Assert.False(item.SemCaminhos);
            Assert.Equal(1, item.TotalCaminhos);
        }

        [Fact]
        public void TotalCaminhos_DeveRefletirContagem_AposAdicionar()
        {
            var item = new ViewResultadoItem();
            item.Caminhos.Add("Caminho 1");
            item.Caminhos.Add("Caminho 2");
            item.Caminhos.Add("Caminho 3");

            Assert.Equal(3, item.TotalCaminhos);
            Assert.False(item.SemCaminhos);
        }
    }
}
