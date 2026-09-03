using bgdb.Common.Repositories;
using bgdb.Common.Services;
using bgdb.Web.Endpoints.Responses;
using bgdb.Web.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace bgdb.Web.Endpoints;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api");

        group.MapGet("/search/{searchId:guid}", GetSearchResults);
        
        group
            .MapPost("/search", SearchImage)
            .DisableAntiforgery();
        
        return builder;
    }

    private static async Task<IResult> GetSearchResults(
        [FromRoute] Guid searchId,
        [FromServices] SearchRepository searchRepository)
    {
        var results = await searchRepository.GetSearchResultsAsync(searchId);
        if (results.Count == 0)
            return Results.NotFound();

        var response = new GetSearchResponse
        {
            Results = results.Select(MatchResultResponse.FromEntity)
        };
        return Results.Ok(response);
    }

    private static async Task<IResult> SearchImage(
        IFormFile image, 
        HttpRequest request,
        [FromServices] ImageSearchService imageSearchService, 
        [FromServices] SearchRepository searchRepository)
    {
        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        var imageBytes = ms.ToArray();
        var searchId = await imageSearchService.CreateSearchAsync(
            imageBytes, image.FileName, request.GetRemoteIpAddress());
        var results = await searchRepository.GetSearchResultsAsync(searchId);

        var response = new SearchResponse
        {
            SearchId = searchId,
            Results = results.Select(MatchResultResponse.FromEntity)
        };
        return Results.Ok(response);
    }
}