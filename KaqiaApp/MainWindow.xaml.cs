using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

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

        public MainWindow(BitmapSource screenshot, int pixelWidth, int pixelHeight)
        {
            InitializeComponent();
            _fullScreenshot = screenshot;
            
            // Set window bounds in DIPs (logical units)
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            
            // Calculate scale between pixels and DIPs
            _scaleX = pixelWidth / Width;
            _scaleY = pixelHeight / Height;

            FullRect.Rect = new Rect(0, 0, Width, Height);
            
            // Fix: Set Bitmap scaling to ensure it looks sharp and matches 1:1 logical pixels
            BackgroundImg.Source = screenshot;
            BackgroundImg.Width = Width;
            BackgroundImg.Height = Height;
            // Ensure no automatic stretching beyond logical units
            RenderOptions.SetBitmapScalingMode(BackgroundImg, BitmapScalingMode.NearestNeighbor);

            KeyDown += (s, e) => {
                if (e.Key == Key.Escape) Close();
                if (e.Key == Key.Delete && _selectedSticker != null)
                {
                    ImageContainer.Children.Remove(_selectedSticker);
                    _selectedSticker = null;
                }
            };

            MainCanvas.MouseDown += (s, e) => {
                if (EditCanvas.Visibility == Visibility.Visible)
                {
                    DeselectAllStickers();
                }
            };
        }

        private void DeselectAllStickers()
        {
            foreach (var child in ImageContainer.Children)
            {
                if (child is StickerControl sc)
                {
                    sc.SetSelected(false);
                }
            }
            _selectedSticker = null;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (EditCanvas.Visibility == Visibility.Visible) return;
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
            {
                EnterEditMode();
            }
        }

        private void EnterEditMode()
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
            SelectionRect.Rect = _currentSelection;

            try 
            {
                // Fix: Accurate physical pixel crop and sync dimensions
                var croppedBitmap = new CroppedBitmap(_fullScreenshot, new Int32Rect(
                    (int)(_currentSelection.X * _scaleX), 
                    (int)(_currentSelection.Y * _scaleY), 
                    (int)(_currentSelection.Width * _scaleX), 
                    (int)(_currentSelection.Height * _scaleY)));
                
                CroppedImage.Source = croppedBitmap;
                // Force size to match logical selection (remove the zoom effect)
                CroppedImage.Width = _currentSelection.Width;
                CroppedImage.Height = _currentSelection.Height;
                CroppedImage.Stretch = Stretch.Fill;
                RenderOptions.SetBitmapScalingMode(CroppedImage, BitmapScalingMode.NearestNeighbor);

                ImageContainer.Width = _currentSelection.Width;
                ImageContainer.Height = _currentSelection.Height;

                Canvas.SetLeft(EditCanvas, _currentSelection.Left);
                Canvas.SetTop(EditCanvas, _currentSelection.Top);
                EditCanvas.Visibility = Visibility.Visible;
                
                Toolbar.Visibility = Visibility.Visible;
                UpdateToolbarPosition();
            }
            catch (Exception ex)
            {
                MessageBox.Show("预览失败: " + ex.Message);
                Close();
            }
        }

        private void OnNoBorderClick(object sender, RoutedEventArgs e)
        {
            ImageBorder.Padding = new Thickness(0);
            ImageBorder.Effect = null;
            ImageBorder.Background = Brushes.Transparent;
            UpdateToolbarPosition();
        }

        private void OnPolaroidClick(object sender, RoutedEventArgs e)
        {
            ImageBorder.Background = Brushes.White;
            ImageBorder.Padding = new Thickness(15, 15, 15, 45);
            ImageBorder.Effect = new DropShadowEffect { BlurRadius = 25, Opacity = 0.4, ShadowDepth = 10, Direction = 270 };
            UpdateToolbarPosition();
        }

        private void OnShadowClick(object sender, RoutedEventArgs e)
        {
            ImageBorder.Background = Brushes.Transparent;
            ImageBorder.Padding = new Thickness(10);
            ImageBorder.Effect = new DropShadowEffect { BlurRadius = 60, Opacity = 0.7, ShadowDepth = 30, Direction = 270 };
            UpdateToolbarPosition();
        }

        private void UpdateToolbarPosition()
        {
            double extraBottom = ImageBorder.Padding.Bottom;
            Toolbar.Margin = new Thickness(
                    _currentSelection.Left + Math.Max(0, _currentSelection.Width - 450), 
                    _currentSelection.Bottom + 15 + extraBottom, 
                    0, 0);
        }

        private void OnAddStickerClick(object sender, RoutedEventArgs e)
        {
            var sticker = new StickerControl();
            
            sticker.DeleteRequested += (s, ev) => {
                ImageContainer.Children.Remove(sticker);
                if (_selectedSticker == sticker) _selectedSticker = null;
            };

            sticker.Selected += (s, ev) => {
                DeselectAllStickers();
                _selectedSticker = sticker;
            };

            ImageContainer.Children.Add(sticker);
            
            Canvas.SetLeft(sticker, (_currentSelection.Width - sticker.Width) / 2);
            Canvas.SetTop(sticker, (_currentSelection.Height - sticker.Height) / 2);
            
            sticker.SetSelected(true);
            _selectedSticker = sticker;
        }

        private void OnFinishClick(object sender, RoutedEventArgs e)
        {
            DeselectAllStickers();
            ImageBorder.UpdateLayout();
            
            var renderBitmap = new RenderTargetBitmap(
                (int)ImageBorder.ActualWidth, (int)ImageBorder.ActualHeight, 
                96, 96, PixelFormats.Pbgra32);
            
            renderBitmap.Render(ImageBorder);

            Clipboard.SetImage(renderBitmap);
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
