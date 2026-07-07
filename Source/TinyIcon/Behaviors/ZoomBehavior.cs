using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TinyIcon.Models;

namespace TinyIcon.Behaviors;

/// <summary>
/// Encapsulates zooming of the associated element. It applies a <see cref="ScaleTransform"/> as the
/// element's <see cref="FrameworkElement.LayoutTransform"/> (so a surrounding <c>ScrollViewer</c> scrolls
/// and centers naturally) and zooms on mouse-wheel. <see cref="ZoomLevel"/> is two-way bindable so it can
/// be shared with menu/toolbar/keyboard commands and preserved when the selected image changes.
/// </summary>
public sealed class ZoomBehavior : Behavior<FrameworkElement>
{
    private readonly ScaleTransform _transform = new(1.0, 1.0);

    public static readonly DependencyProperty ZoomLevelProperty = DependencyProperty.Register(
        nameof(ZoomLevel), typeof(double), typeof(ZoomBehavior),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnZoomLevelChanged, CoerceZoom));

    /// <summary>The current zoom factor (1.0 = 100%).</summary>
    public double ZoomLevel
    {
        get => (double)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    public static readonly DependencyProperty MinZoomProperty = DependencyProperty.Register(
        nameof(MinZoom), typeof(double), typeof(ZoomBehavior),
        new PropertyMetadata(ZoomDefaults.Min, OnZoomLimitChanged));

    public double MinZoom
    {
        get => (double)GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    public static readonly DependencyProperty MaxZoomProperty = DependencyProperty.Register(
        nameof(MaxZoom), typeof(double), typeof(ZoomBehavior),
        new PropertyMetadata(ZoomDefaults.Max, OnZoomLimitChanged));

    public double MaxZoom
    {
        get => (double)GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    public static readonly DependencyProperty WheelStepProperty = DependencyProperty.Register(
        nameof(WheelStep), typeof(double), typeof(ZoomBehavior), new PropertyMetadata(ZoomDefaults.Step));

    /// <summary>Additive step applied per mouse-wheel notch.</summary>
    public double WheelStep
    {
        get => (double)GetValue(WheelStepProperty);
        set => SetValue(WheelStepProperty, value);
    }

    protected override void OnAttached()
    {
        AssociatedObject.LayoutTransform = _transform;
        ApplyZoom();
        AssociatedObject.MouseWheel += OnMouseWheel;
    }

    protected override void OnDetaching() => AssociatedObject.MouseWheel -= OnMouseWheel;

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ZoomLevel += e.Delta > 0 ? WheelStep : -WheelStep;
        e.Handled = true;
    }

    private void ApplyZoom()
    {
        _transform.ScaleX = ZoomLevel;
        _transform.ScaleY = ZoomLevel;
    }

    private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ZoomBehavior)d).ApplyZoom();

    private static void OnZoomLimitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        d.CoerceValue(ZoomLevelProperty);

    private static object CoerceZoom(DependencyObject d, object baseValue)
    {
        var behavior = (ZoomBehavior)d;
        return Math.Clamp((double)baseValue, behavior.MinZoom, behavior.MaxZoom);
    }
}
