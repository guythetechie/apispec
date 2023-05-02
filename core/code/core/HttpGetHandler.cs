using LanguageExt;
using Microsoft.AspNetCore.Http;
using System;
using System.Net;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace core;

public static partial class HttpHandler
{
    /// <summary>
    /// Deletes the resource
    /// </summary>
    /// <typeparam name="TId">Resource ID type</typeparam>
    /// <param name="request">HTTP request</param>
    /// <param name="tryGetIdFromString">Convert the resource ID's string representation to the resource ID. Returns <typeparamref name="TId"/> if the conversion is successful or an error string if it fails.</param>
    /// <returns></returns>
    public static async ValueTask<IResult> Get<TId, TResource>(HttpRequest request,
                                                               Func<string, Either<string, TId>> tryGetIdFromString,
                                                               Func<TId, ValueTask<Option<(TResource, ETag)>>> findResource,
                                                               Func<TResource, JsonObject> serializeResource)
    {
        var result = from id in TryGetId(request, tryGetIdFromString).ToAsync()
                     from x in TryGetResource(id, findResource).ToAsync()
                     select GetSuccessfulResponse(x.Resource, x.ETag, serializeResource);

        return await result.Coalesce();
    }

    private static async ValueTask<Either<IResult, (TResource Resource, ETag ETag)>> TryGetResource<TId, TResource>(TId id, Func<TId, ValueTask<Option<(TResource, ETag)>>> findResource)
    {
        var option = await findResource(id);

        return option.ToEither(new ApiErrorWithStatusCode
        {
            Code = new ApiErrorCode.ResourceNotFound(),
            Message = "Resource with ID was not found",
            StatusCode = HttpStatusCode.NotFound
        }.ToIResult());
    }

    private static IResult GetSuccessfulResponse<TResource>(TResource resource, ETag eTag, Func<TResource, JsonObject> serializeResource)
    {
        var json = serializeResource(resource);

        json.Add("eTag", eTag.Value);

        return TypedResults.Ok(json);
    }
}