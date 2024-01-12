using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace NovelDownloader.Core;

public interface INovelProvider
{
    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public enum Type
    {
        Standalone,
        Chapter
    }

    public Type NovelType { get; }
    public string BaseAddress { get; }

    public int Delay { get; }

    public bool Support(string url);

    public HttpRequestMessage CreateRequest(string address, string? cookieStr = null);

    public Task<List<NovelInfo.INovel>> AnalyzeNovels(HttpClient client, string address);

    public string FixAddress(IList<NovelInfo.ChapterNovel> novels, int nIndex, int cIndex);

    public NovelInfo.ChapterProcessResult ProcessChapter(string html);

    public string GenerateMetaInfo(NovelInfo.INovel novel);
}