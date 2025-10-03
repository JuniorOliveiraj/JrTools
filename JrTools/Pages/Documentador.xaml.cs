using JrTools.Dto;
using JrTools.Flows;
using JrTools.Services;
using JrTools.Services.Db;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Markdig;
using Windows.ApplicationModel.DataTransfer;
namespace JrTools.Pages
{
    public sealed partial class Documentador : Page
    {
        private FileInfo _arquivoAnexado; // Arquivo selecionado

        public Documentador()
        {
            InitializeComponent();
        }

        private void AttachFileButton_Click(object sender, RoutedEventArgs e)
        {

            if (sender is Button button)
            {
                button.IsEnabled = false;

                try
                {
                    using var dialog = new System.Windows.Forms.OpenFileDialog();
                    dialog.Filter = "Documentos (*.pdf;*.docx;*.doc;*.txt)|*.pdf;*.docx;*.doc;*.txt";
                    dialog.Multiselect = false;

                    var result = dialog.ShowDialog();
                    if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrEmpty(dialog.FileName))
                    {
                        _arquivoAnexado = new FileInfo(dialog.FileName);
                        AppendTerminalLog($"📎 Arquivo anexado: {_arquivoAnexado.Name}");


                        CommandTextBox.Text = "Analize em anexo Agora, gere a documentação completa, conforme as instruções.";
                    }
                    else
                    {
                        AppendTerminalLog("⚠️ Nenhum arquivo selecionado.");
                    }
                }
                catch (Exception ex)
                {
                    AppendTerminalLog($"❌ Erro ao anexar arquivo: {ex.Message}");
                }
                finally
                {
                    button.IsEnabled = true;
                }
            }
        }


        private async void CopiarTudoButton_Click(object sender, RoutedEventArgs e)
        {
            var markdown = TerminalOutput.Text;

            if (!string.IsNullOrEmpty(markdown))
            {
                // Converte Markdown para HTML
                string html = Markdown.ToHtml(markdown);

                // Cria DataPackage com HTML
                var dataPackage = new DataPackage();
                dataPackage.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(html));

                // Copia para o clipboard
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

                // Feedback para o usuário
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Copiado!",
                    Content = "Todo o conteúdo foi copiado para a área de transferência com formatação.",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };

                await dialog.ShowAsync();
            }
        }

        private async void ExportPdfButton_Click(object sender, RoutedEventArgs e)
        {
            // Implementar exportação PDF se necessário
        }

        private async void Hyperlink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            var uri = sender.NavigateUri;
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        private async void ProcessarDotnetButton_Click(object sender, RoutedEventArgs e)
        {
            var button = BuildarDotnetButton;
            button.IsEnabled = false;           
            LoadingRing.IsActive = true;       

            var progresso = new Progress<string>(msg => AppendTerminalLog(msg));

            try
            {
                var perfil = await PerfilPessoalHelper.LerConfiguracoesAsync();

                if (perfil == null || string.IsNullOrWhiteSpace(perfil.ApiGemini))
                {
                    ValidationInfoBarUrl.Message = "⚠️ Configuração da API Gemini não encontrada.";
                    ValidationInfoBarUrl.IsOpen = true;
                    return;
                }

                if (string.IsNullOrEmpty(CommitTextBox.Text))
                {
                    ValidationInfoBar.Message = "O campo commit não pode ser vazio.";
                    ValidationInfoBar.IsOpen = true;
                    return;
                }
                ExpanderCommit.IsExpanded = false;

                if (string.IsNullOrEmpty(CommandTextBox.Text) && _arquivoAnexado == null)
                {
                    ValidationInfoBar.Message = "O comando não pode ser vazio.";
                    ValidationInfoBar.IsOpen = true;
                    return;
                }
                ExpanderPrompt.IsExpanded = false;

                await ExecutarComProcessosFechadosAsync(async prog =>
                {
                    try
                    {
                        string promptFinal = await PromptBuilder.ConstruirPromptAsync(CommitTextBox.Text, CommandTextBox.Text);
                        var geminiService = new GeminiService(perfil.ApiGemini);
                        TerminalOutput.Text = "";

                        if (_arquivoAnexado == null)
                        {
                            var resposta = await geminiService.EnviarPromptAsync(promptFinal, prog);
                            AppendTerminalLog(resposta);
                        }
                        else
                        {
                            string caminhoLocal = _arquivoAnexado.FullName;

                            // Copia para temp se necessário
                            string tempFolder = Path.GetTempPath();
                            string tempFilePath = Path.Combine(tempFolder, _arquivoAnexado.Name);
                            File.Copy(_arquivoAnexado.FullName, tempFilePath, true);

                            var arquivosLocais = new List<string> { tempFilePath };
                            var resposta = await geminiService.EnviarPromptComArquivoAsync(promptFinal, arquivosLocais);
                            AppendTerminalLog(resposta);
                        }

                        AppendTerminalLog("✅ Documentação gerada com sucesso.");
                    }
                    catch (Exception ex)
                    {
                        ValidationInfoBar.Message = $"[ERRO] Build falhou: {ex.Message}";
                        ValidationInfoBar.IsOpen = true;
                        prog.Report($"[ERRO] Build falhou: {ex.Message}");
                    }
                });
            }
            finally
            {
                button.IsEnabled = true;       
                LoadingRing.IsActive = false;   
            }
        }


        private async Task ExecutarComProcessosFechadosAsync(Func<IProgress<string>, Task> acao)
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
