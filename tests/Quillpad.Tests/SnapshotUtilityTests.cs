namespace Quillpad.Tests
{
    using System;
    using System.Linq;
    using Notepads.Utilities;
    using Xunit;

    public class SnapshotUtilityTests
    {
        private static string Name(int minute, SnapshotKind kind = SnapshotKind.Manual) =>
            SnapshotUtility.GetFileName(new DateTime(2026, 11, 23, 16, minute, 0, DateTimeKind.Utc), kind);

        [Fact]
        public void NamesReadBack()
        {
            var time = new DateTime(2026, 11, 23, 16, 45, 12, 123, DateTimeKind.Utc);
            var name = SnapshotUtility.GetFileName(time, SnapshotKind.BeforeReplaceAll);

            Assert.Equal("20261123-164512-123-BeforeReplaceAll.txt", name);
            var info = SnapshotUtility.Parse(name);
            Assert.Equal(time, info.TimeUtc);
            Assert.Equal(DateTimeKind.Utc, info.TimeUtc.Kind);
            Assert.Equal(SnapshotKind.BeforeReplaceAll, info.Kind);
        }

        [Theory]
        [InlineData("notes.txt")]
        [InlineData("20261123-164512-123-Unknown.txt")]
        [InlineData("20261123-164512-123-Manual.md")]
        [InlineData("2026-11-23-Manual.txt")]
        [InlineData("")]
        public void OtherFilesAreNotSnapshots(string fileName)
        {
            Assert.Null(SnapshotUtility.Parse(fileName));
        }

        [Fact]
        public void ANameWindowsMadeUniqueStillReads()
        {
            Assert.Equal(SnapshotKind.Manual, SnapshotUtility.Parse("20261123-164512-123-Manual (2).txt").Kind);
        }

        [Fact]
        public void ListsNewestFirst()
        {
            var snapshots = SnapshotUtility.GetSnapshots(new[] { Name(1), "desktop.ini", Name(3, SnapshotKind.BeforeReload), Name(2) });

            Assert.Equal(new[] { Name(3, SnapshotKind.BeforeReload), Name(2), Name(1) }, snapshots.Select(s => s.FileName));
        }

        [Fact]
        public void KeepsTheNewestAndDeletesTheRest()
        {
            var names = Enumerable.Range(0, 25).Select(minute => Name(minute)).ToList();
            var toDelete = SnapshotUtility.GetFilesToDelete(names, SnapshotUtility.MaxSnapshotsPerFile);

            Assert.Equal(5, toDelete.Count);
            Assert.Equal(Enumerable.Range(0, 5).Select(minute => Name(minute)).Reverse(), toDelete);
        }

        [Fact]
        public void NothingToDeleteBelowTheLimit()
        {
            Assert.Empty(SnapshotUtility.GetFilesToDelete(new[] { Name(1), Name(2) }, 20));
        }

        [Fact]
        public void FoldersFollowThePathOrTheTab()
        {
            var id = Guid.NewGuid();
            Assert.Equal(SnapshotUtility.GetFolderName(@"C:\A\b.md", id), SnapshotUtility.GetFolderName(@"c:\a\B.MD", Guid.NewGuid()));
            Assert.StartsWith("Untitled-", SnapshotUtility.GetFolderName(null, id));
            Assert.NotEqual(SnapshotUtility.GetFolderName(null, id), SnapshotUtility.GetFolderName(null, Guid.NewGuid()));
        }
    }
}
