using System.Net;
using bgdb.Common;
using bgdb.Common.Models;
using bgdb.Common.Repositories;
using bgdb.Common.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace bgdb.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IStatsService _statsService;
    private readonly ISearchRepository _searchRepository;
    private readonly IImageSearchService _imageSearchService;

    public IndexModel(
        IStatsService statsService, ISearchRepository searchRepository, IImageSearchService imageSearchService)
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

        await using var imageStream = ImageFile.OpenReadStream();
        
        var searchId = await _imageSearchService.CreateSearchAsync(imageStream, GetRemoteIpAddress());

        return RedirectToPage(null, null, new { searchid = searchId });
    }

    private IPAddress GetRemoteIpAddress()
    {
        var forwardedHeader = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var ipStr = forwardedHeader?.Split(',').FirstOrDefault()?.Trim();
        return (ipStr is null ? HttpContext.Connection.RemoteIpAddress : IPAddress.Parse(ipStr))!;
    }
}