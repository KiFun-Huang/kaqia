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
        private bool _isApplyingConfig = false;

        private DrawingMode _currentMode = DrawingMode.Select;
        private Shape? _activeShape;
        private Polyline? _activePolyline;

        // Crop state variables
        private bool _isDraggingCrop = false;
        private bool _isResizingCrop = false;
        private string _cropResizeEdge = "";
        private Point _cropDragStart;
        private Rect _initialCropSelection;
        private bool _infoInputFocused = false;
        
        // Toolbar Drag State
        private bool _isDraggingToolbar = false;
        private Point _toolbarDragStart;
        private Thickness _toolbarStartMargin;
        private bool _toolbarManuallyMoved = false;

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
            _isApplyingConfig = true; // Lock saving during initialization

            RadiusSlider.Value = _config.Radius;
            StrokeSlider.Value = _config.StrokeThickness;
            PaddingSlider.Value = _config.Padding;
            ShadowCheck.IsChecked = _config.ShadowEnabled;
            try {
                if (!string.IsNullOrEmpty(_config.StrokeColor)) {
                    StrokeLayer.BorderBrush = (Brush)new BrushConverter().ConvertFromString(_config.StrokeColor);
                    StrokeHexInput.Text = _config.StrokeColor;
                }
                if (!string.IsNullOrEmpty(_config.CanvasColor)) {
                    CanvasLayer.Background = (Brush)new BrushConverter().ConvertFromString(_config.CanvasColor);
                    CanvasHexInput.Text = _config.CanvasColor;
                }
            } catch { }
            UpdateBeautifyEffects();
            
            _isApplyingConfig = false; // Unlock saving
        }

        private void SaveCurrentConfig()
        {
            if (!_isInitialized || _config == null || _isApplyingConfig) return;
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
            UpdateCropGridVisibility();
        }

        private void UpdateCropGridVisibility() {
            if (CropResizeGrid != null) {
                CropResizeGrid.Visibility = (_currentMode == DrawingMode.Select && _selectedObject == null) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (EditCanvas.Visibility == Visibility.Visible) {
                if (e.OriginalSource == MainCanvas || e.OriginalSource == BackgroundImg) {
                    DeselectAll();
                }
                return;
            }
            _isSelecting = true; 
            _startPoint = e.GetPosition(MainCanvas); 
            SelectionBorder.Visibility = Visibility.Visible;
            InfoOverlay.Visibility = Visibility.Visible;
            InfoPosText.Visibility = Visibility.Visible;
            InfoSizePanel.Visibility = Visibility.Collapsed;
        }

        private void OnMouseMove(object sender, MouseEventArgs e) {
            if (!_isSelecting) return;
            Point p = e.GetPosition(MainCanvas);
            double left = Math.Min(_startPoint.X, p.X);
            double top = Math.Min(_startPoint.Y, p.Y);
            double width = Math.Max(1, Math.Abs(p.X - _startPoint.X));
            double height = Math.Max(1, Math.Abs(p.Y - _startPoint.Y));
            
            _currentSelection = new Rect(left, top, width, height);
            Canvas.SetLeft(SelectionBorder, left); 
            Canvas.SetTop(SelectionBorder, top);
            SelectionBorder.Width = width; 
            SelectionBorder.Height = height; 
            SelectionRect.Rect = _currentSelection;

            InfoPosText.Text = $"X: {Math.Round(p.X)}  Y: {Math.Round(p.Y)}";
            UpdateInfoOverlay();
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e) {
            if (!_isSelecting) return; 
            _isSelecting = false;
            
            if (_currentSelection.Width > 10 && _currentSelection.Height > 10) {
                InfoPosText.Visibility = Visibility.Collapsed;
                InfoSizePanel.Visibility = Visibility.Visible;
                InfoWidthInput.Text = Math.Round(_currentSelection.Width).ToString();
                InfoHeightInput.Text = Math.Round(_currentSelection.Height).ToString();
                EnterEditMode();
            } else {
                SelectionBorder.Visibility = Visibility.Collapsed;
                InfoOverlay.Visibility = Visibility.Collapsed;
                SelectionRect.Rect = new Rect(0, 0, 0, 0);
            }
        }

        private void EnterEditMode() {
            SelectionBorder.Visibility = Visibility.Collapsed;
            CropResizeGrid.Visibility = Visibility.Visible;
            EditCanvas.Visibility = Visibility.Visible; 
            Toolbar.Visibility = Visibility.Visible; 
            UpdateCropRegion(_currentSelection);
            UpdateBeautifyEffects();
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

            bool isText = mode == DrawingMode.Text;
            ToolThicknessLabel.Text = isText ? "字号" : "粗细";
            ToolThicknessSlider.Minimum = isText ? 12 : 1;
            ToolThicknessSlider.Maximum = isText ? 100 : 15;

            if (_config.Tools.TryGetValue(mode.ToString(), out var state)) {
                if (target != null) {
                    ToolThicknessSlider.Value = isText ? target.ObjectFontSize : target.ObjectThickness;
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
            bool isText = _currentMode == DrawingMode.Text || (_selectedObject != null && _selectedObject.ObjectContent.Content is TextBox);
            
            if (_selectedObject != null) {
                if (isText) _selectedObject.ObjectFontSize = e.NewValue;
                else _selectedObject.ObjectThickness = e.NewValue;
            } else if (_currentMode != DrawingMode.Select) {
                _config.Tools[_currentMode.ToString()].Thickness = e.NewValue;
                SaveCurrentConfig();
            }
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

        // --- CROP RESIZE & DRAG LOGIC ---

        private void UpdateInfoOverlay() {
            if (_currentSelection.Width <= 0 || _currentSelection.Height <= 0) return;
            InfoOverlay.Visibility = Visibility.Visible;
            double left = _currentSelection.Left;
            double top = _currentSelection.Top - 30;
            if (top < 0) top = _currentSelection.Top + 5;
            Canvas.SetLeft(InfoOverlay, left);
            Canvas.SetTop(InfoOverlay, top);
        }

        private void UpdateCropRegion(Rect newRect) {
            newRect.X = Math.Max(0, Math.Min(newRect.X, Width - 20));
            newRect.Y = Math.Max(0, Math.Min(newRect.Y, Height - 20));
            newRect.Width = Math.Max(20, Math.Min(newRect.Width, Width - newRect.X));
            newRect.Height = Math.Max(20, Math.Min(newRect.Height, Height - newRect.Y));

            double dx = newRect.X - _currentSelection.X;
            double dy = newRect.Y - _currentSelection.Y;

            _currentSelection = newRect;
            SelectionRect.Rect = _currentSelection;

            try {
                var croppedBitmap = new CroppedBitmap(_fullScreenshot, new Int32Rect((int)(_currentSelection.X * _scaleX), (int)(_currentSelection.Y * _scaleY), (int)(_currentSelection.Width * _scaleX), (int)(_currentSelection.Height * _scaleY)));
                CroppedImage.Source = croppedBitmap;
                CroppedImage.Width = _currentSelection.Width; 
                CroppedImage.Height = _currentSelection.Height;
                
                ImageContainer.Width = _currentSelection.Width; 
                ImageContainer.Height = _currentSelection.Height;
                AnnotationCanvas.Width = _currentSelection.Width; 
                AnnotationCanvas.Height = _currentSelection.Height;
                CropResizeGrid.Width = _currentSelection.Width;
                CropResizeGrid.Height = _currentSelection.Height;
                
                Canvas.SetLeft(EditCanvas, _currentSelection.Left); 
                Canvas.SetTop(EditCanvas, _currentSelection.Top);

                foreach (UIElement child in ImageContainer.Children) {
                    if (child is KaqiaObjectControl || child is Shape || child is Polyline) {
                        Canvas.SetLeft(child, Canvas.GetLeft(child) - dx);
                        Canvas.SetTop(child, Canvas.GetTop(child) - dy);
                    }
                }

                UpdateToolbarPosition();
                UpdateInfoOverlay();

                if (InfoSizePanel.Visibility == Visibility.Visible && !_infoInputFocused) {
                    InfoWidthInput.Text = Math.Round(_currentSelection.Width).ToString();
                    InfoHeightInput.Text = Math.Round(_currentSelection.Height).ToString();
                }
            } catch { }
        }

        private void OnInfoSizeGotFocus(object sender, RoutedEventArgs e) => _infoInputFocused = true;
        private void OnInfoSizeLostFocus(object sender, RoutedEventArgs e) { _infoInputFocused = false; ApplyManualCropSize(); }
        private void OnInfoSizeKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { Keyboard.ClearFocus(); ApplyManualCropSize(); } }

        private void ApplyManualCropSize() {
            if (double.TryParse(InfoWidthInput.Text, out double w) && double.TryParse(InfoHeightInput.Text, out double h)) {
                Rect newRect = _currentSelection;
                newRect.Width = Math.Max(20, Math.Min(w, Width - newRect.X));
                newRect.Height = Math.Max(20, Math.Min(h, Height - newRect.Y));
                UpdateCropRegion(newRect);
            }
        }

        private void OnCropHandleMouseDown(object sender, MouseButtonEventArgs e) {
            if (sender is FrameworkElement fe) {
                _isResizingCrop = true;
                _cropResizeEdge = fe.Tag.ToString() ?? "";
                _cropDragStart = e.GetPosition(MainCanvas);
                _initialCropSelection = _currentSelection;
                fe.CaptureMouse();
                e.Handled = true;
            }
        }

        private void OnCropHandleMouseMove(object sender, MouseEventArgs e) {
            if (!_isResizingCrop) return;
            Point p = e.GetPosition(MainCanvas);
            double dx = p.X - _cropDragStart.X;
            double dy = p.Y - _cropDragStart.Y;
            Rect newRect = _initialCropSelection;
            
            switch (_cropResizeEdge) {
                case "TopLeft": newRect.X += dx; newRect.Width -= dx; newRect.Y += dy; newRect.Height -= dy; break;
                case "Top": newRect.Y += dy; newRect.Height -= dy; break;
                case "TopRight": newRect.Width += dx; newRect.Y += dy; newRect.Height -= dy; break;
                case "Left": newRect.X += dx; newRect.Width -= dx; break;
                case "Right": newRect.Width += dx; break;
                case "BottomLeft": newRect.X += dx; newRect.Width -= dx; newRect.Height += dy; break;
                case "Bottom": newRect.Height += dy; break;
                case "BottomRight": newRect.Width += dx; newRect.Height += dy; break;
            }
            
            if (newRect.Width < 20) { newRect.X = _currentSelection.X; newRect.Width = 20; }
            if (newRect.Height < 20) { newRect.Y = _currentSelection.Y; newRect.Height = 20; }
            UpdateCropRegion(newRect);
        }
        
        private void OnCropHandleMouseUp(object sender, MouseButtonEventArgs e) {
            if (_isResizingCrop && sender is FrameworkElement fe) {
                _isResizingCrop = false;
                fe.ReleaseMouseCapture();
                if (!_infoInputFocused) {
                    InfoWidthInput.Text = Math.Round(_currentSelection.Width).ToString();
                    InfoHeightInput.Text = Math.Round(_currentSelection.Height).ToString();
                }
            }
        }

        // --- DRAWING LOGIC ---

        private void OnAnnotationCanvasMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.OriginalSource == AnnotationCanvas) { 
                DeselectAll(); 
                Keyboard.ClearFocus();
                AnnotationCanvas.Focus();
                if (_currentMode == DrawingMode.Select) {
                    _isDraggingCrop = true;
                    _cropDragStart = e.GetPosition(MainCanvas);
                    _initialCropSelection = _currentSelection;
                    AnnotationCanvas.CaptureMouse();
                    return;
                }
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
            if (_isDraggingCrop) {
                Point p2 = e.GetPosition(MainCanvas);
                double dx = p2.X - _cropDragStart.X;
                double dy = p2.Y - _cropDragStart.Y;
                Rect newRect = _initialCropSelection;
                newRect.X += dx;
                newRect.Y += dy;
                UpdateCropRegion(newRect);
                return;
            }

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
            if (_isDraggingCrop) {
                _isDraggingCrop = false;
                AnnotationCanvas.ReleaseMouseCapture();
                return;
            }

            if (_activeShape != null || _activePolyline != null) {
                FrameworkElement? content = (FrameworkElement?)_activeShape ?? _activePolyline;
                bool isClickOnly = false;

                if (content != null) {
                    double x, y, w, h;
                    if (content is System.Windows.Shapes.Path path) {
                        Rect bounds = path.Data.Bounds;
                        isClickOnly = bounds.Width < 5 && bounds.Height < 5;
                        x = bounds.Left; y = bounds.Top; w = Math.Max(10, bounds.Width); h = Math.Max(10, bounds.Height);
                        // Store the original points to allow re-drawing
                        if (path.Tag is Point[] pts) {
                            path.Stretch = Stretch.Fill;
                        }
                    } else if (content is Polyline pl) {
                        Rect bounds = GetPolylineBounds(pl);
                        isClickOnly = bounds.Width < 5 && bounds.Height < 5;
                        x = bounds.Left; y = bounds.Top; w = Math.Max(10, bounds.Width); h = Math.Max(10, bounds.Height);
                        for(int i=0; i<pl.Points.Count; i++) pl.Points[i] = new Point(pl.Points[i].X - x, pl.Points[i].Y - y);
                    } else { 
                        x = Canvas.GetLeft(content); y = Canvas.GetTop(content); w = content.Width; h = content.Height; 
                        isClickOnly = double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(w) || double.IsNaN(h) || (w < 5 && h < 5);
                    }
                    
                    AnnotationCanvas.Children.Remove(content); 
                    if (!isClickOnly) {
                        WrapObject(content, x, y, w, h);
                    }
                }
                
                _activeShape = null; _activePolyline = null; AnnotationCanvas.ReleaseMouseCapture();
                
                if (!isClickOnly) {
                    SelectToolBtn.IsChecked = true; _currentMode = DrawingMode.Select;
                }
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
            else if (content is Image i) i.Stretch = Stretch.Uniform;
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
                var bitmap = item.Image;
                double w = bitmap.Width;
                double h = bitmap.Height;

                if (w == 0 || h == 0) { w = 120; h = 120; }
                else {
                    // Fit within a maximum dimension of 200px while maintaining aspect ratio
                    double maxDim = 200;
                    double scale = Math.Min(maxDim / w, maxDim / h);
                    if (scale < 1.0) { w *= scale; h *= scale; }
                }

                var img = new Image { Source = bitmap, Stretch = Stretch.Uniform };
                WrapObject(img, (_currentSelection.Width - w) / 2, (_currentSelection.Height - h) / 2, w, h); 
                StickerPickerPopup.IsOpen = false;
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

            Toolbar.UpdateLayout(); // Force layout to get accurate width
            double tbWidth = Toolbar.ActualWidth > 0 ? Toolbar.ActualWidth : 460;
            double tbHeight = Toolbar.ActualHeight > 0 ? Toolbar.ActualHeight : 42;

            if (_toolbarManuallyMoved) {
                // Ensure it stays within the overall virtual screen bounds
                double curLeft = Math.Max(0, Math.Min(Toolbar.Margin.Left, Width - tbWidth));
                double curTop = Math.Max(0, Math.Min(Toolbar.Margin.Top, Height - tbHeight));
                Toolbar.Margin = new Thickness(curLeft, curTop, 0, 0);
                return;
            }

            double eb = CanvasLayer.Padding.Bottom + (ShadowCheck.IsChecked == true ? 35 : 0);
            double targetX = _currentSelection.Right - tbWidth;
            double targetY = _currentSelection.Bottom + 20 + eb;

            // Simple heuristic: If the crop box is tall (> 600px) or placing it outside exceeds 
            // the global virtual screen height, place the toolbar INSIDE the crop box.
            if (_currentSelection.Height > 600 || targetY + tbHeight > Height) {
                targetY = _currentSelection.Bottom - tbHeight - 10;
                targetX = _currentSelection.Right - tbWidth - 15; // Shifted slightly more to the left
            }

            // Global safety clamps
            if (targetX < 0) targetX = 10;
            if (targetY < 0) targetY = 10;
            if (targetX + tbWidth > Width) targetX = Width - tbWidth - 10;
            if (targetY + tbHeight > Height) targetY = Height - tbHeight - 10;

            Toolbar.Margin = new Thickness(targetX, targetY, 0, 0);
        }

        private void OnToolbarMouseDown(object sender, MouseButtonEventArgs e) {
            _isDraggingToolbar = true;
            _toolbarManuallyMoved = true;
            _toolbarDragStart = e.GetPosition(this);
            _toolbarStartMargin = Toolbar.Margin;
            Toolbar.CaptureMouse();
            e.Handled = true;
        }

        private void OnToolbarMouseMove(object sender, MouseEventArgs e) {
            if (!_isDraggingToolbar) return;
            Point p = e.GetPosition(this);
            double dx = p.X - _toolbarDragStart.X;
            double dy = p.Y - _toolbarDragStart.Y;

            double newLeft = Math.Max(0, Math.Min(_toolbarStartMargin.Left + dx, Width - Toolbar.ActualWidth));
            double newTop = Math.Max(0, Math.Min(_toolbarStartMargin.Top + dy, Height - Toolbar.ActualHeight));
            Toolbar.Margin = new Thickness(newLeft, newTop, 0, 0);
        }

        private void OnToolbarMouseUp(object sender, MouseButtonEventArgs e) {
            if (_isDraggingToolbar) {
                _isDraggingToolbar = false;
                Toolbar.ReleaseMouseCapture();
            }
        }

        private void OnBeautifyToggle(object sender, RoutedEventArgs e) => BeautifyPopup.IsOpen = !BeautifyPopup.IsOpen;
        private void OnStickerPickerToggle(object sender, RoutedEventArgs e) => StickerPickerPopup.IsOpen = !StickerPickerPopup.IsOpen;
        private void OnBeautifyParamChanged(object sender, RoutedEventArgs e) { UpdateBeautifyEffects(); SaveCurrentConfig(); }
        private void OnRadiusChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { UpdateBeautifyEffects(); SaveCurrentConfig(); }
        private void OnStrokeChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { UpdateBeautifyEffects(); SaveCurrentConfig(); }
        private void OnPaddingChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { UpdateBeautifyEffects(); SaveCurrentConfig(); }
        private void OnShadowChanged(object sender, RoutedEventArgs e) { 
            if (!_isInitialized) return;
            UpdateBeautifyEffects(); 
            bool isEffectivelyEmpty = (CanvasLayer.Background == null || CanvasLayer.Background == Brushes.Transparent || CanvasLayer.Background.ToString() == "#00000000");
            if (ShadowCheck.IsChecked == true && PaddingSlider.Value == 0 && isEffectivelyEmpty) {
                PaddingSlider.Value = 20;
                CanvasLayer.Background = Brushes.White;
                CanvasHexInput.Text = "#FFFFFFFF";
            }
            SaveCurrentConfig(); 
        }

        private void OnStrokeColorSelected(object sender, MouseButtonEventArgs e) {
            if (sender is Border b && b.Background != null) { 
                StrokeLayer.BorderBrush = b.Background; 
                StrokeHexInput.Text = b.Background.ToString();
                SaveCurrentConfig(); 
            }
        }

        private void OnCustomStrokeColorClick(object sender, MouseButtonEventArgs e) {
            try {
                var brush = (Brush)new BrushConverter().ConvertFromString(_config.LastCustomColor);
                StrokeLayer.BorderBrush = brush;
                StrokeHexInput.Text = _config.LastCustomColor;
                SaveCurrentConfig();
            } catch { }
            StrokeHexInput.Focus();
            StrokeHexInput.SelectAll();
        }

        private void OnStrokeHexKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                try {
                    var brush = (Brush)new BrushConverter().ConvertFromString(StrokeHexInput.Text);
                    StrokeLayer.BorderBrush = brush;
                    _config.LastCustomColor = StrokeHexInput.Text;
                    SaveCurrentConfig();
                } catch { }
            }
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
