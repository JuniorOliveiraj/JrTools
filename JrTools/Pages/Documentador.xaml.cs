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
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Markdig;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Word = DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using HtmlToOpenXml; // <--- 1. ADICIONADO
using Windows.ApplicationModel; // <--- 2. ADICIONADO

namespace JrTools.Pages
{
    public sealed partial class Documentador : Page
    {
        private FileInfo _arquivoAnexado; // Arquivo selecionado

        public Documentador()
        {
            InitializeComponent();
        }

        // Método original (intacto)
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
                        CommandTextBox.Text = "Analise em anexo Agora, gere a documentação completa, conforme as instruções.";
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

        // Método original (intacto)
        private async void CopiarTudoButton_Click(object sender, RoutedEventArgs e)
        {
            var markdown = TerminalOutput.Text;

            if (!string.IsNullOrEmpty(markdown))
            {
                string html = Markdown.ToHtml(markdown);

                var dataPackage = new DataPackage();
                dataPackage.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(html));

                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

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

        // --- 3. MÉTODO SUBSTITUÍDO ---
        private async void ExportDOCXButton_Click(object sender, RoutedEventArgs e)
        {
            var conteudo = TerminalOutput.Text;

            if (string.IsNullOrWhiteSpace(conteudo))
            {
                var dialogEmpty = new ContentDialog
                {
                    Title = "Nada para exportar",
                    Content = "O conteúdo do terminal está vazio.",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialogEmpty.ShowAsync();
                return;
            }

            try
            {
                string html = Markdown.ToHtml(conteudo);

                using var saveDialog = new System.Windows.Forms.SaveFileDialog();
                saveDialog.Filter = "Documento Word (*.docx)|*.docx";
                saveDialog.FileName = "documentacao.docx";

                if (saveDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // Caminho correto para a pasta Assets em um app WinUI empacotado
                    string templatePath = Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "docPadrao.docx");
                    string novoArquivo = saveDialog.FileName;

                    if (!File.Exists(templatePath))
                    {
                        throw new FileNotFoundException("Template 'docPadrao.docx' não encontrado em /Assets.", templatePath);
                    }

                    // Copia o template para o destino
                    File.Copy(templatePath, novoArquivo, true);

                    // Abre o novo arquivo (cópia do template)
                    using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(novoArquivo, true))
                    {
                        var mainPart = wordDoc.MainDocumentPart;

                        // Garante que o corpo exista (caso o template esteja vazio)
                        if (mainPart.Document == null)
                            mainPart.Document = new Word.Document(new Word.Body());
                        else if (mainPart.Document.Body == null)
                            mainPart.Document.Body = new Word.Body();

                        // Converte o HTML e ADICIONA ao final do documento
                        // (não removemos mais o conteúdo, preservando cabeçalhos/rodapés)
                        HtmlConverter converter = new HtmlConverter(mainPart);
                        converter.ParseHtml(html);

                        mainPart.Document.Save();
                    }

                    var dialogSuccess = new ContentDialog
                    {
                        Title = "Exportação concluída",
                        Content = $"O documento foi salvo em:\n{novoArquivo}",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await dialogSuccess.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var dialogError = new ContentDialog
                {
                    Title = "Erro ao exportar",
                    Content = $"Não foi possível exportar o DOCX: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialogError.ShowAsync();
            }
        }

        // --- 4. MÉTODOS ANTIGOS REMOVIDOS ---
        // 'ProcessarHtmlParaDocx', 'ProcessarNodoHtml',
        // 'CriarRunComPropriedades' e 'CriarParagrafoSimples'
        // foram removidos pois não são mais chamados.

        // Método original (intacto)
        private async void Hyperlink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            var uri = sender.NavigateUri;
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        // Método original (intacto)
        private async void ProcessarDotnetButton_Click(object sender, RoutedEventArgs e)
        {
            var button = BuildarDotnetButton;
            button.IsEnabled = false;
            LoadingRing.IsActive = true;

            var config = await ConfigHelper.LerConfiguracoesAsync();
            string workingDir = config.DiretorioProducao;
            string caminhoAlteracao = Path.Combine(workingDir, "alteracao.txt");

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
                if (File.Exists(caminhoAlteracao))
                {
                    File.Delete(caminhoAlteracao);
                }
            }
        }

        // Método original (intacto)
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

        // Método original (intacto)
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