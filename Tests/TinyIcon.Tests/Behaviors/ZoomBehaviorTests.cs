using TinyIcon.Behaviors;

namespace TinyIcon.Tests.Behaviors;

[TestFixture]
public class ZoomBehaviorTests
{
    [Test]
    public void ZoomLevel_DefaultsToOneHundredPercent()
    {
        var behavior = new ZoomBehavior();

        Assert.That(behavior.ZoomLevel, Is.EqualTo(1.0));
    }

    [Test]
    public void ZoomLevel_IsCoercedAboveTheMaximum()
    {
        var behavior = new ZoomBehavior { MaxZoom = 8.0, ZoomLevel = 99.0 };

        Assert.That(behavior.ZoomLevel, Is.EqualTo(8.0));
    }

    [Test]
    public void ZoomLevel_IsCoercedBelowTheMinimum()
    {
        var behavior = new ZoomBehavior { MinZoom = 0.5, ZoomLevel = 0.01 };

        Assert.That(behavior.ZoomLevel, Is.EqualTo(0.5));
    }

    [Test]
    public void ZoomLevel_WithinRange_IsUnchanged()
    {
        var behavior = new ZoomBehavior { MinZoom = 0.1, MaxZoom = 16.0, ZoomLevel = 2.5 };

        Assert.That(behavior.ZoomLevel, Is.EqualTo(2.5));
    }

    [Test]
    public void LoweringMaxZoom_ReCoercesTheCurrentZoomLevel()
    {
        var behavior = new ZoomBehavior { ZoomLevel = 8.0 };

        behavior.MaxZoom = 4.0;

        Assert.That(behavior.ZoomLevel, Is.EqualTo(4.0));
    }

    [Test]
    public void RaisingMinZoom_ReCoercesTheCurrentZoomLevel()
    {
        var behavior = new ZoomBehavior { ZoomLevel = 0.2 };

        behavior.MinZoom = 0.5;

        Assert.That(behavior.ZoomLevel, Is.EqualTo(0.5));
    }
}
