﻿using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HocrEditor.Services;
using HocrEditor.ViewModels;
using HtmlAgilityPack;
using Microsoft.Win32;

namespace HocrEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainWindowViewModel();
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            var tesseractPath = GetTesseractPath();

            if (tesseractPath == null)
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Pick Images",
                Filter =
                    "Image files (*.bmp;*.gif;*.tif;*.tiff;*.tga;*.jpg;*.jpeg;*.png)|*.bmp;*.gif;*.tif;*.tiff;*.tga;*.jpg;*.jpeg;*.png"
            };

            if (dialog.ShowDialog(this) == true)
            {
                Task.Run(
                        async () =>
                        {
                            var imagePath = dialog.FileName;

                            var service = new TesseractService(tesseractPath);

                            var body = await service.PerformOcr(imagePath, new[] { "script/Hebrew", "eng" });

                            var doc = new HtmlDocument();
                            doc.LoadHtml(body);

                            return HocrDocumentParser.Parse(doc);
                        }
                    )
                    .ContinueWith(
                        async hocr =>
                        {
                            var hocrDocument = await hocr;

                            if (DataContext is MainWindowViewModel mainWindowViewModel && hocrDocument != null)
                            {
                                mainWindowViewModel.Document = new HocrDocumentViewModel(hocrDocument);
                            }
                        },
                        TaskScheduler.FromCurrentSynchronizationContext()
                    );
            }
        }

        private string? GetTesseractPath()
        {
            var tesseractPath = Settings.TesseractPath;

            if (tesseractPath != null)
            {
                return tesseractPath;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Locate tesseract.exe...",
                Filter = "Executables (*.exe)|*.exe"
            };

            if (!(dialog.ShowDialog(this) ?? false))
            {
                return null;
            }

            tesseractPath = dialog.FileName;

            Settings.TesseractPath = tesseractPath;

            return tesseractPath;
        }

        private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedNodes = ViewModel.Document?.SelectedNodes;

            if (e.NewValue is not HocrNodeViewModel node)
            {
                return;
            }

            selectedNodes?.Clear();
            selectedNodes?.Add(node);
        }
    }
}
