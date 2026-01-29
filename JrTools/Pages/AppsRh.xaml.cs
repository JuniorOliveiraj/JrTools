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
            LoadSettingsItems();
        }

        private void LoadSettingsItems()
        {
            // Lista de opções de sistema
            var items = new List<SystemSettingItem>
            {
                new("Projetos Rh", "Altere a versão do sistema de forma rápida.", "\uE912", typeof(RhProdPage)),
                new("Cria", "Ta Afim mesmo de levar uns esculachos?", "\uE99A", typeof(Cria)),
                new("Cria Fix", "Corrigir a formatação UTF8-BOM dos arquivs", "\uE99A", typeof(CriaFix)),
                new("ambiente específico", "Subir ambiente específico completo.", "\uE716", typeof(EspesificosPage)),
            };

            // Limpa o painel e adiciona dinamicamente
            SettingsItemsPanel.Children.Clear();

            var style = (Style)this.Resources["SettingItemStyle"];

            foreach (var item in items)
            {
                var btn = new Button
                {
                    Content = item.Title,
                    AccessKey = item.Description,
                    Tag = item.Icon,
                    Style = style,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                btn.Padding = new Thickness(24, 18, 24, 18); // mais "acolchoamento" interno
                btn.Margin = new Thickness(0, 18, 0, 0);     // mais espaço entre os itens

                btn.Click += (s, e) => OnSettingItemClick(item);
                
                SettingsItemsPanel.Children.Add(btn);

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
