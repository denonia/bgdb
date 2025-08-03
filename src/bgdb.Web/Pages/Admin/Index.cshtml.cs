using bgdb.Common;
using bgdb.Common.Models;
using bgdb.Common.Repositories;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace bgdb.Web.Pages.Admin;

public class Index : PageModel
{
    private readonly ISearchRepository _searchRepository;

    public Index(ISearchRepository searchRepository)
    {
        _searchRepository = searchRepository;
    }
    
    public IList<SearchRecord> LatestSearches { get; set; }
    
    public string SearchUrl(string searchId) => $"{Settings.BaseUrl}/?searchid={searchId}";
    public string SearchPreviewUrl(string searchId) => $"{Settings.BaseUrl}/img/search/{searchId}";
    
    public async Task OnGetAsync()
    {
        LatestSearches = await _searchRepository.GetLatestSearches();
    }
}