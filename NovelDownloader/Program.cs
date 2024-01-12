using System.Net;
using System.Text.Json;
using CommandLine;
using NovelDownloader;
using NovelDownloader.Core;
using NovelDownloader.Provider;

CookieContainer cookieContainer = new();
HttpClientHandler handler = new()
{
    CookieContainer = cookieContainer,
    AutomaticDecompression = DecompressionMethods.GZip,
    // ServerCertificateCustomValidationCallback = (message, certificate2, chain, ssl) => true
};
HttpClient client = new(handler);

List<INovelProvider> providers =
[
    new LiNovelProvider()
];

var parser = Parser.Default.ParseArguments<CommandOptions>(args)
    .WithNotParsed(error =>
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("has some errors: {0}", error);
    });

await parser.WithParsedAsync(async o =>
{
    var address = o.Address ?? throw new ArgumentException("no address.");
    var cookies = o.Cookies ?? "";
    var output = o.Output ?? Directory.GetCurrentDirectory();

    var provider = providers.Find(it => it.Support(address))
                   ?? throw new ArgumentException($"not support {address}");

    if (File.Exists(cookies))
    {
        var json = File.ReadAllText(cookies);
        var cookieList = JsonSerializer.Deserialize<List<Cookie>>(json)
                         ?? Enumerable.Empty<Cookie>();
        foreach (var cookie in cookieList) cookieContainer.Add(cookie);
    }

    Console.WriteLine($"""
                       address: {address}
                       cookie: {cookies}
                       output: {output}
                       """);

    var downloader = new Downloader(client, provider, address) { DownloadPath = output };

    await downloader.AnalyzeNovels();
    await downloader.Download();
});