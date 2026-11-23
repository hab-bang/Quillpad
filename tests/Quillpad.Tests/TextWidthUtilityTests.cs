namespace Quillpad.Tests
{
    using Notepads.Utilities;
    using Xunit;

    public class TextWidthUtilityTests
    {
        [Theory]
        [InlineData(TextWidth.Full, TextWidth.ThreeQuarters)]
        [InlineData(TextWidth.ThreeQuarters, TextWidth.Half)]
        [InlineData(TextWidth.Half, TextWidth.Full)]
        public void NextGoesNarrowerThenBackToFull(TextWidth current, TextWidth expected)
        {
            Assert.Equal(expected, TextWidthUtility.Next(current));
        }
    }
}
