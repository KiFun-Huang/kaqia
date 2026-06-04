using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KaqiaApp
{
    public partial class StickerControl : UserControl
    {
        private Point _lastMousePosition;
        private bool _isDragging;
        private bool _isResizing;
        private bool _isRotating;
        private double _initialAngle;
        private Point _centerPoint;

        public event EventHandler DeleteRequested;
        public event EventHandler Selected;

        public StickerControl()
        {
            InitializeComponent();
            this.Loaded += (s, e) => {
                // Ensure RotateHandle is centered horizontally
                Canvas.SetLeft(RotateHandle, this.ActualWidth / 2 - 6);
            };
        }

        public void SetSelected(bool isSelected)
        {
            ControlCanvas.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnImageMouseDown(object sender, MouseButtonEventArgs e)
        {
            Selected?.Invoke(this, EventArgs.Empty);
            SetSelected(true);

            _isDragging = true;
            _lastMousePosition = e.GetPosition(Parent as UIElement);
            this.CaptureMouse();
            e.Handled = true;
        }

        private void OnResizeMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isResizing = true;
            _lastMousePosition = e.GetPosition(Parent as UIElement);
            this.CaptureMouse();
            e.Handled = true;
        }

        private void OnRotateMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isRotating = true;
            UIElement parent = Parent as UIElement;
            _centerPoint = this.TransformToAncestor(parent).Transform(new Point(this.ActualWidth / 2, this.ActualHeight / 2));
            
            Point currentPos = e.GetPosition(parent);
            Vector delta = currentPos - _centerPoint;
            _initialAngle = Math.Atan2(delta.Y, delta.X) * 180 / Math.PI - StickerRotate.Angle;
            
            this.CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            UIElement parent = Parent as UIElement;
            Point currentPosition = e.GetPosition(parent);

            if (_isDragging)
            {
                var offset = currentPosition - _lastMousePosition;
                StickerTranslate.X += offset.X;
                StickerTranslate.Y += offset.Y;
                _lastMousePosition = currentPosition;
            }
            else if (_isResizing)
            {
                // Simple proportional scaling based on width change
                double deltaX = currentPosition.X - _lastMousePosition.X;
                double scaleFactor = 1 + (deltaX / this.ActualWidth);
                
                StickerScale.ScaleX *= scaleFactor;
                StickerScale.ScaleY *= scaleFactor;
                _lastMousePosition = currentPosition;
            }
            else if (_isRotating)
            {
                Vector delta = currentPosition - _centerPoint;
                double angle = Math.Atan2(delta.Y, delta.X) * 180 / Math.PI;
                StickerRotate.Angle = angle - _initialAngle;
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
