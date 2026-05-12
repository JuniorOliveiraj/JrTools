using JrTools.Dto;
using JrTools.Services.Db;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class SisconService
    {
        private readonly HttpClient _httpClient;
        private DadosPessoaisDataObject _dados;

        private const string BaseUrl = "https://siscon.benner.com.br";
        private const string TokenUrl = "https://siscon.benner.com.br/app_services/auth.oauth2.svc/token";

        public SisconService(DadosPessoaisDataObject dados)
        {
            _httpClient = new HttpClient();
            _dados = dados;
        }

        /// <summary>
        /// Retorna true se o token ainda é válido (existe e não expirou).
        /// </summary>
        public bool TokenValido() =>
            !string.IsNullOrEmpty(_dados.TokenSiscon) &&
            _dados.TokenSisconExpiraEm.HasValue &&
            DateTime.Now < _dados.TokenSisconExpiraEm.Value;

        /// <summary>
        /// Faz login OAuth2 no Siscon e persiste o token.
        /// </summary>
        public async Task<bool> LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(_dados.LoginSiscon) ||
                string.IsNullOrWhiteSpace(_dados.SenhaSiscon))
                return false;

            var content = new StringContent(
                $"username={Uri.EscapeDataString(_dados.LoginSiscon)}" +
                $"&password={Uri.EscapeDataString(_dados.SenhaSiscon)}" +
                $"&grant_type=password",
                Encoding.UTF8,
                "application/x-www-form-urlencoded"
            );

            try
            {
                var response = await _httpClient.PostAsync(TokenUrl, content);
                if (!response.IsSuccessStatusCode) return false;

                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<SisconLoginResponse>(json, options);

                if (result?.AccessToken == null) return false;

                _dados.TokenSiscon = result.AccessToken;
                _dados.TokenSisconExpiraEm = DateTime.Now.AddSeconds(result.ExpiresIn - 30);

                await PerfilPessoalHelper.SalvarConfiguracoesAsync(_dados);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// GET autenticado. Renova o token automaticamente se necessário.
        /// </summary>
        public async Task<string> GetAsync(string path)
        {
            if (!TokenValido())
            {
                var ok = await LoginAsync();
                if (!ok) throw new Exception("Falha ao autenticar no Siscon. Verifique login e senha nas configurações.");
            }

            var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
            request.Headers.Add("accept", "application/json");
            request.Headers.Add("Authorization", $"Bearer {_dados.TokenSiscon}");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Siscon retornou {(int)response.StatusCode} em {path}. Verifique as credenciais ou tente novamente.");

            return await response.Content.ReadAsStringAsync();
        }

        // ── Rotas de negócio ────────────────────────────────────────────────

        /// <summary>GET /api/minhas/informacoes — dados do usuário logado.</summary>
        public Task<string> GetMinhasInformacoesAsync() =>
            GetAsync("/api/minhas/informacoes");

        /// <summary>GET /api/minhas/atividades — SMSs do usuário.</summary>
        public Task<string> GetMinhasAtividadesAsync() =>
            GetAsync("/api/minhas/atividades");

        /// <summary>GET /api/minhas/horas — horas lançadas.</summary>
        public Task<string> GetMinhasHorasAsync() =>
            GetAsync("/api/minhas/horas");

        /// <summary>GET /api/atividades/{id} — detalhe de uma SMS.</summary>
        public Task<string> GetAtividadeDetalheAsync(int id) =>
            GetAsync($"/api/atividades/{id}");
    }

    public class SisconLoginResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }
    }
}
