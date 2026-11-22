namespace Quillpad.Tests
{
    using System.IO;
    using System.Text;
    using Notepads.Utilities;
    using Xunit;

    public class EncodingFitUtilityTests
    {
        private static readonly Encoding Windows1252;

        static EncodingFitUtilityTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Windows1252 = Encoding.GetEncoding(1252);
        }

        [Fact]
        public void CodePageHoldsItsOwnCharacters()
        {
            // Curly quotes, an em dash and accents are all in Windows-1252
            Assert.Equal(-1, EncodingFitUtility.FindFirstUnencodableIndex("“Café” — naïve", Windows1252));
        }

        [Fact]
        public void CodePageReportsTheFirstCharacterItLacks()
        {
            string text = "Hi\r한국어";
            int index = EncodingFitUtility.FindFirstUnencodableIndex(text, Windows1252);

            Assert.Equal(3, index);
            Assert.Equal("한", EncodingFitUtility.GetCharacterAt(text, index));
            Assert.Equal(2, EncodingFitUtility.GetLineNumber(text, index));
        }

        [Fact]
        public void EmojiIsReportedWhole()
        {
            string text = "ok 😀";
            int index = EncodingFitUtility.FindFirstUnencodableIndex(text, Windows1252);

            Assert.Equal(3, index);
            Assert.Equal("😀", EncodingFitUtility.GetCharacterAt(text, index));
        }

        [Fact]
        public void UnicodeEncodingsHoldEverything()
        {
            const string text = "한국어 😀 “quotes”";
            Assert.True(EncodingFitUtility.CanEncode(text, new UTF8Encoding(false)));
            Assert.True(EncodingFitUtility.CanEncode(text, new UTF8Encoding(true)));
            Assert.True(EncodingFitUtility.CanEncode(text, Encoding.Unicode));
            Assert.True(EncodingFitUtility.CanEncode(text, Encoding.BigEndianUnicode));
            Assert.True(EncodingFitUtility.CanEncode(text, Encoding.UTF32));
        }

        [Fact]
        public void CheckDoesNotChangeTheEncoding()
        {
            // The tab keeps using its encoding object, so the check must work on a copy
            EncodingFitUtility.CanEncode("한", Windows1252);
            Assert.Equal((byte)'?', Windows1252.GetBytes("한")[0]);
        }

        [Theory]
        [InlineData("one", 3, 1)]
        [InlineData("one\rtwo", 4, 2)]
        [InlineData("one\ntwo", 4, 2)]
        [InlineData("one\r\ntwo", 5, 2)]
        [InlineData("a\r\rb", 3, 3)]
        public void LineNumberCountsEachKindOfLineBreakOnce(string text, int index, int expectedLine)
        {
            Assert.Equal(expectedLine, EncodingFitUtility.GetLineNumber(text, index));
        }

        [Fact]
        public void BackupKeepsTheFileEncodingWhenItHoldsTheText()
        {
            Assert.Same(Windows1252, EncodingFitUtility.GetBackupEncoding("Café", Windows1252));
        }

        [Fact]
        public void BackupOfTextTheEncodingCannotHoldReadsBackWithTheFileEncoding()
        {
            const string text = "Draft: 한국어 😀";
            Encoding backupEncoding = EncodingFitUtility.GetBackupEncoding(text, Windows1252);
            Assert.Equal(65001, backupEncoding.CodePage);

            // Like FileSystemUtility: the bytes with the encoding's mark, read back with the tab's own encoding
            byte[] bytes = Concat(backupEncoding.GetPreamble(), backupEncoding.GetBytes(text));
            using (var reader = new StreamReader(new MemoryStream(bytes), Windows1252))
            {
                Assert.Equal(text, reader.ReadToEnd());
            }
        }

        private static byte[] Concat(byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            first.CopyTo(result, 0);
            second.CopyTo(result, first.Length);
            return result;
        }
    }
}
