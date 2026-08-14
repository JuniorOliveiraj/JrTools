using JrTools.Dto;
using JrTools.Flows;
using System;
using System.Collections.Generic;
using Xunit;

namespace JrTools.Tests.Flows
{
    public class HorasToggleTests
    {
        [Fact]
        public void SugerirProximoHorarioInicio_NoEntries_Returns0800()
        {
            var service = new HorasToggle("dummy");
            var result = service.SugerirProximoHorarioInicio(new List<HoraLancamento>());
            Assert.Equal(TimeSpan.FromHours(8), result);
        }

        [Fact]
        public void SugerirProximoHorarioInicio_WithEntries_ReturnsLastEnd()
        {
            var service = new HorasToggle("dummy");
            var lancamentos = new List<HoraLancamento>
            {
                new HoraLancamento { HoraInicio = TimeSpan.FromHours(8), HoraFim = TimeSpan.FromHours(9) },
                new HoraLancamento { HoraInicio = TimeSpan.FromHours(10), HoraFim = TimeSpan.FromHours(11) }
            };

            var result = service.SugerirProximoHorarioInicio(lancamentos);
            Assert.Equal(TimeSpan.FromHours(11), result);
        }

        [Fact]
        public void SugerirProximoHorarioInicio_WithEntriesOnlyDuration_CalculatesEnd()
        {
            var service = new HorasToggle("dummy");
            var lancamentos = new List<HoraLancamento>
            {
                new HoraLancamento { HoraInicio = TimeSpan.FromHours(8), TotalHoras = 1.5 } // Ends 09:30
            };

            var result = service.SugerirProximoHorarioInicio(lancamentos);
            Assert.Equal(TimeSpan.FromHours(9.5), result);
        }

        [Fact]
        public void SugerirProximoHorarioInicio_OutOfOrder_FindsLatest()
        {
            var service = new HorasToggle("dummy");
            var lancamentos = new List<HoraLancamento>
            {
                new HoraLancamento { HoraInicio = TimeSpan.FromHours(13), HoraFim = TimeSpan.FromHours(14) },
                new HoraLancamento { HoraInicio = TimeSpan.FromHours(8), HoraFim = TimeSpan.FromHours(9) }
            };

            var result = service.SugerirProximoHorarioInicio(lancamentos);
            Assert.Equal(TimeSpan.FromHours(14), result);
        }
    }
}
