using JrTools.Pages.Apps;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace JrTools.Pages
{
    public sealed partial class AppsRh : Page
    {
        public AppsRh()
        {
            this.InitializeComponent();
            NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            LoadSettingsItems();
        }

        private void LoadSettingsItems()
        {
            // Lista de op��es de sistema
            var items = new List<SystemSettingItem>
            {
                new("Projetos RH", "Altere a versão do sistema de forma rápida.", "\uE912", typeof(RhProdPage)),
                new("Cria", "Ta afim mesmo de levar uns esculachos?", "\uE99A", typeof(Cria)),
                new("Cria Fix", "Corrigir a formatação UTF8-BOM dos arquivos", "\uE99A", typeof(CriaFix)),
                new("Ambiente Específico", "Subir ambiente específico completo.", "\uE716", typeof(EspecificosPage)),
                new("Automações - WES", "Instalar artefatos e gerar páginas via wes.exe.", "\uE896", typeof(InstalarArtefatosPage)),
                new("Importador de Relatórios", "Importar relatórios .rpt para o servidor Benner.", "\uE8A5", typeof(ImportadorRelatoriosPage)),
                new("Subir Ambiente Manual", "Sobe o ambiente manualmente, passo a passo.", "\uE8B7", typeof(SubirAmbienteManualPage)),
                new("Criar Aplicação IIS", "Cria uma nova aplicação no IIS com pool existente.", "\uE774", typeof(CriarAplicacaoIisPage)),
                new("View Path Explorer", "Encontrar caminhos de navegação para views em páginas WES.", "\uE721", typeof(ViewPathExplorerPage)),
            };

            // Limpa o painel e adiciona dinamicamente
            SettingsItemsPanel.Children.Clear();

            var style   = (Style)this.Resources["SettingItemStyle"];
            int count   = items.Count;
            double r    = 8; // deve bater com o CornerRadius do FancyCardStyle

            for (int i = 0; i < count; i++)
            {
                var item = items[i];

                // Corner radius: arredonda só onde o item toca a borda do card
                var cornerRadius = (i == 0 && i == count - 1) ? new CornerRadius(r)           // único
                                 : (i == 0)                   ? new CornerRadius(r, r, 0, 0)  // primeiro
                                 : (i == count - 1)           ? new CornerRadius(0, 0, r, r)  // último
                                                               : new CornerRadius(0);          // meio

                var btn = new Button
                {
                    Content        = item.Title,
                    AccessKey      = item.Description,
                    Tag            = item.Icon,
                    Style          = style,
                    CornerRadius   = cornerRadius,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                btn.Click += (s, e) => OnSettingItemClick(item);
                SettingsItemsPanel.Children.Add(btn);

                // Divisor fino entre itens (não após o último)
                if (i < count - 1)
                {
                    SettingsItemsPanel.Children.Add(new Border
                    {
                        Height     = 1,
                        Margin     = new Thickness(20, 0, 20, 0),
                        Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"]
                    });
                }
            }
        }

        private async void OnSettingItemClick(SystemSettingItem item)
        {
            if (item.TargetPageType != null)
            {
                this.Frame?.Navigate(item.TargetPageType);
            }
            else
            {
                ContentDialog dialog = new()
                {
                    Title = item.Title,
                    Content = "Página ainda não implementada.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }
    }

    public class SystemSettingItem
    {
        public string Title { get; }
        public string Description { get; }
        public string Icon { get; }
        public Type? TargetPageType { get; }

        public SystemSettingItem(string title, string description, string icon, Type? targetPageType)
        {
            Title = title;
            Description = description;
            Icon = icon;
            TargetPageType = targetPageType;
        }
    }
}
