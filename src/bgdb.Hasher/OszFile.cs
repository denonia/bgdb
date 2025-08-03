using System.IO.Compression;
using System.Text;
using Coosu.Beatmap;
using Serilog;

namespace bgdb.Hasher;

public class BackgroundEntry
{
    public BackgroundEntry(string fileName, Stream content)
    {
        FileName = fileName;
        Content = content;
    }
    
    public string FileName { get; set; }
    public Stream Content { get; set; }
}

public class BasicMetadata
{
    public BasicMetadata(string artist, string title, string creator)
    {
        Artist = artist;
        Title = title;
        Creator = creator;
    }
    
    public string Artist { get; set; }
    public string Title { get; set; }
    public string Creator { get; set; }
}

public class OszFile : IDisposable, IAsyncDisposable
{
    private Stream _oszStream = new MemoryStream(); 
    private ZipArchive _archive;
    
    public OszFile(Stream oszStream)
    {
        ReadOszFromStream(oszStream);
        
        _archive = new ZipArchive(_oszStream);
    }

    private void ReadOszFromStream(Stream oszStream)
    {
        using var reader = new StreamReader(oszStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        
        var boundaryPrefix = "--------------------------"u8.ToArray();
        var prefixBuffer = new byte[boundaryPrefix.Length];

        oszStream.ReadExactly(prefixBuffer);
        var startsWithBoundary = prefixBuffer.SequenceEqual(boundaryPrefix);

        oszStream.Seek(0, SeekOrigin.Begin);

        if (startsWithBoundary)
        {
            var linesToSkip = 4;
            var lineCount = 0;
            int b;
            long pos = 0;
            var lastWasCR = false;

            while ((b = oszStream.ReadByte()) != -1)
            {
                pos++;
                if (b == '\n')
                {
                    lineCount++;
                    if (lineCount == linesToSkip)
                        break;
                }
                else if (b == '\r')
                {
                    lastWasCR = true;
                }
                else if (lastWasCR)
                {
                    if (b == '\n')
                    {
                        lineCount++;
                        if (lineCount == linesToSkip)
                            break;
                        pos++;
                    }
                    lastWasCR = false;
                }
            }

            oszStream.Seek(pos, SeekOrigin.Begin);
        }

        oszStream.CopyTo(_oszStream);
    }

    private async Task<OsuFile> ReadOsuFileAsync(Stream osuStream)
    {
        using var ms = new MemoryStream();
        await osuStream.CopyToAsync(ms); 
        ms.Seek(0, SeekOrigin.Begin);

        return OsuFile.ReadFromStream(ms);
    }

    public async Task<IEnumerable<string>> GetBackgroundFileNamesAsync(int mapsetId)
    {
        var fileNames = new HashSet<string>();
        
        foreach (var entry in _archive.Entries.Where(e => e.Name.EndsWith(".osu")))
        {
            await using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            await entryStream.CopyToAsync(ms); 
            ms.Seek(0, SeekOrigin.Begin);

            var osuFile = await ReadOsuFileAsync(ms);
            var fileName = osuFile.Events?.BackgroundInfo?.Filename;

            if (fileName is not null)
            {
                fileNames.Add(fileName);
                continue;
            }
            
            // The parser fails to find backgrounds for some old maps, so double-checking ourselves :/
            ms.Seek(0, SeekOrigin.Begin);
            using var sr = new StreamReader(ms);

            var bgFound = false;
            
            string? line;
            while ((line = await sr.ReadLineAsync()) != null)
            {
                if (line.StartsWith("0,0,\"") || line.StartsWith("0,-100000,\"") || line.StartsWith("0,-1,\""))
                {
                    var start = line.IndexOf('"');
                    var end = line.IndexOf('"', start + 1);
                    fileName = line.Substring(start + 1, end - start - 1);
                    fileNames.Add(fileName);
                    bgFound = true;
                    break;
                }
            }
            
            // Weird
            if (!bgFound)
            {
                string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".bmp" };
            
                if (_archive.Entries.Any(e =>
                        Array.Exists(imageExtensions,
                            ext => ext.Equals(Path.GetExtension(e.Name), StringComparison.InvariantCultureIgnoreCase))))
                {
                    Log.Warning("Background not defined but images found in {mapsetId}", mapsetId);
                }
            }
        }

        return fileNames;
    }

    public async Task<IEnumerable<BackgroundEntry>> GetBackgroundsAsync(int mapsetId)
    {
        var fileNames = await GetBackgroundFileNamesAsync(mapsetId);
        var entries = _archive.Entries
            .IntersectBy(fileNames, e => e.FullName, StringComparer.OrdinalIgnoreCase);
        return entries.Select(e => new BackgroundEntry(e.Name, e.Open()));
    }

    public async Task<BasicMetadata> GetMetadataAsync()
    {
        var entry = _archive.Entries.First(e => e.Name.EndsWith(".osu"));
        await using var entryStream = entry.Open();
        var osuFile = await ReadOsuFileAsync(entryStream);

        var meta = new BasicMetadata(osuFile.Metadata.Artist, osuFile.Metadata.Title, osuFile.Metadata.Creator);
        return meta;
    }

    public void Dispose()
    {
        _oszStream.Dispose();
        _archive.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _oszStream.DisposeAsync();
    }
}