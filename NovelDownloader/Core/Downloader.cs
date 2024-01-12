using System.Text.Json;

namespace NovelDownloader.Core;

public class Downloader
{
    private readonly HttpClient _httpClient;
    private readonly Func<List<int>, Task> _process;
    private readonly INovelProvider _provider;
    private readonly string _address;
    private readonly SemaphoreSlim _parallelImage = new(3);

    private List<NovelInfo.INovel> _novels = [];

    public string DownloadPath { get; init; } = Directory.GetCurrentDirectory();

    public Downloader(HttpClient httpClient, INovelProvider provider, string address)
    {
        _httpClient = httpClient;
        _provider = provider;
        _address = address;

        _process = provider.NovelType switch
        {
            INovelProvider.Type.Standalone => ProcessStandaloneNovel,
            INovelProvider.Type.Chapter => ProcessChapterNovel,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public async Task AnalyzeNovels()
    {
        _novels = await _provider.AnalyzeNovels(_httpClient, _address);
    }

    public async Task Download()
    {
        await Download(_novels.Select((_, i) => i).ToList());
    }

    public async Task Download(List<int> novelIndex)
    {
        if (novelIndex.ToList().Find(i => i >= _novels.Count) is var index && index > _novels.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(novelIndex), index, "download index is out of list");
        }

        await _process.Invoke(novelIndex);
    }

    private async Task ProcessStandaloneNovel(List<int> downloadIndex)
    {
        var novels = _novels.OfType<NovelInfo.StandaloneNovel>().ToList();
        if (novels.Count == 0) throw new ArgumentException("download list is empty");

        throw new NotImplementedException();
    }

    private async Task ProcessChapterNovel(List<int> downloadIndex)
    {
        var novels = _novels.OfType<NovelInfo.ChapterNovel>().ToList();
        if (novels.Count == 0) throw new ArgumentException("download list is empty");

        foreach (var (novel, ni) in novels.Select((n, i) => (n, i)))
        {
            if (!downloadIndex.Contains(ni)) continue;
            var novelName = novel.Name;
            var novelTitle = novel.Title;
            var chapters = novel.Chapters;

            var savePath = $"{DownloadPath}/{novelName}/{novelTitle}";
            if (!Path.Exists(savePath)) Directory.CreateDirectory(savePath);

            if (File.Exists($"{savePath}/meta.json"))
            {
                Console.WriteLine("{0} has download.", savePath);
                continue;
            }

            foreach (var (chapter, ci) in chapters.Select((c, i) => (c, i)))
            {
                if (!chapter.Address.StartsWith("http")
                    && _provider.FixAddress(novels, ni, ci) is var fixedAddress
                    && !string.IsNullOrEmpty(fixedAddress))
                {
                    chapter.Address = fixedAddress;
                }

                if (!chapter.Address.StartsWith("http"))
                {
                    chapter.Status = NovelInfo.Chapter.ChapterStatus.Failed;
                    chapter.Context = $"## [{chapter.Name}]({novel.Address}) DOWNLOAD FAILED";
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[{0} | {1} | {2}] get content failed.", novel.Name, novel.Title, chapter.Name);
                    Console.ResetColor();
                }

                if (chapter.Status == NovelInfo.Chapter.ChapterStatus.Ready)
                {
                    Console.WriteLine("start download [{0} | {1} | {2}]: {3}",
                        novel.Name, novel.Title, chapter.Name, chapter.Address);
                    await ProcessChapter(chapter);
                }
            }

            List<NovelInfo.ImageInfo> images = [..chapters.SelectMany(c => c.Images)];
            if (novel.Cover != null) images.Add(novel.Cover);
            var tasks = images
                .DistinctBy(img => img.Address)
                .Select(img =>
                {
                    Console.WriteLine("download image [{0} | {1} | {2}]: {3}",
                        novel.Name, novel.Title, img.FileName, img.Address);
                    return DownloadImage(img, savePath);
                });


            var article = File.OpenWrite($"{savePath}/article.md");
            StreamWriter writer = new(article);
            foreach (var chapter in chapters)
            {
                Console.WriteLine("write chapter [{0} | {1} | {2}. {3}]",
                    novel.Name, novel.Title, chapter.Seq, chapter.Name);
                await writer.WriteLineAsync($"# {chapter.Seq}. {chapter.Name}\n");
                await writer.WriteLineAsync(chapter.Context);
                await writer.WriteLineAsync("\n");
                await writer.FlushAsync();

                chapter.Context = string.Empty;
            }

            writer.Close();
            article.Close();

            var meta = _provider.GenerateMetaInfo(novel);
            if (!string.IsNullOrEmpty(meta)) await File.WriteAllTextAsync($"{savePath}/meta.json", meta);

            var pandocCommand = $"pandoc .\\{novel.Title}\\article.md -o \"{novel.Name} {novel.Title}.epub\"" +
                                " --epub-cover-image=cover.jpg" +
                                " --from markdown+hard_line_breaks" +
                                $" --metadata title=\"{novel.Name} {novel.Title}\"" +
                                $" --metadata author=\"{novel.Author ?? string.Empty}\"";
            var shellExt = OperatingSystem.IsWindows() ? "bat" : "sh";
            await File.WriteAllTextAsync($"{savePath}/pandoc2epub.{shellExt}", string.Join("\n", pandocCommand));

            await Task.WhenAll(tasks);

            Console.WriteLine("download success");
        }
    }

    private async Task<NovelInfo.Chapter> ProcessChapter(NovelInfo.Chapter chapter, bool onlyAnalyze = false)
    {
        NovelInfo.ChapterProcessResult result = new(true, chapter.Address);
        var retry = 0;
        while (result.NextPage && retry < 5)
        {
            string chapterHtml = "";
            try
            {
                Console.WriteLine("get page: {0}", result.NextAddress);
                var request = _provider.CreateRequest(result.NextAddress!);
                var response = await _httpClient.SendAsync(request);
                chapterHtml = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                retry++;
                continue;
            }

            result = _provider.ProcessChapter(chapterHtml);
            if (!string.IsNullOrEmpty(result.PrevAddress)) chapter.PrevChapterAddress = result.PrevAddress;
            if (!onlyAnalyze && !string.IsNullOrEmpty(result.Content)) chapter.Context += result.Content;
            if (!onlyAnalyze && result.Images != null) chapter.Images.AddRange(result.Images);

            retry = 0;
            await Task.Delay(_provider.Delay);
        }

        chapter.NextChapterAddress = result.NextAddress;
        chapter.Status = NovelInfo.Chapter.ChapterStatus.Success;

        return chapter;
    }


    private Task DownloadImage(NovelInfo.ImageInfo image, string savePath) => Task.Run(async () =>
    {
        await _parallelImage.WaitAsync();
        var imageFilePath = $"{savePath}/{image.FileName}";
        var request = _provider.CreateRequest(image.Address);
        var response = await _httpClient.SendAsync(request);
        var bodyStream = await response.Content.ReadAsStreamAsync();
        var imageStream = File.OpenWrite(imageFilePath);
        await bodyStream.CopyToAsync(imageStream);
        _parallelImage.Release();
    });
}