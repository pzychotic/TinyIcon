using TinyIcon.Behaviors;

namespace TinyIcon.Tests.Behaviors;

[TestFixture]
public class PanBehaviorTests
{
    [Test]
    public void MinVisibleFraction_DefaultsToAQuarter()
    {
        var behavior = new PanBehavior();

        Assert.That(behavior.MinVisibleFraction, Is.EqualTo(0.25));
    }

    [Test]
    public void ClampAxis_CenteredOffset_IsUnchanged()
    {
        var clamped = PanBehavior.ClampAxis(0.0, content: 100, viewport: 200, minVisibleFraction: 0.25);

        Assert.That(clamped, Is.EqualTo(0.0));
    }

    [Test]
    public void ClampAxis_SmallContent_KeepsTheRequiredFractionVisible()
    {
        // content 100 in a 200 viewport, 25% (=25px) must stay in view.
        // max = (100 + 200) / 2 - min(0.25*100, 200) = 150 - 25 = 125.
        var clamped = PanBehavior.ClampAxis(1000.0, content: 100, viewport: 200, minVisibleFraction: 0.25);

        Assert.That(clamped, Is.EqualTo(125.0));
    }

    [Test]
    public void ClampAxis_NegativeOffset_IsClampedSymmetrically()
    {
        var clamped = PanBehavior.ClampAxis(-1000.0, content: 100, viewport: 200, minVisibleFraction: 0.25);

        Assert.That(clamped, Is.EqualTo(-125.0));
    }

    [Test]
    public void ClampAxis_ContentLargerThanViewport_LimitsToFullyCoverTheViewport()
    {
        // When 25% of the content exceeds the viewport, the offset is capped so the viewport stays fully covered.
        // content 1000, viewport 200: max = (1000 + 200) / 2 - min(250, 200) = 600 - 200 = 400.
        var clamped = PanBehavior.ClampAxis(9999.0, content: 1000, viewport: 200, minVisibleFraction: 0.25);

        Assert.That(clamped, Is.EqualTo(400.0));
    }

    [Test]
    public void ClampAxis_OffsetWithinRange_IsUnchanged()
    {
        var clamped = PanBehavior.ClampAxis(50.0, content: 100, viewport: 200, minVisibleFraction: 0.25);

        Assert.That(clamped, Is.EqualTo(50.0));
    }

    [Test]
    public void ClampAxis_UnmeasuredElement_PinsToCenter()
    {
        var clamped = PanBehavior.ClampAxis(80.0, content: 0, viewport: 0, minVisibleFraction: 0.25);

        Assert.That(clamped, Is.EqualTo(0.0));
    }
}
