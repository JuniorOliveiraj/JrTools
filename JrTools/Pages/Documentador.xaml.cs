using JrTools.Dto;
using JrTools.Flows;
using JrTools.Services;
using JrTools.Services.Db;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace JrTools.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Documentador : Page
    {
        public Documentador()
        {
            InitializeComponent();
        }

        private async void AttachFileButton_Click(object sender, RoutedEventArgs e)
        {

        }
        private async void ExportPdfButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void Hyperlink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            var uri = sender.NavigateUri;
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }
        private async void ProcessarDotnetButton_Click(object sender, RoutedEventArgs e)
        {
            var progresso = (IProgress<string>)new Progress<string>(msg => AppendTerminalLog(msg));
            //string commitId = "c5cab8a1367225050b4a004b63f2929c3d40aff1";

            var perfil = await PerfilPessoalHelper.LerConfiguracoesAsync();

            if (perfil == null || string.IsNullOrWhiteSpace(perfil.ApiGemini))
            {
                ValidationInfoBarUrl.Message = "";
                ValidationInfoBarUrl.IsOpen = true;
                return;
            }
            if (string.IsNullOrEmpty(CommitTextBox.Text))
            {
                ValidationInfoBar.Message = "O campo commit nao pode ser vazio";
                ValidationInfoBar.IsOpen = true;
                return;
            }
            if (string.IsNullOrEmpty(CommandTextBox.Text))
            {
                ValidationInfoBar.Message = "O comando nao pode ser vazio";
                ValidationInfoBar.IsOpen = true;
                return;
            }

            await ExecutarComProcessosFechadosAsync( async progresso =>
            {

                 try
                {

                    string promptFinal = await PromptBuilder.ConstruirPromptAsync(CommitTextBox.Text, CommandTextBox.Text);

                    if (perfil == null || string.IsNullOrWhiteSpace(perfil.ApiGemini))
                    {
                        AppendTerminalLog("❌ Configuração da API Gemini não encontrada.");
                        return;
                    }

                    var geminiService = new GeminiService(perfil.ApiGemini);
                    TerminalOutput.Text = "";


                    var resposta = await geminiService.EnviarPromptAsync(promptFinal, progresso);
                    AppendTerminalLog(resposta);

                    AppendTerminalLog("✅ Documentação gerada com sucesso.");
 

                }
                catch (Exception ex)
                {
                    ValidationInfoBar.Message = $"[ERRO] Build falhou: {ex.Message}";
                    ValidationInfoBar.IsOpen = true;
                    progresso.Report($"[ERRO] Build falhou: {ex.Message}");
                }
            });

        }
        private async Task ExecutarComProcessosFechadosAsync( Func<IProgress<string>, Task> acao)
        {
            using var cts = new CancellationTokenSource();
            var progresso = new Progress<string>(msg => AppendTerminalLog(msg));
 
            try
            {
                await acao(progresso);
            }
            finally
            {
                cts.Cancel();
             }
        }
        private void AppendTerminalLog(string mensagem)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                TerminalOutput.Text += mensagem + "\n";
                MarkdownScrollViewer.ChangeView(null, MarkdownScrollViewer.ScrollableHeight, null);
            });
        }
    }
}
