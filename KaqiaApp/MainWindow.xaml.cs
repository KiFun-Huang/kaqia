using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using KaqiaCore;

namespace KaqiaApp
{
    public enum DrawingMode { Select, Rectangle, Ellipse, Arrow, Pen, Text }

    public partial class MainWindow : Window
    {
        private Point _startPoint;
        private bool _isSelecting;
        private Rect _currentSelection;
        private BitmapSource _fullScreenshot;
        private double _scaleX, _scaleY;
        private KaqiaObjectControl? _selectedObject;
        private AppConfig _config;
        private bool _isInitialized = false;

        private DrawingMode _currentMode = DrawingMode.Select;
        private Shape? _activeShape;
        private Polyline? _activePolyline;
        
        public class StickerItem { public string FilePath { get; set; } = ""; public BitmapImage Image { get; set; } = new BitmapImage(); }
        public ObservableCollection<StickerItem> StickerLibrary { get; set; } = new ObservableCollection<StickerItem>();

        public MainWindow(BitmapSource screenshot, int pixelWidth, int pixelHeight)
        {
            try {
                InitializeComponent();
                _fullScreenshot = screenshot;
                _config = ConfigManager.Load();
                
                _scaleX = (double)pixelWidth / SystemParameters.VirtualScreenWidth;
                _scaleY = (double)pixelHeight / SystemParameters.VirtualScreenHeight;
                Left = SystemParameters.VirtualScreenLeft; Top = SystemParameters.VirtualScreenTop; 
                Width = SystemParameters.VirtualScreenWidth; Height = SystemParameters.VirtualScreenHeight;
                
                FullRect.Rect = new Rect(0, 0, Width, Height);
                BackgroundImg.Source = screenshot; BackgroundImg.Width = Width; BackgroundImg.Height = Height;
                
                LoadStickers(); 
                StickerItemsControl.ItemsSource = StickerLibrary;

                _isInitialized = true;
                ApplyConfigToUI();

                KeyDown += (s, e) => {
                    if (e.Key == Key.Escape) Close();
                    if (e.Key == Key.Delete && _selectedObject != null) { ImageContainer.Children.Remove(_selectedObject); _selectedObject = null; ToolPropertyPopup.IsOpen = false; }
                };
                AnnotationCanvas.MouseDown += OnAnnotationCanvasMouseDown;
                AnnotationCanvas.MouseMove += OnAnnotationCanvasMouseMove;
                AnnotationCanvas.MouseUp += OnAnnotationCanvasMouseUp;
            } catch (Exception ex) {
                MessageBox.Show("MainWindow 初始化失败: " + ex.Message);
                Close();
            }
        }

        private void ApplyConfigToUI()
        {
            if (!_isInitialized) return;
            RadiusSlider.Value = _config.Radius;
            StrokeSlider.Value = _config.StrokeThickness;
            PaddingSlider.Value = _config.Padding;
            ShadowCheck.IsChecked = _config.ShadowEnabled;
            try {
                if (!string.IsNullOrEmpty(_config.StrokeColor))
                    StrokeLayer.BorderBrush = (Brush)new BrushConverter().ConvertFromString(_config.StrokeColor);
                if (!string.IsNullOrEmpty(_config.CanvasColor)) {
                    CanvasLayer.Background = (Brush)new BrushConverter().ConvertFromString(_config.CanvasColor);
                    CanvasHexInput.Text = _config.CanvasColor;
                }
            } catch { }
        }

        private void SaveCurrentConfig()
        {
            if (!_isInitialized || _config == null) return;
            _config.Radius = RadiusSlider.Value;
            _config.StrokeThickness = StrokeSlider.Value;
            _config.Padding = PaddingSlider.Value;
            _config.ShadowEnabled = ShadowCheck.IsChecked == true;
            if (StrokeLayer.BorderBrush != null) _config.StrokeColor = StrokeLayer.BorderBrush.ToString();
            if (CanvasLayer.Background != null) _config.CanvasColor = CanvasLayer.Background.ToString();
            ConfigManager.Save(_config);
        }

        private void LoadStickers() {
            StickerLibrary.Clear();
            foreach (var path in StickerManager.GetStickers()) {
                try {
                    var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.UriSource = new Uri(path); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.EndInit(); bitmap.Freeze();
                    StickerLibrary.Add(new StickerItem { FilePath = path, Image = bitmap });
                } catch { }
            }
        }

        private void DeselectAll(bool closePopup = true) {
            foreach (var child in ImageContainer.Children) if (child is KaqiaObjectControl oc) oc.SetSelected(false);
            _selectedObject = null;
            if (closePopup) ToolPropertyPopup.IsOpen = false;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (EditCanvas.Visibility == Visibility.Visible) {
                if (e.OriginalSource == MainCanvas || e.OriginalSource == BackgroundImg) DeselectAll();
                return;
            }
            _isSelecting = true; _startPoint = e.GetPosition(MainCanvas); SelectionBorder.Visibility = Visibility.Visible;
        }

        private void OnMouseMove(object sender, MouseEventArgs e) {
            if (!_isSelecting) return;
            _currentSelection = new Rect(_startPoint, e.GetPosition(MainCanvas));
            Canvas.SetLeft(SelectionBorder, _currentSelection.Left); Canvas.SetTop(SelectionBorder, _currentSelection.Top);
            SelectionBorder.Width = _currentSelection.Width; SelectionBorder.Height = _currentSelection.Height; SelectionRect.Rect = _currentSelection;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e) {
            if (!_isSelecting) return; _isSelecting = false;
            if (_currentSelection.Width > 10 && _currentSelection.Height > 10) EnterEditMode();
        }

        private void EnterEditMode() {
            SelectionBorder.Visibility = Visibility.Collapsed; SelectionRect.Rect = _currentSelection;
            try {
                var croppedBitmap = new CroppedBitmap(_fullScreenshot, new Int32Rect((int)(_currentSelection.X * _scaleX), (int)(_currentSelection.Y * _scaleY), (int)(_currentSelection.Width * _scaleX), (int)(_currentSelection.Height * _scaleY)));
                CroppedImage.Source = croppedBitmap; CroppedImage.Width = _currentSelection.Width; CroppedImage.Height = _currentSelection.Height;
                ImageContainer.Width = _currentSelection.Width; ImageContainer.Height = _currentSelection.Height;
                AnnotationCanvas.Width = _currentSelection.Width; AnnotationCanvas.Height = _currentSelection.Height;
                Canvas.SetLeft(EditCanvas, _currentSelection.Left); Canvas.SetTop(EditCanvas, _currentSelection.Top);
                EditCanvas.Visibility = Visibility.Visible; Toolbar.Visibility = Visibility.Visible; UpdateBeautifyEffects();
            } catch (Exception ex) { MessageBox.Show("预览失败: " + ex.Message); Close(); }
        }

        // --- TOOL PROPERTY LOGIC ---

        private void OnToolSelected(object sender, RoutedEventArgs e) {
            if (!_isInitialized) return;
            if (sender is RadioButton rb && rb.Tag != null) {
                _currentMode = (DrawingMode)Enum.Parse(typeof(DrawingMode), rb.Tag.ToString());
                if (_currentMode != DrawingMode.Select) {
                    DeselectAll();
                    ShowToolPropertyPopup(_currentMode);
                } else {
                    ToolPropertyPopup.IsOpen = false;
                }
            }
        }

        private void ShowToolPropertyPopup(DrawingMode mode, KaqiaObjectControl? target = null) {
            ToolPropertyPopup.PlacementTarget = Toolbar;
            ToolPropertyTitle.Text = mode.ToString() + " 属性";

            if (_config.Tools.TryGetValue(mode.ToString(), out var state)) {
                if (target != null) {
                    ToolThicknessSlider.Value = target.ObjectThickness;
                    ToolHexInput.Text = target.ObjectBrush.ToString();
                } else {
                    ToolThicknessSlider.Value = state.Thickness;
                    ToolHexInput.Text = state.Color;
                }
            }
            ToolPropertyPopup.IsOpen = true;
        }

        private void OnToolParamChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (!_isInitialized) return;
            if (_selectedObject != null) _selectedObject.ObjectThickness = e.NewValue;
            else if (_currentMode != DrawingMode.Select) { _config.Tools[_currentMode.ToString()].Thickness = e.NewValue; SaveCurrentConfig(); }
        }

        private void OnToolColorSelected(object sender, MouseButtonEventArgs e) {
            if (sender is Border b && b.Background != null) {
                ApplyToolColor(b.Background);
                ToolHexInput.Text = b.Background.ToString();
            }
        }

        private void OnCustomColorClick(object sender, MouseButtonEventArgs e) {
            try {
                var brush = (Brush)new BrushConverter().ConvertFromString(_config.LastCustomColor);
                ApplyToolColor(brush);
                ToolHexInput.Text = _config.LastCustomColor;
            } catch { }
            ToolHexInput.Focus();
            ToolHexInput.SelectAll();
        }

        private void OnToolHexKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                try {
                    var brush = (Brush)new BrushConverter().ConvertFromString(ToolHexInput.Text);
                    _config.LastCustomColor = ToolHexInput.Text;
                    SaveCurrentConfig();
                    ApplyToolColor(brush);
                } catch { }
            }
        }

        private void ApplyToolColor(Brush brush) {
            if (_selectedObject != null) _selectedObject.ObjectBrush = brush;
            else if (_currentMode != DrawingMode.Select) { _config.Tools[_currentMode.ToString()].Color = brush.ToString(); SaveCurrentConfig(); }
        }

        private void OnCanvasHexKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                try {
                    var brush = (Brush)new BrushConverter().ConvertFromString(CanvasHexInput.Text);
                    CanvasLayer.Background = brush;
                    SaveCurrentConfig();
                } catch { }
            }
        }

        // --- DRAWING LOGIC ---

        private void OnAnnotationCanvasMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.OriginalSource == AnnotationCanvas) { 
                DeselectAll(); 
                Keyboard.ClearFocus();
                AnnotationCanvas.Focus();
                if (_currentMode == DrawingMode.Select) return;
            } else return;

            Point p = e.GetPosition(AnnotationCanvas); _startPoint = p;
            if (!_config.Tools.TryGetValue(_currentMode.ToString(), out var state)) return;
            Brush brush = (Brush)new BrushConverter().ConvertFromString(state.Color);
            double thickness = state.Thickness;

            switch (_currentMode) {
                case DrawingMode.Rectangle: _activeShape = new Rectangle { Stroke = brush, StrokeThickness = thickness }; AnnotationCanvas.Children.Add(_activeShape); break;
                case DrawingMode.Ellipse: _activeShape = new Ellipse { Stroke = brush, StrokeThickness = thickness }; AnnotationCanvas.Children.Add(_activeShape); break;
                case DrawingMode.Arrow: _activeShape = CreateArrowShapeInternal(); AnnotationCanvas.Children.Add(_activeShape); break;
                case DrawingMode.Pen: _activePolyline = new Polyline { Stroke = brush, StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round }; _activePolyline.Points.Add(p); AnnotationCanvas.Children.Add(_activePolyline); break;
                case DrawingMode.Text: CreateInteractiveText(p); SelectToolBtn.IsChecked = true; _currentMode = DrawingMode.Select; return;
            }
            AnnotationCanvas.CaptureMouse();
        }

        private void OnAnnotationCanvasMouseMove(object sender, MouseEventArgs e) {
            if (_activeShape == null && _activePolyline == null) return;
            Point p = e.GetPosition(AnnotationCanvas);
            if (_activeShape != null) {
                if (_activeShape is System.Windows.Shapes.Path arrow) UpdateArrowShapeInternal(arrow, _startPoint, p);
                else {
                    double left = Math.Min(p.X, _startPoint.X); double top = Math.Min(p.Y, _startPoint.Y);
                    double w = Math.Max(1, Math.Abs(p.X - _startPoint.X)); double h = Math.Max(1, Math.Abs(p.Y - _startPoint.Y));
                    Canvas.SetLeft(_activeShape, left); Canvas.SetTop(_activeShape, top); _activeShape.Width = w; _activeShape.Height = h;
                }
            } else if (_activePolyline != null) _activePolyline.Points.Add(p);
        }

        private void OnAnnotationCanvasMouseUp(object sender, MouseButtonEventArgs e) {
            if (_activeShape != null || _activePolyline != null) {
                FrameworkElement? content = (FrameworkElement?)_activeShape ?? _activePolyline;
                if (content != null) {
                    double x, y, w, h;
                    if (content is System.Windows.Shapes.Path path) {
                        Rect bounds = path.Data.Bounds;
                        x = bounds.Left; y = bounds.Top; w = Math.Max(10, bounds.Width); h = Math.Max(10, bounds.Height);
                        // Store the original points to allow re-drawing
                        if (path.Tag is Point[] pts) {
                            path.Stretch = Stretch.Fill;
                        }
                    } else if (content is Polyline pl) {
                        Rect bounds = GetPolylineBounds(pl);
                        x = bounds.Left; y = bounds.Top; w = Math.Max(10, bounds.Width); h = Math.Max(10, bounds.Height);
                        for(int i=0; i<pl.Points.Count; i++) pl.Points[i] = new Point(pl.Points[i].X - x, pl.Points[i].Y - y);
                    } else { x = Canvas.GetLeft(content); y = Canvas.GetTop(content); w = content.Width; h = content.Height; }
                    AnnotationCanvas.Children.Remove(content); WrapObject(content, x, y, w, h);
                }
                _activeShape = null; _activePolyline = null; AnnotationCanvas.ReleaseMouseCapture();
                SelectToolBtn.IsChecked = true; _currentMode = DrawingMode.Select;
            }
        }

        private Rect GetPolylineBounds(Polyline pl) {
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var p in pl.Points) { minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); }
            return new Rect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
        }

        private System.Windows.Shapes.Path CreateArrowShapeInternal() { 
            if (!_config.Tools.TryGetValue("Arrow", out var state)) return new System.Windows.Shapes.Path();
            Brush brush = (Brush)new BrushConverter().ConvertFromString(state.Color);
            return new System.Windows.Shapes.Path { Stroke = brush, StrokeThickness = state.Thickness, Fill = brush, StrokeLineJoin = PenLineJoin.Round }; 
        }

        private void UpdateArrowShapeInternal(System.Windows.Shapes.Path? path, Point start, Point end) {
            if (path == null) return;
            path.Tag = new Point[] { start, end }; // Save for reference
            Vector dir = end - start; if (dir.Length < 5) return;
            dir.Normalize(); Vector perp = new Vector(-dir.Y, dir.X);
            Point p1 = end - dir * 15 + perp * 7; Point p2 = end - dir * 15 - perp * 7;
            var geometry = new PathGeometry();
            var fig = new PathFigure { StartPoint = start, IsClosed = false }; fig.Segments.Add(new LineSegment(end, true)); geometry.Figures.Add(fig);
            var head = new PathFigure { StartPoint = end, IsClosed = true, IsFilled = true }; head.Segments.Add(new LineSegment(p1, true)); head.Segments.Add(new LineSegment(p2, true)); geometry.Figures.Add(head);
            path.Data = geometry;
        }

        private void CreateInteractiveText(Point p) {
            if (!_config.Tools.TryGetValue("Text", out var state)) return;
            Brush brush = (Brush)new BrushConverter().ConvertFromString(state.Color);
            var tb = new TextBox { 
                Background = Brushes.Transparent, 
                Foreground = brush, 
                FontSize = 20, 
                FontWeight = FontWeights.Bold, 
                BorderThickness = new Thickness(1), 
                BorderBrush = Brushes.Transparent, 
                MinWidth = 80, 
                AcceptsReturn = true, 
                Text = "输入文字..." 
            };
            tb.GotFocus += (s, e) => { 
                if (tb.Text == "输入文字...") tb.Text = ""; 
                tb.Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)); 
                tb.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 0, 162, 255)); 
            };
            tb.LostFocus += (s, e) => { 
                tb.Background = Brushes.Transparent; 
                tb.BorderBrush = Brushes.Transparent; 
            };
            WrapObject(tb, p.X, p.Y, double.NaN, double.NaN); tb.Focus();
        }

        private void WrapObject(FrameworkElement content, double x, double y, double w, double h) {
            var oc = new KaqiaObjectControl(); 
            if (!double.IsNaN(w)) oc.Width = w; else if (content is TextBox) oc.Width = double.NaN;
            if (!double.IsNaN(h)) oc.Height = h; else if (content is TextBox) oc.Height = double.NaN;

            content.Width = double.NaN; content.Height = double.NaN;
            if (content is Shape s) s.Stretch = Stretch.Fill; 
            else if (content is Image i) i.Stretch = Stretch.Fill;
            else if (content is TextBox) { content.HorizontalAlignment = HorizontalAlignment.Stretch; content.VerticalAlignment = VerticalAlignment.Stretch; }

            oc.ObjectContent.Content = content;
            oc.DeleteRequested += (s, e) => { ImageContainer.Children.Remove(oc); if (_selectedObject == oc) _selectedObject = null; ToolPropertyPopup.IsOpen = false; };
            oc.Selected += (s, e) => { 
                if (_selectedObject == oc && ToolPropertyPopup.IsOpen) return;

                // Optimized selection logic: deselect others but don't close popup yet to avoid flicker
                foreach (var child in ImageContainer.Children) if (child is KaqiaObjectControl other) other.SetSelected(false);

                oc.SetSelected(true); _selectedObject = oc; 
                DrawingMode mode = DrawingMode.Rectangle; 
                if (content is System.Windows.Shapes.Path) mode = DrawingMode.Arrow;
                else if (content is Polyline) mode = DrawingMode.Pen;
                else if (content is Ellipse) mode = DrawingMode.Ellipse;
                else if (content is TextBox) mode = DrawingMode.Text;
                ShowToolPropertyPopup(mode, oc);
            };
            ImageContainer.Children.Add(oc); Canvas.SetLeft(oc, x); Canvas.SetTop(oc, y); DeselectAll(false); oc.SetSelected(true); _selectedObject = oc;
        }

        private void OnStickerSelected(object sender, MouseButtonEventArgs e) {
            if (sender is FrameworkElement fe && fe.DataContext is StickerItem item) {
                var img = new Image { Source = item.Image, Stretch = Stretch.Uniform };
                WrapObject(img, (_currentSelection.Width - 120) / 2, (_currentSelection.Height - 120) / 2, 120, 120); StickerPickerPopup.IsOpen = false;
            }
        }

        // --- BEAUTIFY LOGIC ---

        private void UpdateBeautifyEffects() {
            if (!_isInitialized || EditCanvas.Visibility != Visibility.Visible) return;
            StrokeLayer.CornerRadius = new CornerRadius(RadiusSlider.Value); StrokeLayer.BorderThickness = new Thickness(StrokeSlider.Value);
            CanvasLayer.Padding = new Thickness(PaddingSlider.Value);
            ShadowLayer.Effect = ShadowCheck.IsChecked == true ? new DropShadowEffect { BlurRadius = 40, Opacity = 0.5, ShadowDepth = 15, Direction = 270, RenderingBias = RenderingBias.Quality } : null;
            UpdateToolbarPosition();
        }

        private void UpdateToolbarPosition() {
            if (!_isInitialized) return;
            double eb = CanvasLayer.Padding.Bottom + (ShadowCheck.IsChecked == true ? 35 : 0);
            Toolbar.Margin = new Thickness(_currentSelection.Left + Math.Max(0, _currentSelection.Width - 420), _currentSelection.Bottom + 20 + eb, 0, 0);
        }

        private void OnBeautifyToggle(object sender, RoutedEventArgs e) => BeautifyPopup.IsOpen = !BeautifyPopup.IsOpen;
        private void OnStickerPickerToggle(object sender, RoutedEventArgs e) => StickerPickerPopup.IsOpen = !StickerPickerPopup.IsOpen;
        private void OnBeautifyParamChanged(object sender, RoutedEventArgs e) { UpdateBeautifyEffects(); SaveCurrentConfig(); }
        private void OnRadiusChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { UpdateBeautifyEffects(); SaveCurrentConfig(); }
        private void OnStrokeChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { UpdateBeautifyEffects(); SaveCurrentConfig(); }
        private void OnPaddingChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { UpdateBeautifyEffects(); SaveCurrentConfig(); }
        private void OnShadowChanged(object sender, RoutedEventArgs e) { UpdateBeautifyEffects(); SaveCurrentConfig(); }

        private void OnStrokeColorSelected(object sender, MouseButtonEventArgs e) {
            if (sender is Border b && b.Background != null) { StrokeLayer.BorderBrush = b.Background; SaveCurrentConfig(); }
        }

        private void OnCanvasColorSelected(object sender, MouseButtonEventArgs e) {
            if (sender is Border b && b.Background != null) { 
                CanvasLayer.Background = b.Background; 
                CanvasHexInput.Text = b.Background.ToString();
                SaveCurrentConfig();
            }
        }

        private void OnUploadStickerClick(object sender, RoutedEventArgs e) {
            var ofd = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp" }; if (ofd.ShowDialog() == true) { StickerManager.AddSticker(ofd.FileName); LoadStickers(); }
        }

        private void OnDeleteStickerFromLibrary(object sender, RoutedEventArgs e) {
            if (sender is Button b && b.Tag is StickerItem item) { StickerManager.DeleteSticker(item.FilePath); StickerLibrary.Remove(item); }
        }

        private void OnFinishClick(object sender, RoutedEventArgs e) {
            DeselectAll(); ShadowLayer.UpdateLayout(); var src = PresentationSource.FromVisual(this);
            double dx = 96.0 * (src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0);
            double dy = 96.0 * (src?.CompositionTarget?.TransformToDevice.M22 ?? 1.0);
            var rb = new RenderTargetBitmap((int)Math.Round(ShadowLayer.ActualWidth * (dx / 96.0)), (int)Math.Round(ShadowLayer.ActualHeight * (dy / 96.0)), dx, dy, PixelFormats.Pbgra32);
            rb.Render(ShadowLayer); var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rb));
            using (var s = new MemoryStream()) {
                enc.Save(s); var data = new DataObject(); data.SetImage(rb); data.SetData("PNG", s, false); Clipboard.SetDataObject(data, true); 
                if (!string.IsNullOrEmpty(_config.DefaultSavePath) && Directory.Exists(_config.DefaultSavePath)) {
                    string fn = $"Kaqia_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    string path = System.IO.Path.Combine(_config.DefaultSavePath, fn);
                    using (var fs = File.OpenWrite(path)) { s.Position = 0; s.CopyTo(fs); }
                }
            }
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
    }
}
