namespace Quillpad.Tests
{
    using System.Linq;
    using Notepads.Utilities;
    using Xunit;

    public class OutlineUtilityTests
    {
        [Fact]
        public void ListsHeadingsAndSceneBreaksWithTheirLines()
        {
            var text = "# Chapter One\r\rShe woke.\r\r***\r\rThe next morning, rain.\r\r## Part Two\rMore.";
            var outline = OutlineUtility.GetOutline(text);

            Assert.Equal(3, outline.Count);
            Assert.Equal((OutlineItemKind.Heading, 0, 1, "Chapter One"), (outline[0].Kind, outline[0].Line, outline[0].Level, outline[0].Title));
            Assert.Equal((OutlineItemKind.SceneBreak, 4, 2, "The next morning, rain."), (outline[1].Kind, outline[1].Line, outline[1].Level, outline[1].Title));
            Assert.Equal((OutlineItemKind.Heading, 8, 2, "Part Two"), (outline[2].Kind, outline[2].Line, outline[2].Level, outline[2].Title));
        }

        [Fact]
        public void SkipsCodeBlocks()
        {
            var text = "```\r# not a heading\r---\r```\r# Real";
            var outline = OutlineUtility.GetOutline(text);

            Assert.Single(outline);
            Assert.Equal("Real", outline[0].Title);
        }

        [Fact]
        public void LongTitlesAreShortened()
        {
            var title = new string('a', 100);
            var item = OutlineUtility.GetOutline("# " + title).Single();

            Assert.Equal(OutlineUtility.MaxTitleLength, item.Title.Length);
            Assert.EndsWith("…", item.Title);
        }

        [Fact]
        public void ASceneBreakAtTheEndHasNoTitle()
        {
            Assert.Equal(string.Empty, OutlineUtility.GetOutline("Text\n\n* * *\n").Single().Title);
        }

        [Fact]
        public void PlainTextHasNoOutline()
        {
            Assert.Empty(OutlineUtility.GetOutline("Just a paragraph.\nAnd #not a heading."));
        }
    }
}
