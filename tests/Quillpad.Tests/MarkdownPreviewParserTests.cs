namespace Quillpad.Tests
{
    using System.Linq;
    using Notepads.Controls.Markdown;
    using Notepads.Utilities;
    using Xunit;

    /// <summary>
    /// The preview has its own parser (from the Windows Community Toolkit). These tests check that it
    /// agrees with the editor's live styling on what is code.
    /// </summary>
    public class MarkdownPreviewParserTests
    {
        private static MarkdownBlockType[] Blocks(string text)
        {
            var document = new MarkdownDocument();
            document.Parse(text);
            return document.Blocks.Select(b => b.Type).ToArray();
        }

        private static bool PreviewShowsCode(string text) => Blocks(text).Contains(MarkdownBlockType.Code);

        private static bool EditorShowsCode(string text)
        {
            var lines = LineEndingUtility.ApplyLineEnding(text, LineEnding.Cr).Split('\r');
            return MarkdownLiveStyleParser.GetLineContexts(lines).Any(c => c != MarkdownLineContext.Normal);
        }

        [Fact]
        public void ClosedBacktickFenceIsCode()
        {
            Assert.Equal(new[] { MarkdownBlockType.Paragraph, MarkdownBlockType.Code, MarkdownBlockType.Paragraph },
                Blocks("text\n\n```\ncode\n```\n\nafter"));
        }

        [Fact]
        public void UnclosedBacktickFenceIsNotCode()
        {
            Assert.False(PreviewShowsCode("text\n\n```\n\nthe rest of the chapter\n\nand more"));
        }

        [Fact]
        public void TabIndentedProseIsAParagraph()
        {
            Assert.Equal(new[] { MarkdownBlockType.Paragraph, MarkdownBlockType.Paragraph },
                Blocks("\tShe opened the door.\n\n\tThe hall was dark."));
        }

        [Fact]
        public void SpacesThenATabAreProseToo()
        {
            Assert.Equal(new[] { MarkdownBlockType.Paragraph }, Blocks("  \tShe opened the door."));
        }

        [Fact]
        public void FourSpaceIndentedCodeIsStillCode()
        {
            Assert.Equal(new[] { MarkdownBlockType.Code }, Blocks("    var x = 1;"));
        }

        [Theory]
        [InlineData("She left.\n\n~~~\n\nMorning came.\n\n~~~\n\nNight.")]
        [InlineData("She left.\n\n~~~\n\nMorning came.")]
        [InlineData("text\n\n```\n\nno closing fence")]
        [InlineData("text\n\n```\ncode\n```\n\nafter")]
        [InlineData("```\n~~~\n```")]
        [InlineData("\tShe opened the door.\n\n\tThe hall was dark.")]
        public void EditorAndPreviewAgreeOnCode(string text)
        {
            Assert.Equal(EditorShowsCode(text), PreviewShowsCode(text));
        }
    }
}
