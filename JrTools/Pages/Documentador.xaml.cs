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
                    string templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "docPadrao.docx");
                    string novoArquivo = saveDialog.FileName;

                    File.Copy(templatePath, novoArquivo, true);

                    using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(novoArquivo, true))
                    {
                        var mainPart = wordDoc.MainDocumentPart;
                        var body = mainPart.Document.Body;
                        body.RemoveAllChildren();

                        // Processa o HTML e adiciona ao documento
                        ProcessarHtmlParaDocx(html, body);

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

        private void ProcessarHtmlParaDocx(string html, Word.Body body)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            foreach (var node in doc.DocumentNode.ChildNodes)
            {
                if (node.NodeType == HtmlNodeType.Text)
                {
                    if (!string.IsNullOrWhiteSpace(node.InnerText))
                    {
                        body.AppendChild(CriarParagrafoSimples(node.InnerText));
                    }
                }
                else if (node.Name == "p" || node.Name == "div")
                {
                    var paragrafo = new Word.Paragraph();
                    ProcessarNodoHtml(node, paragrafo);

                    // Adiciona espaçamento após o parágrafo
                    paragrafo.ParagraphProperties = new Word.ParagraphProperties
                    {
                        SpacingBetweenLines = new Word.SpacingBetweenLines
                        {
                            After = "200",
                            Line = "276",
                            LineRule = Word.LineSpacingRuleValues.Auto
                        }
                    };

                    body.AppendChild(paragrafo);
                }
                else if (node.Name == "strong" || node.Name == "b")
                {
                    var paragrafo = new Word.Paragraph();
                    var run = CriarRunComPropriedades(node.InnerText, true);
                    paragrafo.AppendChild(run);
                    body.AppendChild(paragrafo);
                }
                else if (node.Name == "h1" || node.Name == "h2" || node.Name == "h3")
                {
                    var paragrafo = new Word.Paragraph();
                    var run = new Word.Run();
                    
                    // Configuração para títulos
                    run.RunProperties = new Word.RunProperties
                    {
                        FontSize = new Word.FontSize { Val = node.Name == "h1" ? "36" : node.Name == "h2" ? "30" : "26" },
                        Bold = new Word.Bold(),
                        Color = new Word.Color { Val = "2F5597" },
                        RunFonts = new Word.RunFonts { Ascii = "Calibri" }
                    };

                    run.AppendChild(new Word.Text(node.InnerText));
                    paragrafo.AppendChild(run);
                    
                    paragrafo.ParagraphProperties = new Word.ParagraphProperties
                    {
                        SpacingBetweenLines = new Word.SpacingBetweenLines
                        {
                            Before = "400",
                            After = "200",
                            Line = "276",
                            LineRule = Word.LineSpacingRuleValues.Auto
                        }
                    };

                    body.AppendChild(paragrafo);
                }
            }
        }

        private void ProcessarNodoHtml(HtmlNode node, Word.Paragraph paragrafo)
        {
            foreach (var childNode in node.ChildNodes)
            {
                if (childNode.NodeType == HtmlNodeType.Text)
                {
                    var texto = childNode.InnerText;
                    if (!string.IsNullOrWhiteSpace(texto))
                    {
                        bool temEmoji = texto.Contains("⏳") || texto.Contains("✅") || texto.Contains("⚠️") || texto.Contains("❌");
                        var run = CriarRunComPropriedades(texto, false, temEmoji);
                        paragrafo.AppendChild(run);
                    }
                }
                else if (childNode.Name == "strong" || childNode.Name == "b")
                {
                    var run = CriarRunComPropriedades(childNode.InnerText, true);
                    paragrafo.AppendChild(run);
                }
                else if (childNode.Name == "em" || childNode.Name == "i")
                {
                    var run = CriarRunComPropriedades(childNode.InnerText, false, false, true);
                    paragrafo.AppendChild(run);
                }
                else if (childNode.Name == "code")
                {
                    var run = CriarRunComPropriedades(childNode.InnerText, false, false, false, true);
                    paragrafo.AppendChild(run);
                }
                else
                {
                    ProcessarNodoHtml(childNode, paragrafo);
                }
            }
        }

        private Word.Run CriarRunComPropriedades(string texto, bool negrito = false, bool emoji = false, bool italico = false, bool codigo = false)
        {
            var run = new Word.Run();
            var runProperties = new Word.RunProperties();

            if (emoji)
            {
                runProperties.FontSize = new Word.FontSize { Val = "24" };
                runProperties.Color = new Word.Color { Val = "2F5597" };
                runProperties.RunFonts = new Word.RunFonts { Ascii = "Segoe UI Emoji" };
            }
            else
            {
                runProperties.FontSize = new Word.FontSize { Val = "22" };
                runProperties.RunFonts = new Word.RunFonts { Ascii = codigo ? "Consolas" : "Calibri" };

                if (negrito)
                    runProperties.Bold = new Word.Bold();

                if (italico)
                    runProperties.Italic = new Word.Italic();

                if (codigo)
                {
                    runProperties.Color = new Word.Color { Val = "7F7F7F" };
                    runProperties.Shading = new Word.Shading { Fill = "F2F2F2" };
                }
            }

            run.RunProperties = runProperties;
            run.AppendChild(new Word.Text(texto));

            return run;
        }

        private Word.Paragraph CriarParagrafoSimples(string texto)
        {
            return new Word.Paragraph(
                new Word.Run(
                    new Word.RunProperties(
                        new Word.FontSize { Val = "22" },
                        new Word.RunFonts { Ascii = "Calibri" }
                    ),
                    new Word.Text(texto)
                )
            );
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
