using CommandLine;

namespace NovelDownloader;

public class CommandOptions
{
    
    [Value(0)] 
    public string? Address { get; set; }
    
    [Option('c',"cookie", Required = false)] 
    public string? Cookies { get; set; }
    
    [Option('o', "output", Required = false)] 
    public string? Output { get; set; }
    
}