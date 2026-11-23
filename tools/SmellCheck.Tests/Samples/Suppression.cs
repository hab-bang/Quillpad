// expect suppression: 4
using System.Diagnostics.CodeAnalysis;

public class Suppression
{
#pragma warning disable CS0168
    public void NoReason() { int unused; }
#pragma warning restore CS0168

#pragma warning disable CS0168 // The sample needs an unused local
    public void WithReason() { int unused; }
#pragma warning restore CS0168

#pragma warning disable CS0168 //
    public void EmptyReason() { int unused; }
#pragma warning restore CS0168

    [SuppressMessage("Style", "IDE0060")]
    public void AttributeWithoutReason(int value) { }

    [SuppressMessage("Style", "IDE0060", Justification = "Called by reflection")]
    public void AttributeWithReason(int value) { }

    // The // in the URL is inside a string, so it is not a reason
    [SuppressMessage("Style", "IDE0060", MessageId = "see http://example.com")]
    public void AttributeWithUrl(int value) { }

    [SuppressMessage("Style", "IDE0060")] // Called by reflection
    public void AttributeWithComment(int value) { }
}
