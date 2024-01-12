using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using NovelDownloader.Core;

namespace NovelDownloader.Provider;

public class LiNovelProvider : INovelProvider
{
    private const string QueryTitleLi = "body > .wrap > .container > .book-meta > h1";
    private const string QueryVolumeListLi = "#volume-list > .volume";
    private const string QueryVolumeTitleLi = ".volume-info > h2";
    private const string QueryVolumeCoverLi = ".volume-cover > img";
    private const string QueryChapterListLi = ".chapter-list > li > a";

    private static readonly Regex
        AddressRegex = new("^https:\\/\\/www.linovelib.com\\/novel\\/\\d+(.html|\\/catalog)$");

    private readonly Dictionary<string, string> _dictionary = new()
    {
        { "", "的" }, { "", "一" }, { "", "是" }, { "", "了" }, { "", "我" }, { "", "不" }, { "", "人" }, { "", "在" },
        { "", "他" }, { "", "有" }, { "", "这" }, { "", "个" }, { "", "上" }, { "", "们" }, { "", "来" }, { "", "到" },
        { "", "时" }, { "", "大" }, { "", "地" }, { "", "为" }, { "", "子" }, { "", "中" }, { "", "你" }, { "", "说" },
        { "", "生" }, { "", "国" }, { "", "年" }, { "", "着" }, { "", "就" }, { "", "那" }, { "", "和" }, { "", "要" },
        { "", "她" }, { "", "出" }, { "", "也" }, { "", "得" }, { "", "里" }, { "", "后" }, { "", "自" }, { "", "以" },
        { "", "会" }, { "", "家" }, { "", "可" }, { "", "下" }, { "", "而" }, { "", "过" }, { "", "天" }, { "", "去" },
        { "", "能" }, { "", "对" }, { "", "小" }, { "", "多" }, { "", "然" }, { "", "于" }, { "", "心" }, { "", "学" },
        { "", "么" }, { "", "之" }, { "", "都" }, { "", "好" }, { "", "看" }, { "", "起" }, { "", "发" }, { "", "当" },
        { "", "没" }, { "", "成" }, { "", "只" }, { "", "如" }, { "", "事" }, { "", "把" }, { "", "还" }, { "", "用" },
        { "", "第" }, { "", "样" }, { "", "道" }, { "", "想" }, { "", "作" }, { "", "种" }, { "", "开" }, { "", "美" },
        { "", "乳" }, { "", "阴" }, { "", "液" }, { "", "茎" }, { "", "欲" }, { "", "呻" }, { "", "肉" }, { "", "交" },
        { "", "性" }, { "", "胸" }, { "", "私" }, { "", "穴" }, { "", "淫" }, { "", "臀" }, { "", "舔" }, { "", "射" },
        { "", "脱" }, { "", "裸" }, { "", "骚" }, { "", "唇" }
    };

    private readonly HtmlDocument _htmlDocument = new()
    {
        OptionWriteEmptyNodes = true
    };

    public INovelProvider.Type NovelType => INovelProvider.Type.Chapter;
    public string BaseAddress => "https://www.linovelib.com";
    public int Delay => 700;

    public HttpRequestMessage CreateRequest(string address, string? cookieStr = null)
    {
        HttpRequestMessage request = new()
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(address),
            Headers =
            {
                {
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0"
                },
                { "Referer", BaseAddress }
            }
        };

        if (!string.IsNullOrEmpty(cookieStr)) request.Headers.Add("Cookie", cookieStr);

        return request;
    }

    public async Task<List<INovel>> AnalyzeNovels(HttpClient client, string address)
    {
        if (!AddressRegex.IsMatch(address)) throw new ArgumentException($"[{address}] is not match");

        var catalogUrl = address.Contains("catalog") ? address : address.Replace(".html", "/catalog");

        var request = CreateRequest(catalogUrl);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        _htmlDocument.LoadHtml(html);
        var document = _htmlDocument.DocumentNode;

        var titleNode = document.QuerySelector(QueryTitleLi);
        var authorNode = document.QuerySelector("div.container > div.book-meta > p > span:nth-child(1) > a");
        var volumeNodeList = document.QuerySelectorAll(QueryVolumeListLi);

        var name = titleNode.InnerText;
        var author = authorNode?.InnerText ?? string.Empty;

        List<INovel> list = [];
        foreach (var (volumeNode, seq) in volumeNodeList.Select((v, i) => (v, i)))
        {
            var vTitleNode = volumeNode.QuerySelector(QueryVolumeTitleLi);
            var coverNode = volumeNode.QuerySelector(QueryVolumeCoverLi);
            var chapterNodeList = volumeNode.QuerySelectorAll(QueryChapterListLi)
                                  ?? ImmutableList<HtmlNode>.Empty;

            var title = vTitleNode.InnerText ?? "";
            var coverSrc = coverNode.Attributes["data-original"].Value;
            var firstChapter = coverNode.ParentNode.Attributes["href"].Value;
            var novel = new ChapterNovel(seq, firstChapter, name, title) { Author = author };

            if (!string.IsNullOrEmpty(coverSrc) && !coverSrc.Contains("no-cover"))
            {
                Uri uri = new(coverSrc);
                var path = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
                var ext = Path.GetExtension(path);
                ImageInfo cover = new($"cover{ext}", coverSrc, BaseAddress);
                novel.Cover = cover;
            }

            var chapters = chapterNodeList.Select((c, i) =>
                {
                    var chapterName = c.InnerText
                        .Replace("《", " ")
                        .Replace("》", " ")
                        .Trim();
                    var src = c.Attributes["href"].Value;
                    if (src.Contains("javascript:")) src = firstChapter;

                    if (string.IsNullOrEmpty(src)) return null;
                    return new Chapter(BaseAddress + src, i, chapterName);
                })
                .Where(c => c != null)
                .OfType<Chapter>();
            novel.Chapters.AddRange(chapters);
            list.Add(novel);
        }

        return list;
    }


    public string FixAddress(
        IList<ChapterNovel> novels,
        int nIndex, int cIndex)
    {
        var currentNovel = novels[nIndex];
        var count = currentNovel.Chapters.Count;

        Chapter? prev = null;
        if (cIndex != 0) prev = currentNovel.Chapters[cIndex - 1];
        else if (nIndex != 0) prev = novels[nIndex - 1].Chapters.LastOrDefault();

        // Chapter? next = null;
        // if (cIndex < count - 1) next = currentNovel.Chapters[cIndex + 1];
        // else if (nIndex != novels.Count - 1) next = novels[nIndex + 1].Chapters.FirstOrDefault();

        return prev?.NextChapterAddress ?? string.Empty;
    }

    public ChapterProcessResult ProcessChapter(string html)
    {
        _htmlDocument.LoadHtml(html);
        var document = _htmlDocument.DocumentNode;
        StringBuilder builder = new();
        List<ImageInfo> images = [];

        foreach (var content in document.QuerySelectorAll(".read-content > *"))
            if (content.Name == "p" || content.Name == "br")
            {
                var text = content.InnerText;
                foreach (var (key, value) in _dictionary) text = text.Replace(key, value);

                builder.AppendLine(text);
            }
            else if (content.Name == "img")
            {
                var imgAddress = content.Attributes["data-src"]?.Value
                                 ?? content.Attributes["src"]?.Value;
                if (!string.IsNullOrEmpty(imgAddress) && !imgAddress.Contains("sloading"))
                {
                    Uri uri = new(imgAddress);
                    var uriPath = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
                    var name = uriPath.Replace("/", "_");
                    var img = new ImageInfo(name, imgAddress, BaseAddress);
                    images.Add(img);
                    builder.AppendLine($"![]({name})");
                }
            }
            else
            {
                builder.AppendLine();
            }

        var prev = document.QuerySelector(".mlfy_page > a:first-child");
        var next = document.QuerySelector(".mlfy_page > a:last-child");

        var prevAddress = BaseAddress + prev.Attributes["href"]?.Value;
        var nextAddress = BaseAddress + next.Attributes["href"]?.Value;
        var hasNext = nextAddress?.Contains("_") ?? false;

        return new ChapterProcessResult(hasNext, nextAddress, prevAddress, builder.ToString(), images);
    }

    public string GenerateMetaInfo(INovel novel)
    {
        var title = novel switch
        {
            ChapterNovel n => n.Title,
            _ => string.Empty
        };
        return JsonSerializer.Serialize(new
        {
            DownloadTime = DateTime.Now,
            novel.Name,
            novel.Seq,
            Title = title,
            Author = novel.Author ?? string.Empty,
            novel.Description
        }, INovelProvider.JsonSerializerOptions);
    }

    public bool Support(string url)
    {
        return url.StartsWith(BaseAddress);
    }
}