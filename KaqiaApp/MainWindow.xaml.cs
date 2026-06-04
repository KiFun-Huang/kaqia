using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using KaqiaCore;

namespace KaqiaApp
{
    public partial class MainWindow : Window
    {
        private Point _startPoint;
        private bool _isSelecting;
        private Rect _currentSelection;
        private BitmapSource _fullScreenshot;
        private double _scaleX, _scaleY;
        private StickerControl? _selectedSticker;
        private AppConfig _config;
        
        public class StickerItem
        {
            public string FilePath { get; set; } = "";
            public BitmapImage Image { get; set; } = new BitmapImage();
        }

        public ObservableCollection<StickerItem> StickerLibrary { get; set; } = new ObservableCollection<StickerItem>();

        public MainWindow(BitmapSource screenshot, int pixelWidth, int pixelHeight)
        {
            InitializeComponent();
            _fullScreenshot = screenshot;
            _config = ConfigManager.Load();

            _scaleX = pixelWidth / SystemParameters.VirtualScreenWidth;
            _scaleY = pixelHeight / SystemParameters.VirtualScreenHeight;

            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            
            FullRect.Rect = new Rect(0, 0, Width, Height);
            BackgroundImg.Source = screenshot;
            BackgroundImg.Width = Width;
            BackgroundImg.Height = Height;

            LoadStickers();
            StickerItemsControl.ItemsSource = StickerLibrary;

            KeyDown += (s, e) => {
                if (e.Key == Key.Escape) Close();
                if (e.Key == Key.Delete && _selectedSticker != null)
                {
                    ImageContainer.Children.Remove(_selectedSticker);
                    _selectedSticker = null;
                }
            };
        }

        private void LoadStickers()
        {
            StickerLibrary.Clear();
            var stickers = StickerManager.GetStickers();
            foreach (var path in stickers) 
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                StickerLibrary.Add(new StickerItem { FilePath = path, Image = bitmap });
            }
        }

        private void DeselectAllStickers()
        {
            foreach (var child in ImageContainer.Children)
            {
                if (child is StickerControl sc) sc.SetSelected(false);
            }
            _selectedSticker = null;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (EditCanvas.Visibility == Visibility.Visible)
            {
                if (e.OriginalSource is Canvas || e.OriginalSource is Image)
                    DeselectAllStickers();
                return;
            }
            _isSelecting = true;
            _startPoint = e.GetPosition(MainCanvas);
            SelectionBorder.Visibility = Visibility.Visible;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;
            var currentPoint = e.GetPosition(MainCanvas);
            _currentSelection = new Rect(_startPoint, currentPoint);
            Canvas.SetLeft(SelectionBorder, _currentSelection.Left);
            Canvas.SetTop(SelectionBorder, _currentSelection.Top);
            SelectionBorder.Width = _currentSelection.Width;
            SelectionBorder.Height = _currentSelection.Height;
            SelectionRect.Rect = _currentSelection;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            _isSelecting = false;
            if (_currentSelection.Width > 10 && _currentSelection.Height > 10)
                EnterEditMode();
        }

        private void EnterEditMode()
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
            SelectionRect.Rect = _currentSelection;

            try 
            {
                var croppedBitmap = new CroppedBitmap(_fullScreenshot, new Int32Rect(
                    (int)(_currentSelection.X * _scaleX), (int)(_currentSelection.Y * _scaleY), 
                    (int)(_currentSelection.Width * _scaleX), (int)(_currentSelection.Height * _scaleY)));
                
                CroppedImage.Source = croppedBitmap;
                CroppedImage.Width = _currentSelection.Width;
                CroppedImage.Height = _currentSelection.Height;

                ImageContainer.Width = _currentSelection.Width;
                ImageContainer.Height = _currentSelection.Height;

                Canvas.SetLeft(EditCanvas, _currentSelection.Left);
                Canvas.SetTop(EditCanvas, _currentSelection.Top);
                EditCanvas.Visibility = Visibility.Visible;
                
                Toolbar.Visibility = Visibility.Visible;
                UpdateBeautifyEffects();
            }
            catch (Exception ex) { MessageBox.Show("预览失败: " + ex.Message); Close(); }
        }

        private void UpdateBeautifyEffects()
        {
            if (EditCanvas.Visibility != Visibility.Visible) return;

            double radius = RadiusSlider.Value;
            double stroke = StrokeSlider.Value;
            double padding = PaddingSlider.Value;

            ImageClipBorder.CornerRadius = new CornerRadius(radius);
            StrokeLayer.CornerRadius = new CornerRadius(radius);
            StrokeLayer.BorderThickness = new Thickness(stroke);

            CanvasLayer.Padding = new Thickness(padding);

            if (ShadowCheck.IsChecked == true)
                ShadowLayer.Effect = new DropShadowEffect { BlurRadius = 40, Opacity = 0.5, ShadowDepth = 15, Direction = 270, RenderingBias = RenderingBias.Quality };
            else
                ShadowLayer.Effect = null;

            UpdateToolbarPosition();
        }

        private void UpdateToolbarPosition()
        {
            double extraBottom = CanvasLayer.Padding.Bottom + (ShadowCheck.IsChecked == true ? 35 : 0);
            Toolbar.Margin = new Thickness(
                _currentSelection.Left + Math.Max(0, _currentSelection.Width - 320), 
                _currentSelection.Bottom + 20 + extraBottom, 0, 0);
        }

        private void OnBeautifyToggle(object sender, RoutedEventArgs e) => BeautifyPopup.IsOpen = !BeautifyPopup.IsOpen;

        private void OnBeautifyParamChanged(object sender, RoutedEventArgs e) => UpdateBeautifyEffects();

        private void OnShadowChanged(object sender, RoutedEventArgs e) => UpdateBeautifyEffects();

        private void OnRadiusChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateBeautifyEffects();
        private void OnStrokeChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateBeautifyEffects();
        private void OnPaddingChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateBeautifyEffects();

        private void OnStrokeColorSelected(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Background != null) StrokeLayer.BorderBrush = b.Background;
        }

        private void OnCanvasColorSelected(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag != null)
            {
                if (b.Tag.ToString() == "Transparent") CanvasLayer.Background = Brushes.Transparent;
                else CanvasLayer.Background = b.Background;
            }
        }

        private void OnStickerPickerToggle(object sender, RoutedEventArgs e) => StickerPickerPopup.IsOpen = !StickerPickerPopup.IsOpen;

        private void OnUploadStickerClick(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp" };
            if (ofd.ShowDialog() == true) { StickerManager.AddSticker(ofd.FileName); LoadStickers(); }
        }

        private void OnStickerSelected(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is StickerItem item)
            {
                var s = new StickerControl();
                s.StickerImage.Source = item.Image;
                s.DeleteRequested += (src, ev) => ImageContainer.Children.Remove(s);
                s.Selected += (src, ev) => { DeselectAllStickers(); s.SetSelected(true); _selectedSticker = s; };
                ImageContainer.Children.Add(s);
                Canvas.SetLeft(s, (_currentSelection.Width - s.Width) / 2);
                Canvas.SetTop(s, (_currentSelection.Height - s.Height) / 2);
                s.SetSelected(true); _selectedSticker = s;
                StickerPickerPopup.IsOpen = false;
            }
        }

        private void OnDeleteStickerFromLibrary(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is StickerItem item) { StickerManager.DeleteSticker(item.FilePath); StickerLibrary.Remove(item); }
        }

        private void OnFinishClick(object sender, RoutedEventArgs e)
        {
            DeselectAllStickers();
            ShadowLayer.UpdateLayout();
            var source = PresentationSource.FromVisual(this);
            double dx = 96.0 * (source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0);
            double dy = 96.0 * (source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0);
            var rb = new RenderTargetBitmap((int)Math.Round(ShadowLayer.ActualWidth * (dx / 96.0)), (int)Math.Round(ShadowLayer.ActualHeight * (dy / 96.0)), dx, dy, PixelFormats.Pbgra32);
            rb.Render(ShadowLayer);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rb));
            using (var s = new MemoryStream()) 
            { 
                enc.Save(s); 
                var data = new DataObject(); data.SetImage(rb); data.SetData("PNG", s, false); Clipboard.SetDataObject(data, true); 

                if (!string.IsNullOrEmpty(_config.DefaultSavePath) && Directory.Exists(_config.DefaultSavePath))
                {
                    try {
                        string fn = $"Kaqia_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                        string path = Path.Combine(_config.DefaultSavePath, fn);
                        using (var fs = File.OpenWrite(path)) { s.Position = 0; s.CopyTo(fs); }
                    } catch { }
                }
            }
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
    }
}
