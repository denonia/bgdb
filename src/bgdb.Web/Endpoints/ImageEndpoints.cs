using bgdb.Common.Storages;
using Microsoft.AspNetCore.Mvc;

namespace bgdb.Web.Endpoints;

public static class ImageEndpoints
{
    public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/img");
        
        group.MapGet("/thumb/{mapsetId:int}/{fileName}", GetThumbnail);
        group.MapGet("/search/{searchId:guid}", GetSearchThumbnail);
        
        return builder;
    }

    private static async Task<IResult> GetThumbnail(
        [FromRoute] int mapsetId,
        [FromRoute] string fileName,
        [FromServices] ImageStorage imageStorage)
    {
        var image = await imageStorage.GetBackgroundThumbnailAsync(mapsetId, fileName);
        
        return image is null 
            ? Results.NotFound() 
            : Results.File(image.Content, image.ContentType);
    }
    
    private static async Task<IResult> GetSearchThumbnail(
        [FromRoute] Guid searchId,
        [FromServices] ImageStorage imageStorage)
    {
        var image = await imageStorage.GetSearchThumbnailAsync(searchId);
        
        return image is null 
            ? Results.NotFound() 
            : Results.File(image.Content, image.ContentType);
    }
}