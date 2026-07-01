using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TinyIcon.Behaviors;

/// <summary>
/// Lets the user drag the associated element around with the right mouse button. A plain right-click (a
/// press-and-release without meaningful movement) recenters the element, as does a change of the bound
/// <see cref="ResetSignal"/> (wire it to the selected item so switching selection recenters too). The pan is
/// applied as a <see cref="TranslateTransform"/> on the element's <see cref="UIElement.RenderTransform"/> and
/// is constrained so at least <see cref="MinVisibleFraction"/> of the element always overlaps its parent.
/// </summary>
public sealed class PanBehavior : Behavior<FrameworkElement>
{
    /// <summary>Movement (in device-independent pixels) tolerated before a right-click counts as a drag.</summary>
    private const double DragThreshold = 3.0;

    private readonly TranslateTransform _transform = new(0.0, 0.0);

    private Point _dragStart;
    private double _startPanX;
    private double _startPanY;
    private double _panX;
    private double _panY;
    private bool _isPanning;
    private bool _dragged;

    public static readonly DependencyProperty MinVisibleFractionProperty = DependencyProperty.Register(
        nameof(MinVisibleFraction), typeof(double), typeof(PanBehavior), new PropertyMetadata(0.25));

    /// <summary>Fraction of the element that must remain within the parent's bounds (0.25 = a quarter).</summary>
    public double MinVisibleFraction
    {
        get => (double)GetValue(MinVisibleFractionProperty);
        set => SetValue(MinVisibleFractionProperty, value);
    }

    public static readonly DependencyProperty ResetSignalProperty = DependencyProperty.Register(
        nameof(ResetSignal), typeof(object), typeof(PanBehavior),
        new PropertyMetadata(null, OnResetSignalChanged));

    /// <summary>When the bound value changes, the pan is recentered. Bind it to the current selection.</summary>
    public object? ResetSignal
    {
        get => GetValue(ResetSignalProperty);
        set => SetValue(ResetSignalProperty, value);
    }

    protected override void OnAttached()
    {
        AssociatedObject.RenderTransform = _transform;
        AssociatedObject.Loaded += OnLoaded;
        AssociatedObject.MouseRightButtonDown += OnRightButtonDown;
        AssociatedObject.MouseMove += OnMouseMove;
        AssociatedObject.MouseRightButtonUp += OnRightButtonUp;
        AssociatedObject.SizeChanged += OnSizeChanged;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= OnLoaded;
        AssociatedObject.MouseRightButtonDown -= OnRightButtonDown;
        AssociatedObject.MouseMove -= OnMouseMove;
        AssociatedObject.MouseRightButtonUp -= OnRightButtonUp;
        AssociatedObject.SizeChanged -= OnSizeChanged;

        if (Host is { } host)
            host.SizeChanged -= OnHostSizeChanged;
    }

    /// <summary>The element the pan is measured against (its parent), or <see langword="null"/> if unset.</summary>
    private FrameworkElement? Host => VisualTreeHelper.GetParent(AssociatedObject) as FrameworkElement;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // The parent isn't reachable until the element is in the tree; keep centering in sync with its size.
        if (Host is { } host)
            host.SizeChanged += OnHostSizeChanged;

        ApplyTransform();
    }

    private void OnRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Host is not { } host)
            return;

        _dragStart = e.GetPosition(host);
        _startPanX = _panX;
        _startPanY = _panY;
        _isPanning = true;
        _dragged = false;
        AssociatedObject.CaptureMouse();
        AssociatedObject.Cursor = Cursors.Hand;
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning || Host is not { } host)
            return;

        var current = e.GetPosition(host);
        var dx = current.X - _dragStart.X;
        var dy = current.Y - _dragStart.Y;

        if (Math.Abs(dx) > DragThreshold || Math.Abs(dy) > DragThreshold)
            _dragged = true;

        _panX = _startPanX + dx;
        _panY = _startPanY + dy;
        ApplyTransform();
    }

    private void OnRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning)
            return;

        _isPanning = false;
        AssociatedObject.ReleaseMouseCapture();
        AssociatedObject.Cursor = null;

        // A right-click that did not turn into a drag recenters the element.
        if (!_dragged)
            Recenter();

        e.Handled = true;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => ApplyTransform();

    private void OnHostSizeChanged(object sender, SizeChangedEventArgs e) => ApplyTransform();

    private static void OnResetSignalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((PanBehavior)d).Recenter();

    private void Recenter()
    {
        _panX = 0.0;
        _panY = 0.0;
        ApplyTransform();
    }

    /// <summary>
    /// Re-clamps the pan and writes the resulting offset to the transform. The element is aligned to the
    /// host's top-left, so a centering term is added: this keeps the rest position (pan 0) centered at every
    /// size, including when a zoomed image is larger than the host and layout alignment would not center it.
    /// </summary>
    private void ApplyTransform()
    {
        if (Host is not { } host)
            return;

        var content = AssociatedObject;
        _panX = ClampAxis(_panX, content.ActualWidth, host.ActualWidth, MinVisibleFraction);
        _panY = ClampAxis(_panY, content.ActualHeight, host.ActualHeight, MinVisibleFraction);

        _transform.X = (host.ActualWidth - content.ActualWidth) / 2.0 + _panX;
        _transform.Y = (host.ActualHeight - content.ActualHeight) / 2.0 + _panY;
    }

    /// <summary>
    /// Clamps a desired pan offset (from the centered rest position) along one axis so that at least
    /// <paramref name="minVisibleFraction"/> of the content stays within the viewport. When the content is larger
    /// than the viewport the offset is limited to keep the viewport fully covered.
    /// </summary>
    public static double ClampAxis(double desired, double content, double viewport, double minVisibleFraction)
    {
        var requiredOverlap = Math.Min(minVisibleFraction * content, viewport);
        var max = (content + viewport) / 2.0 - requiredOverlap;
        if (max <= 0.0)
            return 0.0;

        return Math.Clamp(desired, -max, max);
    }
}
