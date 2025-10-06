using JrTools.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class TogglClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://api.track.toggl.com/api/v9";
        private readonly string _token;


        public TogglClient(string apiToken)
        {
            _token = apiToken ?? throw new ArgumentNullException(nameof(apiToken));
            _httpClient = new HttpClient();

            // Autenticação básica → token como usuário, "api_token" como senha
            var authBytes = Encoding.ASCII.GetBytes($"{_token}:api_token");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        }

        public async Task<JsonElement> GetMeAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/me");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(content).RootElement;
        }

        public async Task<JsonElement> GetWorkspacesAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/workspaces");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(content).RootElement;
        }

        public async Task<JsonElement> CreateTimeEntryAsync(long workspaceId, HoraLancamento horaLancamento)
        {
            if (horaLancamento.Data == null)
                horaLancamento.Data = DateTime.Today;

            var startDateTime = horaLancamento.Data.Value
                .Add(horaLancamento.HoraInicio ?? TimeSpan.Zero);

            double durationSeconds = 0;

            if (horaLancamento.HoraFim.HasValue)
            {
                var endDateTime = horaLancamento.Data.Value.Add(horaLancamento.HoraFim.Value);
                durationSeconds = (endDateTime - startDateTime).TotalSeconds;

                if (durationSeconds < 0)
                    durationSeconds = 0;
            }
            else if (horaLancamento.TotalHoras.HasValue && horaLancamento.TotalHoras > 0)
            {
                durationSeconds = horaLancamento.TotalHoras.Value * 3600;
            }
            else
            {
                durationSeconds = -1;
            }

            var payload = new
            {
                description = horaLancamento.Descricao ?? "Lançamento sem descrição",
                start = startDateTime.ToString("yyyy-MM-ddTHH:mm:sszzz"), // ISO 8601 com timezone
                duration = (int)durationSeconds,
                wid = workspaceId,
                created_with = "csharp-integration"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/workspaces/{workspaceId}/time_entries", content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(responseBody).RootElement;
        }



        public async Task<JsonElement> GetTodayTimeEntriesAsync(DateTime dataLancamento)
        {

            var offset = DateTimeOffset.Now.Offset; // timezone local

            // define início e fim do dia
            var start = new DateTimeOffset(dataLancamento.Year, dataLancamento.Month, dataLancamento.Day, 0, 0, 0, offset);
            var end = start.AddDays(1).AddSeconds(-1);

            string startStr = Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:sszzz"));
            string endStr = Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:sszzz"));

            string url = $"{_baseUrl}/me/time_entries?start_date={startStr}&end_date={endStr}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(body).RootElement;
        }


    }
}
