namespace Quillpad.Tests
{
    using System.Linq;
    using Notepads.Utilities;
    using Xunit;
    using static Notepads.Utilities.MarkdownLiveStyleParser;

    public class MarkdownLiveStyleParserTests
    {
        private const MarkdownLineContext N = MarkdownLineContext.Normal;
        private const MarkdownLineContext F = MarkdownLineContext.Fence;
        private const MarkdownLineContext C = MarkdownLineContext.InCode;

        // The editor keeps "\r" line endings and splits on them, the same as here
        private static MarkdownLineContext[] Contexts(params string[] lines) => GetLineContexts(lines);

        private static byte[] Style(string line, MarkdownLineContext context = MarkdownLineContext.Normal)
        {
            var styles = new byte[line.Length];
            StyleLine(line, context, styles);
            return styles;
        }

        private static byte Color(byte style) => (byte)(style & ColorMask);

        // Headings

        [Fact]
        public void HeadingMarkersAreDimAndTheTextIsBold()
        {
            var s = Style("## Chapter 2");
            Assert.Equal(ColorDim, Color(s[0]));
            Assert.Equal(ColorDim, Color(s[1]));
            Assert.All(s.Skip(3), b => Assert.True((b & Bold) != 0));
        }

        [Theory]
        [InlineData("#hashtag")]
        [InlineData("####### seven is too many")]
        public void NotAHeading(string line)
        {
            Assert.All(Style(line), b => Assert.Equal(0, b & Bold));
        }

        // Emphasis

        [Theory]
        [InlineData("*soft*", Italic)]
        [InlineData("_soft_", Italic)]
        [InlineData("**loud**", Bold)]
        [InlineData("__loud__", Bold)]
        [InlineData("***both***", Bold | Italic)]
        [InlineData("~~gone~~", Strikethrough)]
        public void EmphasisFlagsTheWholeRun(string line, int flags)
        {
            var s = Style(line);
            Assert.All(s, b => Assert.Equal(flags, b & flags));
            Assert.Equal(ColorDim, Color(s[0]));
            Assert.Equal(ColorDim, Color(s[s.Length - 1]));
        }

        [Fact]
        public void UnderscoresInsideAWordAreNotEmphasis()
        {
            Assert.All(Style("snake_case_name"), b => Assert.Equal(0, b));
        }

        [Fact]
        public void BoldAndItalicOnOneLineDoNotShareMarkers()
        {
            var line = "**a** and *b*";
            var s = Style(line);
            Assert.True((s[2] & Bold) != 0);
            Assert.Equal(0, s[2] & Italic);
            Assert.True((s[line.IndexOf('b')] & Italic) != 0);
            Assert.Equal(0, s[line.IndexOf('b')] & Bold);
        }

        // Code spans

        [Fact]
        public void CodeSpanHidesEmphasis()
        {
            var line = "use `x*y*z` here";
            var s = Style(line);
            Assert.Equal(ColorDim, Color(s[4]));
            Assert.All(s.Skip(5).Take(5), b => Assert.Equal(ColorCode, Color(b)));
            Assert.All(s, b => Assert.Equal(0, b & Italic));
        }

        [Fact]
        public void UnclosedBacktickIsPlainText()
        {
            Assert.All(Style("a `lonely tick"), b => Assert.Equal(0, b));
        }

        // Fences

        [Fact]
        public void ClosedBacktickFenceIsCode()
        {
            Assert.Equal(new[] { N, F, C, C, F, N }, Contexts("text", "```", "code", "", "```", "after"));
        }

        [Fact]
        public void UnclosedBacktickFenceIsNotCode()
        {
            Assert.Equal(new[] { N, N, N, N }, Contexts("text", "```", "the rest of the chapter", "and more"));
        }

        [Fact]
        public void ShorterFenceDoesNotCloseALongerOne()
        {
            Assert.Equal(new[] { F, C, C, F }, Contexts("````", "```", "code", "````"));
        }

        [Fact]
        public void FenceWithAnUnclosedLongerOpenerBelowAClosedOneKeepsTheClosedOne()
        {
            Assert.Equal(new[] { F, C, F, N, N }, Contexts("```", "code", "```", "````", "prose"));
        }

        [Fact]
        public void BacktickLineWithABacktickInTheInfoStringIsNotAFence()
        {
            Assert.Equal(new[] { N, N, N }, Contexts("``` a ` b", "prose", "```"));
        }

        [Fact]
        public void TildeSceneBreaksAreNotFences()
        {
            Assert.Equal(new[] { N, N, N, N, N }, Contexts("She left.", "~~~", "Morning came.", "~~~", "Night."));
        }

        [Fact]
        public void TildeLineDoesNotCloseABacktickFence()
        {
            Assert.Equal(new[] { F, C, F }, Contexts("```", "~~~", "```"));
        }

        // Line endings

        [Theory]
        [InlineData("# Title\r\n\r\n```\r\ncode\r\n```\r\nprose *it*\r\n")]
        [InlineData("# Title\n\n```\ncode\n```\nprose *it*\n")]
        public void CrlfAndLfGiveTheSameResultAfterTheEditorNormalizesThem(string text)
        {
            var lines = LineEndingUtility.ApplyLineEnding(text, LineEnding.Cr).Split('\r');
            Assert.Equal(new[] { N, N, F, C, F, N, N }, GetLineContexts(lines));
            Assert.True((Style(lines[5])[8] & Italic) != 0);
        }

        [Fact]
        public void FenceStillClosesWhenALineKeepsATrailingCarriageReturn()
        {
            Assert.Equal(new[] { F, C, F }, Contexts("```\r", "code\r", "```\r"));
        }

        // Surrogate pairs

        [Theory]
        [InlineData("**😀 grin**")]
        [InlineData("*𝒜 script*")]
        [InlineData("# 😀 Heading")]
        public void SurrogatePairsAreNeverSplitAcrossStyles(string line)
        {
            var s = Style(line);
            for (var i = 0; i < line.Length; i++)
            {
                if (char.IsHighSurrogate(line[i])) Assert.Equal(s[i], s[i + 1]);
            }
        }

        [Fact]
        public void EmojiBeforeEmphasisKeepsOffsetsInUtf16Units()
        {
            var line = "😀 *a*";
            var s = Style(line);
            Assert.Equal(0, s[0]);
            Assert.Equal(ColorDim, Color(s[3]));
            Assert.True((s[4] & Italic) != 0);
        }
    }
}
