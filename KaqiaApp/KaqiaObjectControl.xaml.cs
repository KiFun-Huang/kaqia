using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KaqiaApp
{
    public partial class KaqiaObjectControl : UserControl
    {
        private Point _lastMousePosition;
        private bool _isDragging;
        private bool _isResizing;
        private bool _isRotating;
        private string _resizeEdge = "";
        private double _initialAngle;
        private Point _centerPoint;

        public event EventHandler? DeleteRequested;
        public event EventHandler? Selected;
        public event EventHandler? SizeChangedManually;

        public KaqiaObjectControl()
        {
            InitializeComponent();
            this.Loaded += (s, e) => UpdateRotateHandlePosition();
            this.SizeChanged += (s, e) => UpdateRotateHandlePosition();
        }

        private void UpdateRotateHandlePosition()
        {
            // Center in Column 1 (which starts at 10px and has width ActualWidth - 20)
            double centerX = (this.ActualWidth / 2) - 10 - 6;
            Canvas.SetLeft(RotateHandle, centerX);
        }

        public void SetSelected(bool isSelected)
        {
            ControlCanvas.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
        }

        // --- Property Management ---

        public Brush ObjectBrush
        {
            get {
                if (ObjectContent.Content is Shape s) return s.Stroke;
                if (ObjectContent.Content is TextBox tb) return tb.Foreground;
                return Brushes.Transparent;
            }
            set {
                if (ObjectContent.Content is Shape s) {
                    s.Stroke = value;
                    if (s is System.Windows.Shapes.Path p) s.Fill = value;
                }
                else if (ObjectContent.Content is TextBox tb) tb.Foreground = value;
            }
        }

        public double ObjectThickness
        {
            get {
                if (ObjectContent.Content is Shape s) return s.StrokeThickness;
                return 0;
            }
            set {
                if (ObjectContent.Content is Shape s) s.StrokeThickness = value;
            }
        }

        // --- Interaction ---

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            Selected?.Invoke(this, EventArgs.Empty);
            base.OnPreviewMouseDown(e);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            e.Handled = true; // Stop bubbling to MainCanvas to prevent deselection
            base.OnMouseDown(e);
        }

        private void OnContentMouseDown(object sender, MouseButtonEventArgs e)
        {
            SetSelected(true);
            _isDragging = true;
            _lastMousePosition = e.GetPosition(Parent as UIElement);
            this.CaptureMouse();
            e.Handled = true;
        }

        private void OnResizeHandleMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                _isResizing = true;
                _resizeEdge = fe.Tag.ToString() ?? "";
                _lastMousePosition = e.GetPosition(Parent as UIElement);
                this.CaptureMouse();
                e.Handled = true;
            }
        }

        private void OnRotateMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isRotating = true;
            UIElement parent = Parent as UIElement;
            _centerPoint = this.TransformToAncestor(parent).Transform(new Point(this.ActualWidth / 2, this.ActualHeight / 2));
            Point currentPos = e.GetPosition(parent);
            Vector delta = currentPos - _centerPoint;
            _initialAngle = Math.Atan2(delta.Y, delta.X) * 180 / Math.PI - ObjectRotate.Angle;
            this.CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            UIElement parent = Parent as UIElement;
            if (parent == null) return;
            Point currentPosition = e.GetPosition(parent);
            Vector delta = currentPosition - _lastMousePosition;

            if (_isDragging)
            {
                ObjectTranslate.X += delta.X;
                ObjectTranslate.Y += delta.Y;
                _lastMousePosition = currentPosition;
            }
            else if (_isResizing)
            {
                ApplyDirectResize(delta);
                _lastMousePosition = currentPosition;
                SizeChangedManually?.Invoke(this, EventArgs.Empty);
            }
            else if (_isRotating)
            {
                Vector d = currentPosition - _centerPoint;
                double angle = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
                ObjectRotate.Angle = angle - _initialAngle;
            }
        }

        private void ApplyDirectResize(Vector delta)
        {
            double currentWidth = double.IsNaN(this.Width) ? this.ActualWidth : this.Width;
            double currentHeight = double.IsNaN(this.Height) ? this.ActualHeight : this.Height;

            switch (_resizeEdge)
            {
                case "Right":
                    this.Width = Math.Max(10, currentWidth + delta.X);
                    break;
                case "Left":
                    double newWidthL = Math.Max(10, currentWidth - delta.X);
                    if (newWidthL > 10) ObjectTranslate.X += delta.X;
                    this.Width = newWidthL;
                    break;
                case "Bottom":
                    this.Height = Math.Max(10, currentHeight + delta.Y);
                    break;
                case "Top":
                    double newHeightT = Math.Max(10, currentHeight - delta.Y);
                    if (newHeightT > 10) ObjectTranslate.Y += delta.Y;
                    this.Height = newHeightT;
                    break;
                case "BottomRight":
                    this.Width = Math.Max(10, currentWidth + delta.X);
                    this.Height = Math.Max(10, currentHeight + delta.Y);
                    break;
                case "TopLeft":
                    double newWidthTL = Math.Max(10, currentWidth - delta.X);
                    double newHeightTL = Math.Max(10, currentHeight - delta.Y);
                    if (newWidthTL > 10) ObjectTranslate.X += delta.X;
                    if (newHeightTL > 10) ObjectTranslate.Y += delta.Y;
                    this.Width = newWidthTL;
                    this.Height = newHeightTL;
                    break;
                case "TopRight":
                    this.Width = Math.Max(10, currentWidth + delta.X);
                    double newHeightTR = Math.Max(10, currentHeight - delta.Y);
                    if (newHeightTR > 10) ObjectTranslate.Y += delta.Y;
                    this.Height = newHeightTR;
                    break;
                case "BottomLeft":
                    double newWidthBL = Math.Max(10, currentWidth - delta.X);
                    if (newWidthBL > 10) ObjectTranslate.X += delta.X;
                    this.Width = newWidthBL;
                    this.Height = Math.Max(10, currentHeight + delta.Y);
                    break;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            _isDragging = false;
            _isResizing = false;
            _isRotating = false;
            this.ReleaseMouseCapture();
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
