using System.Collections.Immutable;

namespace NovelDownloader.Core;

public class NovelInfo
{
    public class Novel(string address, string name)
    {
        public string Address { get; } = address;
        public string Name { get; } = name;
        public string Title { get; set; } = "";
        public string Context { get; set; } = "";
        public List<ImageInfo> ImageInfos { get; set; } = [];
        public List<Chapter> Chapters { get; set; } = [];
    }

    public interface INovel
    {
        public int Seq { get; }
        public string Address { get; }
        public string Name { get; }
        public string? Author { get; set; }
        public string? Description { get; set; }
    }

    public class StandaloneNovel(int seq, string address, string name) : INovel
    {
        public int Seq { get; } = seq;
        public string Address { get; } = address;
        public string Name { get; } = name;
        public string? Author { get; set; }
        public string? Description { get; set; }
        public string Context { get; set; } = "";
        public List<ImageInfo> ImageInfos { get; set; } = [];
    }

    public class ChapterNovel(int seq, string address, string name, string title) : INovel
    {
        public int Seq { get; } = seq;

        // address is the first chapter address on ChapterNovel
        public string Address { get; } = address;
        public string Name { get; } = name;
        public string? Author { get; set; }
        public string? Description { get; set; }
        public string Title { get; set; } = title;
        public ImageInfo? Cover { get; set; }
        public List<Chapter> Chapters { get; set; } = [];
    }

    public class Chapter(string address, int seq, string name = "")
    {
        public enum ChapterStatus
        {
            Failed,
            Ready,
            Success
        }

        public int Seq { get; } = seq;
        public string Address { get; set; } = address;
        public string Name { get; } = name;
        public string Context { get; set; } = "";
        public List<ImageInfo> Images { get; set; } = [];

        public ChapterStatus Status { get; set; } = ChapterStatus.Ready;
        public string? PrevChapterAddress { get; set; }
        public string? NextChapterAddress { get; set; }
    }

    public record struct ChapterProcessResult(
        bool NextPage,
        string? NextAddress,
        string? PrevAddress = null,
        string? Content = null,
        IList<ImageInfo>? Images = null);

    public record ImageInfo(string FileName, string Address, string? Referer = null);
}