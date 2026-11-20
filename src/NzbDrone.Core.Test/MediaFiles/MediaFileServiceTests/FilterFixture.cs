using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.MediaFileServiceTests
{
    [TestFixture]
    public class FilterFixture : FileSystemTest<MediaFileService>
    {
        private DateTime _lastWrite = new DateTime(2019, 1, 1);

        private List<IFileInfo> GivenFiles(string[] files)
        {
            foreach (var file in files)
            {
                FileSystem.AddFile(file, new MockFileData(string.Empty) { LastWriteTime = _lastWrite });
            }

            return files.Select(x => DiskProvider.GetFileInfo(x)).ToList();
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_return_all_files_if_no_existing_files(FilterFilesType filter)
        {
            var files = GivenFiles(new[]
                {
                    "C:\\file1.avi".AsOsAgnostic(),
                    "C:\\file2.avi".AsOsAgnostic(),
                    "C:\\file3.avi".AsOsAgnostic()
                });

            Mocker.GetMock<IMediaFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<BookFile>());

            Subject.FilterUnchangedFiles(files, filter).Should().BeEquivalentTo(files);
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_return_nothing_if_all_files_exist(FilterFilesType filter)
        {
            var files = GivenFiles(new[]
                {
                    "C:\\file1.avi".AsOsAgnostic(),
                    "C:\\file2.avi".AsOsAgnostic(),
                    "C:\\file3.avi".AsOsAgnostic()
                });

            Mocker.GetMock<IMediaFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(files.Select(f => new BookFile
                {
                    Path = f.FullName,
                    Modified = _lastWrite
                }).ToList());

            Subject.FilterUnchangedFiles(files, filter).Should().BeEmpty();
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_not_return_existing_files(FilterFilesType filter)
        {
            var files = GivenFiles(new[]
                {
                    "C:\\file1.avi".AsOsAgnostic(),
                    "C:\\file2.avi".AsOsAgnostic(),
                    "C:\\file3.avi".AsOsAgnostic()
                });

            Mocker.GetMock<IMediaFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<BookFile>
                {
                    new BookFile
                    {
                        Path = "C:\\file2.avi".AsOsAgnostic(),
                        Modified = _lastWrite
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(2);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().NotContain("C:\\file2.avi".AsOsAgnostic());
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_return_none_existing_files_ignoring_case(FilterFilesType filter)
        {
            WindowsOnly();

            var files = GivenFiles(new[]
                {
                    "C:\\file1.avi".AsOsAgnostic(),
                    "C:\\FILE2.avi".AsOsAgnostic(),
                    "C:\\file3.avi".AsOsAgnostic()
                });

            Mocker.GetMock<IMediaFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<BookFile>
                {
                    new BookFile
                    {
                        Path = "C:\\file2.avi".AsOsAgnostic(),
                        Modified = _lastWrite
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(2);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().NotContain("C:\\file2.avi".AsOsAgnostic());
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_return_none_existing_files_not_ignoring_case(FilterFilesType filter)
        {
            PosixOnly();

            var files = GivenFiles(new[]
                {
                    "C:\\file1.avi".AsOsAgnostic(),
                    "C:\\FILE2.avi".AsOsAgnostic(),
                    "C:\\file3.avi".AsOsAgnostic()
                });

            Mocker.GetMock<IMediaFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<BookFile>
                {
                    new BookFile
                    {
                        Path = "C:\\file2.avi".AsOsAgnostic(),
                        Modified = _lastWrite
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(3);
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_not_change_casing(FilterFilesType filter)
        {
            var files = GivenFiles(new[]
                {
                    "C:\\FILE1.avi".AsOsAgnostic()
                });

            Mocker.GetMock<IMediaFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<BookFile>());

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(1);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().NotContain(files.First().FullName.ToLower());
            Subject.FilterUnchangedFiles(files, filter).Should().Contain(files.First());
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_not_return_existing_file_if_size_unchanged(FilterFilesType filter)
        {
            FileSystem.AddFile("C:\\file1.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile("C:\\file2.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile("C:\\file3.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });

            var files = FileSystem.AllFiles.Select(x => DiskProvider.GetFileInfo(x)).ToList();

            Mocker.GetMock<IMediaFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<BookFile>
                {
                    new BookFile
                    {
                        Path = "C:\\file2.avi".AsOsAgnostic(),
                        Size = 10,
                        Modified = _lastWrite
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(2);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().NotContain("C:\\file2.avi".AsOsAgnostic());
        }

        [TestCase(FilterFilesType.Matched)]
        public void filter_unmatched_should_return_existing_file_if_unmatched(FilterFilesType filter)
        {
            FileSystem.AddFile("C:\\file1.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile("C:\\file2.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile("C:\\file3.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });

            var files = FileSystem.AllFiles.Select(x => DiskProvider.GetFileInfo(x)).ToList();

            Mocker.GetMock<IMediaFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<BookFile>
                {
                    new BookFile
                    {
                        Path = "C:\\file2.avi".AsOsAgnostic(),
                        Size = 10,
                        Modified = _lastWrite,
                        Edition = new LazyLoaded<Edition>(null)
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(3);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().Contain("C:\\file2.avi".AsOsAgnostic());
        }

        [TestCase(FilterFilesType.Matched)]
        public void filter_unmatched_should_not_return_existing_file_if_matched(FilterFilesType filter)
        {
            FileSystem.AddFile("C:\\file1.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile("C:\\file2.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile("C:\\file3.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });

            var files = FileSystem.AllFiles.Select(x => DiskProvider.GetFileInfo(x)).ToList();

            Mocker.GetMock<IMediaFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<BookFile>
                {
                    new BookFile
                    {
                        Path = "C:\\file2.avi".AsOsAgnostic(),
                        Size = 10,
                        Modified = _lastWrite,
                        Edition = Builder<Edition>.CreateNew().Build()
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(2);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().NotContain("C:\\file2.avi".AsOsAgnostic());
        }

        [TestCase(FilterFilesType.Matched)]
        public void filter_unmatched_should_not_return_unmatched_file_if_already_remote_searched(FilterFilesType filter)
        {
            FileSystem.AddFile("C:\\file1.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile("C:\\file2.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile("C:\\file3.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });

            var files = FileSystem.AllFiles.Select(x => DiskProvider.GetFileInfo(x)).ToList();

            Mocker.GetMock<IMediaFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<BookFile>
                {
                    new BookFile
                    {
                        Path = "C:\\file2.avi".AsOsAgnostic(),
                        Size = 10,
                        Modified = _lastWrite,
                        Edition = new LazyLoaded<Edition>(null),
                        LastRemoteSearchTime = _lastWrite
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(2);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().NotContain("C:\\file2.avi".AsOsAgnostic());
        }

        [TestCase(FilterFilesType.Matched)]
        public void filter_unmatched_should_return_remote_searched_file_if_changed(FilterFilesType filter)
        {
            FileSystem.AddFile("C:\\file1.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile("C:\\file2.avi".AsOsAgnostic(), new MockFileData("".PadRight(11)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile("C:\\file3.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });

            var files = FileSystem.AllFiles.Select(x => DiskProvider.GetFileInfo(x)).ToList();

            Mocker.GetMock<IMediaFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<BookFile>
                {
                    new BookFile
                    {
                        Path = "C:\\file2.avi".AsOsAgnostic(),
                        Size = 10,
                        Modified = _lastWrite,
                        Edition = new LazyLoaded<Edition>(null),
                        LastRemoteSearchTime = _lastWrite
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(3);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().Contain("C:\\file2.avi".AsOsAgnostic());
        }

        private List<IFileInfo> GivenFile2Changed()
        {
            FileSystem.AddFile("C:\\file1.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile("C:\\file2.avi".AsOsAgnostic(), new MockFileData("".PadRight(11)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile("C:\\file3.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });

            return FileSystem.AllFiles.Select(x => DiskProvider.GetFileInfo(x)).ToList();
        }

        private void GivenKeepUnmatchedFilesUnmatched()
        {
            Mocker.GetMock<NzbDrone.Core.Configuration.IConfigService>()
                .SetupGet(c => c.KeepUnmatchedFilesUnmatched)
                .Returns(true);
        }

        private void GivenExistingFile2(BookFile file)
        {
            file.Path = "C:\\file2.avi".AsOsAgnostic();
            file.Size = 10;
            file.Modified = _lastWrite;

            Mocker.GetMock<IMediaFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<BookFile> { file });
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void keep_unmatched_should_not_return_changed_unmatched_file_already_remote_searched(FilterFilesType filter)
        {
            GivenKeepUnmatchedFilesUnmatched();
            var files = GivenFile2Changed();
            var searched = _lastWrite.AddDays(-1);
            GivenExistingFile2(new BookFile { Edition = new LazyLoaded<Edition>(null), LastRemoteSearchTime = searched });

            var result = Subject.FilterUnchangedFiles(files, filter);

            result.Should().HaveCount(2);
            result.Select(x => x.FullName).Should().NotContain("C:\\file2.avi".AsOsAgnostic());

            // the new size is recorded so the file isn't seen as changed again, and the stamp is kept
            Mocker.GetMock<IMediaFileRepository>()
                .Verify(c => c.UpdateMany(It.Is<List<BookFile>>(l => l.Count == 1 && l[0].Size == 11 && l[0].LastRemoteSearchTime == searched)), Times.Once());
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void keep_unmatched_should_not_update_unchanged_unmatched_file(FilterFilesType filter)
        {
            GivenKeepUnmatchedFilesUnmatched();
            FileSystem.AddFile("C:\\file2.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            var files = FileSystem.AllFiles.Select(x => DiskProvider.GetFileInfo(x)).ToList();
            GivenExistingFile2(new BookFile { Edition = new LazyLoaded<Edition>(null), LastRemoteSearchTime = _lastWrite });

            Subject.FilterUnchangedFiles(files, filter).Should().BeEmpty();

            Mocker.GetMock<IMediaFileRepository>()
                .Verify(c => c.UpdateMany(It.IsAny<List<BookFile>>()), Times.Never());
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void keep_unmatched_should_return_changed_unmatched_file_never_remote_searched(FilterFilesType filter)
        {
            GivenKeepUnmatchedFilesUnmatched();
            var files = GivenFile2Changed();
            GivenExistingFile2(new BookFile { Edition = new LazyLoaded<Edition>(null) });

            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().Contain("C:\\file2.avi".AsOsAgnostic());
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void keep_unmatched_should_return_changed_matched_file(FilterFilesType filter)
        {
            GivenKeepUnmatchedFilesUnmatched();
            var files = GivenFile2Changed();
            GivenExistingFile2(new BookFile { EditionId = 5, Edition = Builder<Edition>.CreateNew().Build(), LastRemoteSearchTime = _lastWrite });

            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().Contain("C:\\file2.avi".AsOsAgnostic());
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void keep_unmatched_off_should_return_changed_unmatched_file_already_remote_searched(FilterFilesType filter)
        {
            var files = GivenFile2Changed();
            GivenExistingFile2(new BookFile { Edition = new LazyLoaded<Edition>(null), LastRemoteSearchTime = _lastWrite });

            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().Contain("C:\\file2.avi".AsOsAgnostic());

            Mocker.GetMock<IMediaFileRepository>()
                .Verify(c => c.UpdateMany(It.IsAny<List<BookFile>>()), Times.Never());
        }

        [Test]
        public void keep_unmatched_should_not_apply_to_full_rescan()
        {
            GivenKeepUnmatchedFilesUnmatched();
            var files = GivenFile2Changed();
            GivenExistingFile2(new BookFile { Edition = new LazyLoaded<Edition>(null), LastRemoteSearchTime = _lastWrite });

            Subject.FilterUnchangedFiles(files, FilterFilesType.None).Should().HaveCount(3);
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_return_existing_file_if_size_changed(FilterFilesType filter)
        {
            FileSystem.AddFile("C:\\file1.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile("C:\\file2.avi".AsOsAgnostic(), new MockFileData("".PadRight(11)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile("C:\\file3.avi".AsOsAgnostic(), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });

            var files = FileSystem.AllFiles.Select(x => DiskProvider.GetFileInfo(x)).ToList();

            Mocker.GetMock<IMediaFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<BookFile>
                {
                    new BookFile
                    {
                        Path = "C:\\file2.avi".AsOsAgnostic(),
                        Size = 10,
                        Modified = _lastWrite
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(3);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().Contain("C:\\file2.avi".AsOsAgnostic());
        }
    }
}
