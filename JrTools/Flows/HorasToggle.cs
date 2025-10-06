using JrTools.Dto;
using JrTools.Services;
using JrTools.Services.Db;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JrTools.Flows
{
    public class HorasToggle
    {
        private readonly string _token;

        public HorasToggle(string token)
        {
            _token = token ?? throw new ArgumentNullException(nameof(token));
        }

        public static async Task<IEnumerable<string>> CarregarProjetosAsync()
        {
            var projetos = new List<string> { "Nenhum" };
            var projetosSalvos = await ProjetosHelper.LerProjetosAsync();

            foreach (var projeto in projetosSalvos)
                if (!projetos.Contains(projeto))
                    projetos.Add(projeto);

            return projetos;
        }

        public static async Task AdicionarProjetoAsync(string nomeProjeto, IEnumerable<string> projetosAtuais)
        {
            if (string.IsNullOrWhiteSpace(nomeProjeto) || !nomeProjeto.StartsWith("@") || nomeProjeto.Contains(' '))
                throw new InvalidOperationException("Formato inválido. Use o formato @ProjetoSemEspaco.");

            var lista = projetosAtuais.ToList();
            if (!lista.Contains(nomeProjeto))
            {
                lista.Add(nomeProjeto);
                await ProjetosHelper.SalvarProjetosAsync(lista.Where(p => p != "Nenhum"));
            }
        }

        public string GerarDescricaoFinal(string descricaoInput, string projeto)
        {
            descricaoInput = descricaoInput?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(descricaoInput) && string.IsNullOrWhiteSpace(projeto))
                throw new InvalidOperationException("Descrição ou projeto é obrigatória.");

            string descricaoFinal = descricaoInput;
            var regexSMS = new Regex(@"^(SMS-(\d+)|(\d+))\s*-?");
            var match = regexSMS.Match(descricaoInput);

            if (match.Success && match.Groups[3].Success && !match.Groups[1].Value.StartsWith("SMS-"))
                descricaoFinal = "SMS-" + match.Groups[3].Value + descricaoInput.Substring(match.Length);

            if (!string.IsNullOrEmpty(projeto))
                descricaoFinal = projeto + " " + descricaoFinal.Trim();
            else if (!match.Success)
                throw new InvalidOperationException("Descrição inválida. Deve começar com SMS-XXXXXX ou número da SMS.");

            return descricaoFinal.Trim();
        }

        public TimeSpan ObterHoraInicioDisponivel(TimeSpan? horaInicio, TimeSpan duracao, IEnumerable<HoraLancamento> lancamentos)
        {
            TimeSpan inicioAlmoco = TimeSpan.FromHours(12);
            TimeSpan fimAlmoco = TimeSpan.FromHours(13);

            TimeSpan inicio = horaInicio ?? (lancamentos.Any() ? lancamentos.Max(l => l.HoraFim ?? l.HoraInicio.Value) : TimeSpan.FromHours(8));
            TimeSpan fim = inicio + duracao;

            while (lancamentos.Any(l => (l.HoraFim ?? l.HoraInicio) > inicio && l.HoraInicio < fim)
                   || (inicio < fimAlmoco && fim > inicioAlmoco))
            {
                var conflitos = lancamentos.Where(l => (l.HoraFim ?? l.HoraInicio) > inicio && l.HoraInicio < fim).ToList();

                if (conflitos.Any())
                    inicio = conflitos.Max(l => l.HoraFim ?? l.HoraInicio.Value);
                else if (inicio < fimAlmoco && fim > inicioAlmoco)
                    inicio = fimAlmoco;

                fim = inicio + duracao;
            }

            return inicio;
        }

        public async Task SalvarLancamentoAsync(HoraLancamento lancamento)
        {
            if (string.IsNullOrWhiteSpace(_token))
                throw new InvalidOperationException("Token do Toggl não configurado. Vá para Configurações e adicione seu token.");

            var toggl = new TogglClient(_token);
            var me = await toggl.GetMeAsync();
            long workspaceId = me.GetProperty("default_workspace_id").GetInt64();

            var result = await toggl.CreateTimeEntryAsync(workspaceId, lancamento);
            Console.WriteLine($"🟢 Lançamento criado: {result.GetProperty("id").GetInt64()}");
        }

        public async Task<List<HoraLancamento>> CarregarLancamentosDoDiaAsync(DateTime dataLancamento)
        {
            var lancamentos = new List<HoraLancamento>();
            if (string.IsNullOrWhiteSpace(_token))
                return lancamentos;

            var toggl = new TogglClient(_token);
            var entries = await toggl.GetTodayTimeEntriesAsync(dataLancamento);

            foreach (var entry in entries.EnumerateArray())
            {
                var startUtc = entry.GetProperty("start").GetDateTime();
                DateTime localStart = startUtc.ToLocalTime();

                DateTime? localEnd = null;
                if (entry.TryGetProperty("stop", out var stopProp) && stopProp.ValueKind != JsonValueKind.Null)
                    localEnd = stopProp.GetDateTime().ToLocalTime();

                double totalHoras = 0;
                if (entry.TryGetProperty("duration", out var durProp))
                {
                    var dur = durProp.GetDouble();
                    if (dur > 0) totalHoras = dur / 3600.0;
                }

                lancamentos.Add(new HoraLancamento
                {
                    HoraInicio = localStart.TimeOfDay,
                    HoraFim = localEnd?.TimeOfDay,
                    TotalHoras = totalHoras,
                    Descricao = entry.GetProperty("description").GetString(),
                    Projeto = entry.TryGetProperty("project_id", out var projProp) && !projProp.ValueKind.Equals(JsonValueKind.Null)
                        ? projProp.GetInt64().ToString()
                        : null
                });
            }

            return lancamentos;
        }
    }
}
