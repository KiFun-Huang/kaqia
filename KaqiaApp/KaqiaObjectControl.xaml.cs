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
            Canvas.SetLeft(RotateHandle, this.ActualWidth / 2 - 6);
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

        private void OnContentMouseDown(object sender, MouseButtonEventArgs e)
        {
            Selected?.Invoke(this, EventArgs.Empty);
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
            double newWidth = this.Width;
            double newHeight = this.Height;

            switch (_resizeEdge)
            {
                case "Right":
                    newWidth = Math.Max(10, this.Width + delta.X);
                    this.Width = newWidth;
                    break;
                case "Left":
                    newWidth = Math.Max(10, this.Width - delta.X);
                    this.Width = newWidth;
                    ObjectTranslate.X += (this.Width == 10) ? 0 : delta.X;
                    break;
                case "Bottom":
                    newHeight = Math.Max(10, this.Height + delta.Y);
                    this.Height = newHeight;
                    break;
                case "Top":
                    newHeight = Math.Max(10, this.Height - delta.Y);
                    this.Height = newHeight;
                    ObjectTranslate.Y += (this.Height == 10) ? 0 : delta.Y;
                    break;
                case "BottomRight":
                    this.Width = Math.Max(10, this.Width + delta.X);
                    this.Height = Math.Max(10, this.Height + delta.Y);
                    break;
                case "TopLeft":
                    newWidth = Math.Max(10, this.Width - delta.X);
                    newHeight = Math.Max(10, this.Height - delta.Y);
                    if (newWidth > 10) ObjectTranslate.X += delta.X;
                    if (newHeight > 10) ObjectTranslate.Y += delta.Y;
                    this.Width = newWidth;
                    this.Height = newHeight;
                    break;
                case "TopRight":
                    this.Width = Math.Max(10, this.Width + delta.X);
                    newHeight = Math.Max(10, this.Height - delta.Y);
                    if (newHeight > 10) ObjectTranslate.Y += delta.Y;
                    this.Height = newHeight;
                    break;
                case "BottomLeft":
                    newWidth = Math.Max(10, this.Width - delta.X);
                    if (newWidth > 10) ObjectTranslate.X += delta.X;
                    this.Width = newWidth;
                    this.Height = Math.Max(10, this.Height + delta.Y);
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
