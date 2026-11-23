// expect suppression: 2
using System.Diagnostics.CodeAnalysis;

public class Suppression
{
#pragma warning disable CS0168
    public void NoReason() { int unused; }
#pragma warning restore CS0168

#pragma warning disable CS0168 // The sample needs an unused local
    public void WithReason() { int unused; }
#pragma warning restore CS0168

    [SuppressMessage("Style", "IDE0060")]
    public void AttributeWithoutReason(int value) { }

    [SuppressMessage("Style", "IDE0060", Justification = "Called by reflection")]
    public void AttributeWithReason(int value) { }
}
