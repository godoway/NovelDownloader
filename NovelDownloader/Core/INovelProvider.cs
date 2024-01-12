using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace NovelDownloader.Core;

public interface INovelProvider
{
    public enum Type
    {
        Standalone,
        Chapter
    }

    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public Type NovelType { get; }
    public string BaseAddress { get; }

    public int Delay { get; }

    public bool Support(string url);

    public HttpRequestMessage CreateRequest(string address, string? cookieStr = null);

    public Task<List<INovel>> AnalyzeNovels(HttpClient client, string address);

    public string FixAddress(IList<ChapterNovel> novels, int nIndex, int cIndex);

    public ChapterProcessResult ProcessChapter(string html);

    public string GenerateMetaInfo(INovel novel);
}