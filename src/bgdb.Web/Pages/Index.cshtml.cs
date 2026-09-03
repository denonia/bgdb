using bgdb.Common;
using bgdb.Common.Models;
using bgdb.Common.Repositories;
using bgdb.Common.Services;
using bgdb.Web.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace bgdb.Web.Pages;

public class IndexModel : PageModel
{
    private readonly StatsService _statsService;
    private readonly SearchRepository _searchRepository;
    private readonly ImageSearchService _imageSearchService;

    public IndexModel(
        StatsService statsService, SearchRepository searchRepository, ImageSearchService imageSearchService)
    {
        _statsService = statsService;
        _searchRepository = searchRepository;
        _imageSearchService = imageSearchService;
    }

    [FromQuery] public string? SearchId { get; set; }
    [BindProperty] public IFormFile? ImageFile { get; set; }

    public string? SearchPreviewUrl => SearchId is null ? null : $"{Settings.BaseUrl}/img/search/{SearchId}";
    public string ImageBaseUrl => $"{Settings.BaseUrl}/img/thumb";

    public List<MatchResult>? Results { get; set; }
    public DatabaseStats DatabaseStats { get; set; }

    public async Task OnGetAsync()
    {
        DatabaseStats = await _statsService.GetDatabaseStats();

        if (SearchId is not null)
        {
            Results = (await _searchRepository.GetSearchResultsAsync(Guid.Parse(SearchId))).ToList();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ImageFile == null || ImageFile.Length == 0)
            return BadRequest();

        using var ms = new MemoryStream();
        await ImageFile.CopyToAsync(ms);
        var imageBytes = ms.ToArray();

        var searchId = await _imageSearchService.CreateSearchAsync(
            imageBytes, ImageFile.FileName, Request.GetRemoteIpAddress());

        return RedirectToPage(null, null, new { searchid = searchId });
    }
}