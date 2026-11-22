namespace Quillpad.Tests
{
    using System;
    using Notepads.Utilities;
    using Xunit;

    public class WorkspaceFolderLockTests
    {
        // A new folder per test, so tests that run at the same time never share a lock
        private static string NewFolderPath() => @"C:\Users\writer\Documents\Story-" + Guid.NewGuid().ToString("N");

        [Fact]
        public void SecondWindowCannotTakeAFolderThatIsOpen()
        {
            string folder = NewFolderPath();

            using (var first = WorkspaceFolderLock.TryAcquire(folder))
            {
                Assert.NotNull(first);
                Assert.Null(WorkspaceFolderLock.TryAcquire(folder));
            }
        }

        [Fact]
        public void SameFolderIsFoundWhateverItsCaseOrTrailingSlash()
        {
            string folder = NewFolderPath();

            using (WorkspaceFolderLock.TryAcquire(folder))
            {
                Assert.Null(WorkspaceFolderLock.TryAcquire(folder.ToUpperInvariant()));
                Assert.Null(WorkspaceFolderLock.TryAcquire(folder + @"\"));
                Assert.Null(WorkspaceFolderLock.TryAcquire(folder.Replace('\\', '/')));
            }
        }

        [Fact]
        public void FolderIsFreeAgainOnceItsWindowLetsGo()
        {
            string folder = NewFolderPath();

            WorkspaceFolderLock.TryAcquire(folder).Dispose();

            using (var again = WorkspaceFolderLock.TryAcquire(folder))
            {
                Assert.NotNull(again);
            }
        }

        [Fact]
        public void OtherFoldersStayFree()
        {
            string folder = NewFolderPath();

            using (WorkspaceFolderLock.TryAcquire(folder))
            using (var subfolder = WorkspaceFolderLock.TryAcquire(folder + @"\Chapters"))
            using (var other = WorkspaceFolderLock.TryAcquire(NewFolderPath()))
            {
                Assert.NotNull(subfolder);
                Assert.NotNull(other);
            }
        }

        [Fact]
        public void FolderWithoutPathAlwaysOpens()
        {
            // Nothing to compare, so nothing to block
            using (var first = WorkspaceFolderLock.TryAcquire(""))
            using (var second = WorkspaceFolderLock.TryAcquire(""))
            {
                Assert.NotNull(first);
                Assert.NotNull(second);
            }
        }

        [Fact]
        public void NameIsAValidMutexName()
        {
            string name = WorkspaceFolderLock.GetName(@"C:\Users\writer\Documents\Story");

            Assert.StartsWith("Quillpad-Folder-", name);
            Assert.DoesNotContain(@"\", name);
            Assert.True(name.Length < 260);
            Assert.Null(WorkspaceFolderLock.GetName("   "));
        }

        [Fact]
        public void SameFolderCheckIgnoresCaseAndTrailingSlash()
        {
            Assert.True(WorkspaceFolderLock.IsSameFolder(@"C:\Story", @"c:\story\"));
            Assert.False(WorkspaceFolderLock.IsSameFolder(@"C:\Story", @"C:\Story\Chapters"));
            Assert.False(WorkspaceFolderLock.IsSameFolder(null, @"C:\Story"));
        }
    }
}
