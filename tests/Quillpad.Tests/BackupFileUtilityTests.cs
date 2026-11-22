namespace Quillpad.Tests
{
    using Notepads.Utilities;
    using Xunit;

    public class BackupFileUtilityTests
    {
        private const string Token = "3f2c9d0e8b7a4c1d9e6f5a4b3c2d1e0f";

        [Fact]
        public void BackupFileInUseStays()
        {
            Assert.False(BackupFileUtility.ShouldDeleteFromBackupFolder(Token + "-Pending", usedBySession: true));
        }

        [Fact]
        public void BackupFileNoTabUsesIsDeleted()
        {
            Assert.True(BackupFileUtility.ShouldDeleteFromBackupFolder(Token + "-LastSaved", usedBySession: false));
        }

        [Fact]
        public void TempFileOfAStoppedBackupWriteIsDeleted()
        {
            Assert.True(BackupFileUtility.ShouldDeleteFromBackupFolder(Token + "-Pending" + BackupFileUtility.SaveTempFileSuffix, usedBySession: false));
            Assert.True(BackupFileUtility.ShouldDeleteFromBackupFolder(Token + "-Pending.QUILLPAD-SAVE.TMP", usedBySession: false));
        }

        [Fact]
        public void KeptBackupStays()
        {
            string kept = BackupFileUtility.GetKeptFileName(Token + "-Pending");

            Assert.Equal(Token + "-Pending-Corrupted.txt", kept);
            Assert.False(BackupFileUtility.ShouldDeleteFromBackupFolder(kept, usedBySession: false));
        }

        [Fact]
        public void OtherFilesWithAnExtensionStay()
        {
            Assert.False(BackupFileUtility.ShouldDeleteFromBackupFolder("notes.txt", usedBySession: false));
            Assert.False(BackupFileUtility.ShouldDeleteFromBackupFolder("NotepadsSessionData.json", usedBySession: false));
        }

        [Fact]
        public void EmptyNameIsNeverDeleted()
        {
            Assert.False(BackupFileUtility.ShouldDeleteFromBackupFolder(string.Empty, usedBySession: false));
            Assert.False(BackupFileUtility.ShouldDeleteFromBackupFolder(null, usedBySession: false));
        }

        [Theory]
        [InlineData("Chapter 3.md", "Untitled.txt", "Chapter 3.md")]
        [InlineData(null, "Untitled 2.txt", "Untitled 2.txt")]
        [InlineData("", "", "Untitled.txt")]
        [InlineData(null, null, "Untitled.txt")]
        public void TabNameIsTheFileNameThenThePlaceholder(string editingFileName, string placeholder, string expected)
        {
            Assert.Equal(expected, BackupFileUtility.GetTabName(editingFileName, placeholder, "Untitled.txt"));
        }
    }
}
