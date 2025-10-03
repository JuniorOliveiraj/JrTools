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
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace JrTools.Pages
{
    public sealed partial class Documentador : Page
    {
        private Windows.Storage.StorageFile _arquivoAnexado;

        public Documentador()
        {
            InitializeComponent();
        }

        private async void AttachFileButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();

            // Pega o HWND da janela principal
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow); // App.MainWindow precisa estar definido
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;

            picker.FileTypeFilter.Add(".pdf");
            picker.FileTypeFilter.Add(".docx");
            picker.FileTypeFilter.Add(".doc");
            picker.FileTypeFilter.Add(".txt");

            var file = await picker.PickSingleFileAsync();

            if (file != null)
            {
                _arquivoAnexado = file;
                AppendTerminalLog($"📎 Arquivo anexado: {_arquivoAnexado.Name}");
            }
            else
            {
                AppendTerminalLog("⚠️ Nenhum arquivo selecionado.");
            }
        }
        private async void CopiarTudoButton_Click(object sender, RoutedEventArgs e)
        {
            var texto = TerminalOutput.Text;

            if (!string.IsNullOrEmpty(texto))
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(texto);
                Clipboard.SetContent(dataPackage);

                // Mensagem de feedback
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Copiado!",
                    Content = "Todo o conteúdo foi copiado para a área de transferência.",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };

                await dialog.ShowAsync();
            }
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
            if (string.IsNullOrEmpty(CommandTextBox.Text))
            {
                if(_arquivoAnexado == null)
                {
                    ValidationInfoBar.Message = "O comando não pode ser vazio.";
                    ValidationInfoBar.IsOpen = true;
                    return;
                }

            }
            ExpanderPrompt.IsExpanded = false;


            await ExecutarComProcessosFechadosAsync(async progresso =>
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

                    if (_arquivoAnexado == null)
                    {
                        var resposta = await geminiService.EnviarPromptAsync(promptFinal, progresso);
                        AppendTerminalLog(resposta);
                    }
                    else
                    {
                        string caminhoLocal;

                        if (!string.IsNullOrEmpty(_arquivoAnexado.Path))
                        {
                            caminhoLocal = _arquivoAnexado.Path;
                        }
                        else
                        {
                            var tempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(_arquivoAnexado.Name, CreationCollisionOption.ReplaceExisting);
                            await _arquivoAnexado.CopyAndReplaceAsync(tempFile);
                            caminhoLocal = tempFile.Path;
                        }

                        var arquivosLocais = new List<string> { caminhoLocal };
                        var resposta = await geminiService.EnviarPromptComArquivoAsync(promptFinal, arquivosLocais); 
                        AppendTerminalLog(resposta); 
                    }

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
