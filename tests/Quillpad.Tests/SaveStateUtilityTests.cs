namespace Quillpad.Tests
{
    using Notepads.Utilities;
    using Xunit;

    public class SaveStateUtilityTests
    {
        [Fact]
        public void TabIsCleanWhenNothingWasTypedDuringTheSave()
        {
            Assert.False(SaveStateUtility.IsModifiedAfterSave("Chapter one\rShe left.", "Chapter one\rShe left."));
        }

        [Fact]
        public void TabStaysModifiedWhenTextWasTypedDuringTheSave()
        {
            Assert.True(SaveStateUtility.IsModifiedAfterSave("She left.", "She left. Then"));
        }

        [Fact]
        public void TabStaysModifiedWhenTextWasDeletedDuringTheSave()
        {
            Assert.True(SaveStateUtility.IsModifiedAfterSave("She left.", "She left"));
        }

        [Fact]
        public void ComparesExactlyNotByCaseOrCulture()
        {
            Assert.True(SaveStateUtility.IsModifiedAfterSave("she left", "She left"));
            Assert.True(SaveStateUtility.IsModifiedAfterSave("é", "é")); // Same letter, different characters
        }

        [Fact]
        public void EmptyFileSavedWithNoTyping()
        {
            Assert.False(SaveStateUtility.IsModifiedAfterSave(string.Empty, string.Empty));
        }
    }
}
