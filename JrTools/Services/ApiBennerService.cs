using JrTools.Dto;
using JrTools.Services.Db;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class ApiBennerService
    {
        private readonly HttpClient _httpClient;
        private DadosPessoaisDataObject _dados;
        private DateTime _tokenExpiraEm;
        private string urlRh = "https://rh.portalbenner.com.br/RHWEB";
        public ApiBennerService(DadosPessoaisDataObject dados)
        {
            _httpClient = new HttpClient();
            _dados = dados;
        }

        public async Task<bool> LoginAsync()
        {
            string urlRhPadrao = string.IsNullOrEmpty(_dados.UrlRh) ? urlRh : _dados.UrlRh;
            var url = $"{urlRhPadrao}/app_services/auth.oauth2.svc/token";

            var content = new StringContent(
                $"username={_dados.LoginRhWeb}&password={_dados.SenhaRhWeb}&grant_type=password",
                Encoding.UTF8,
                "application/x-www-form-urlencoded"
            );

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var result = JsonSerializer.Deserialize<LoginResponse>(json, options);

            if (result?.AccessToken == null) return false;

            // Aqui já vai estar preenchido corretamente
            _dados.TokenRhWeb = result.AccessToken;
            _tokenExpiraEm = DateTime.Now.AddSeconds(result.ExpiresIn - 30);
            _dados.TokenRhExpiraEm = _tokenExpiraEm;

            await PerfilPessoalHelper.SalvarConfiguracoesAsync(_dados);
            return true;

        }

        private bool TokenValido() =>
            !string.IsNullOrEmpty(_dados.TokenRhWeb) && DateTime.Now < _dados.TokenRhExpiraEm;

        public async Task<string> GetAsync(string url)
        {
            var tokenValido = TokenValido();

            if (!TokenValido())
            {
                var loginOk = await LoginAsync();
                if (!loginOk) throw new Exception("Falha ao fazer login na API.");
            }
            string urlRhPadrao = string.IsNullOrEmpty(_dados.UrlRh) ? urlRh : _dados.UrlRh;

            var request = new HttpRequestMessage(HttpMethod.Get, (urlRhPadrao + url));
            request.Headers.Add("Authorization", $"Bearer {_dados.TokenRhWeb}");

            var response = await _httpClient.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
public class LoginResponse
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
