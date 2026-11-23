namespace Quillpad.Tests
{
    using Notepads.Utilities;
    using Xunit;

    public class Ao3HtmlConverterTests
    {
        private static string Html(string markdown) => Ao3HtmlConverter.ToHtml(markdown);

        [Fact]
        public void BlankLinesSeparateParagraphs()
        {
            Assert.Equal("<p>One.</p>\n<p>Two.</p>\n", Html("One.\r\rTwo."));
            Assert.Equal("<p>One.</p>\n<p>Two.</p>\n", Html("One.\r\n\r\n\r\nTwo.\r\n"));
        }

        [Fact]
        public void ASingleLineBreakStaysALineBreak()
        {
            Assert.Equal("<p>Roses are red,<br />\nviolets are blue.</p>\n", Html("Roses are red,\nviolets are blue."));
        }

        [Theory]
        [InlineData("*soft*", "<p><em>soft</em></p>\n")]
        [InlineData("_soft_", "<p><em>soft</em></p>\n")]
        [InlineData("**loud**", "<p><strong>loud</strong></p>\n")]
        [InlineData("__loud__", "<p><strong>loud</strong></p>\n")]
        [InlineData("***both***", "<p><strong><em>both</em></strong></p>\n")]
        [InlineData("**a *b* c**", "<p><strong>a <em>b</em> c</strong></p>\n")]
        [InlineData("*a **b** c*", "<p><em>a <strong>b</strong> c</em></p>\n")]
        public void EmphasisBecomesEmAndStrong(string markdown, string expected)
        {
            Assert.Equal(expected, Html(markdown));
        }

        [Theory]
        [InlineData("snake_case_name", "<p>snake_case_name</p>\n")]
        [InlineData("5 * 3 * 2", "<p>5 * 3 * 2</p>\n")]
        [InlineData(@"\*not em\*", "<p>*not em*</p>\n")]
        [InlineData(@"a \\ b", @"<p>a \ b</p>" + "\n")]
        public void StarsAndUnderscoresThatAreNotEmphasisStay(string markdown, string expected)
        {
            Assert.Equal(expected, Html(markdown));
        }

        [Theory]
        [InlineData("***")]
        [InlineData("---")]
        [InlineData("~~~")]
        [InlineData("* * *")]
        [InlineData("  - - -  ")]
        [InlineData("_____")]
        public void SceneBreaksBecomeHr(string sceneBreak)
        {
            Assert.Equal("<p>Before.</p>\n<hr />\n<p>After.</p>\n", Html("Before.\n\n" + sceneBreak + "\n\nAfter."));
        }

        [Fact]
        public void ASceneBreakRightAfterAParagraphLineEndsIt()
        {
            Assert.Equal("<p>Before.</p>\n<hr />\n<p>After.</p>\n", Html("Before.\n---\nAfter."));
        }

        [Theory]
        [InlineData("# Chapter One", "<h2>Chapter One</h2>\n")]
        [InlineData("## Part *two* ##", "<h3>Part <em>two</em></h3>\n")]
        [InlineData("#hashtag", "<p>#hashtag</p>\n")]
        public void HeadingsStartAtH2(string markdown, string expected)
        {
            Assert.Equal(expected, Html(markdown));
        }

        [Fact]
        public void HtmlCharactersAreEscaped()
        {
            Assert.Equal("<p>Tom &amp; Jerry &lt;3 \"quoted\"</p>\n", Html("Tom & Jerry <3 \"quoted\""));
        }

        [Fact]
        public void NoClassesOrStyles()
        {
            var html = Html("# Title\n\nSome *text*.\n\n***\n\nMore **text**.");
            Assert.DoesNotContain("class=", html);
            Assert.DoesNotContain("style=", html);
        }

        [Fact]
        public void EmptyTextGivesNothing()
        {
            Assert.Equal(string.Empty, Html(""));
            Assert.Equal(string.Empty, Html(null));
        }

        [Fact]
        public void TheDocumentWrapsTheSameHtml()
        {
            var document = Ao3HtmlConverter.ToHtmlDocument("Hi *there*.", "Chapter <1>");
            Assert.StartsWith("<!DOCTYPE html>", document);
            Assert.Contains("<meta charset=\"utf-8\" />", document);
            Assert.Contains("<title>Chapter &lt;1&gt;</title>", document);
            Assert.Contains("<body>\n<p>Hi <em>there</em>.</p>\n</body>", document);
        }
    }
}
