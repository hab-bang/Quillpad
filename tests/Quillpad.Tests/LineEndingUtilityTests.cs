namespace Quillpad.Tests
{
    using Notepads.Utilities;
    using Xunit;

    public class LineEndingUtilityTests
    {
        [Theory]
        [InlineData("a\r\nb", LineEnding.Crlf)]
        [InlineData("a\rb", LineEnding.Cr)]
        [InlineData("a\nb", LineEnding.Lf)]
        [InlineData("no line break", LineEnding.Crlf)]
        public void DetectsLineEnding(string text, LineEnding expected)
        {
            Assert.Equal(expected, LineEndingUtility.GetLineEndingTypeFromText(text));
        }

        [Fact]
        public void AppliesLineEndingToMixedText()
        {
            Assert.Equal("a\nb\nc\nd", LineEndingUtility.ApplyLineEnding("a\r\nb\rc\nd", LineEnding.Lf));
            Assert.Equal("a\r\nb\r\nc", LineEndingUtility.ApplyLineEnding("a\nb\rc", LineEnding.Crlf));
        }
    }
}
