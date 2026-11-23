namespace Quillpad.Tests
{
    using Notepads.Utilities;
    using Xunit;

    public class TypewriterUtilityTests
    {
        [Fact]
        public void PutsTheCaretLineInTheMiddle()
        {
            // Line from 1000 to 1020 in a 600 high view: its middle, 1010, lands at 300
            Assert.Equal(710, TypewriterUtility.GetCenteredVerticalOffset(1000, 20, 600));
        }

        [Fact]
        public void TheFirstLinesStayAtTheTop()
        {
            Assert.Equal(0, TypewriterUtility.GetCenteredVerticalOffset(40, 20, 600));
        }
    }
}
