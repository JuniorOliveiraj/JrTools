using JrTools.Dto;
using JrTools.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;

namespace JrTools.Pages.Apps
{
    public sealed partial class CriarAplicacaoIisPage : Page
    {
        private const int MAX_LOG = 15000;
        private const string BASE_FONTES = @"D:\Benner\fontes\rh";
        private const string WES_SUBPATH = @"WES\WebApp";

        private readonly IisService _iis = new();

        public CriarAplicacaoIisPage()
        {
            InitializeComponent();
            NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await Task.WhenAll(CarregarProjetosAsync(), CarregarPoolsAsync());
        }

        // ── Carregamento ─────────────────────────────────────────────────────

        private Task CarregarProjetosAsync()
            => Task.Run(() =>
            {
                var projetos = Folders.ListarPastas(BASE_FONTES);
                DispatcherQueue.TryEnqueue(() =>
                {
                    CmbProjeto.ItemsSource    = projetos;
                    CmbProjeto.DisplayMemberPath = "Nome";
                    CmbProjeto.PlaceholderText   = projetos.Count == 0 ? "Nenhum projeto encontrado" : "Selecione o projeto";
                });
            });

        private async Task CarregarPoolsAsync()
        {
            try
            {
                var pools = await _iis.ListarPoolsAsync();
                CmbPool.ItemsSource    = pools;
                CmbPool.PlaceholderText = pools.Count == 0 ? "Nenhuma pool encontrada" : "Selecione a pool";
            }
            catch (Exception ex)
            {
                AppendLog($"[AVISO] Não foi possível listar pools: {ex.Message}");
                CmbPool.PlaceholderText = "Erro ao carregar pools";
            }
        }

        // ── Eventos ──────────────────────────────────────────────────────────

        private void CmbProjeto_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProjeto.SelectedItem is not PastaInformacoesDto projeto) return;
            TxtCaminho.Text = Path.Combine(projeto.Caminho, WES_SUBPATH);
        }

        private async void BtnCriar_Click(object sender, RoutedEventArgs e)
        {
            var site      = TxtSite.Text.Trim();
            var nomeApp   = TxtNomeApp.Text.Trim();
            var pool      = CmbPool.SelectedItem?.ToString();
            var caminho   = TxtCaminho.Text.Trim();

            if (string.IsNullOrWhiteSpace(site)    ||
                string.IsNullOrWhiteSpace(nomeApp) ||
                string.IsNullOrWhiteSpace(pool)    ||
                string.IsNullOrWhiteSpace(caminho))
            {
                MostrarErro("Preencha todos os campos antes de criar.");
                return;
            }

            InfoBarAviso.IsOpen    = false;
            LoadingCriar.IsActive  = true;
            BtnCriar.IsEnabled     = false;
            try
            {
                await _iis.CriarAplicacaoAsync(site, nomeApp, pool, caminho, CriarProgresso());
            }
            catch (Exception ex)
            {
                MostrarErro(ex.Message);
                AppendLog($"[ERRO] {ex.Message}");
            }
            finally
            {
                LoadingCriar.IsActive = false;
                BtnCriar.IsEnabled    = true;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void MostrarErro(string msg)
        {
            InfoBarAviso.Message  = msg;
            InfoBarAviso.Severity = InfoBarSeverity.Error;
            InfoBarAviso.IsOpen   = true;
        }

        private IProgress<string> CriarProgresso() => new Progress<string>(AppendLog);

        private void AppendLog(string linha)
        {
            if (DispatcherQueue.HasThreadAccess) EscreverLog(linha);
            else DispatcherQueue.TryEnqueue(() => EscreverLog(linha));
        }

        private void EscreverLog(string linha)
        {
            TxtLog.Text += linha + "\n";
            if (TxtLog.Text.Length > MAX_LOG)
                TxtLog.Text = TxtLog.Text[^MAX_LOG..];
            ScrollLog.ChangeView(null, ScrollLog.ScrollableHeight, null);
        }

        private void BtnLimparLog_Click(object sender, RoutedEventArgs e)
            => TxtLog.Text = string.Empty;
    }
}
