using LanguageExt;
using Microsoft.AspNetCore.Http;
using System;
using System.Net;
using System.Threading.Tasks;

namespace core;

public static partial class HttpHandler
{
    public abstract record DeleteError
    {
        public sealed record ETagMismatch : DeleteError;
    }

    /// <summary>
    /// Deletes the resource
    /// </summary>
    /// <typeparam name="TId">Resource ID type</typeparam>
    /// <param name="request">HTTP request</param>
    /// <param name="tryGetIdFromString">Convert the resource ID's string representation to the resource ID. Returns <typeparamref name="TId"/> if the conversion is successful or an error string if it fails.</param>
    /// <param name="tryDeleteRecord">Delete the resource, returning a <see cref="DeleteError"/> if the deletion fails.</param>
    /// <returns></returns>
    public static async ValueTask<IResult> Delete<TId>(HttpRequest request,
                                                       Func<string, Either<string, TId>> tryGetIdFromString,
                                                       Func<TId, ETag, ValueTask<Either<DeleteError, Unit>>> tryDeleteRecord)
    {
        var result = from id in TryGetId(request, tryGetIdFromString)
                     from eTag in TryGetETag(request)
                     select Delete(id, eTag, tryDeleteRecord);

        return await result.Coalesce();
    }

    private static Either<IResult, TId> TryGetId<TId>(HttpRequest request, Func<string, Either<string, TId>> tryGetIdFromString)
    {
        var idString = request.GetLastPathSegment();

        return tryGetIdFromString(idString).MapLeft(error => new ApiErrorWithStatusCode
        {
            Code = new ApiErrorCode.InvalidId(),
            Message = error,
            StatusCode = HttpStatusCode.BadRequest
        }.ToIResult());
    }

    private static Either<IResult, ETag> TryGetETag(HttpRequest request)
    {
        return request.Headers
                      .TryGetIfMatchHeader()
                      .MapLeft(error => new ApiErrorWithStatusCode
                      {
                          Code = new ApiErrorCode.InvalidConditionalHeader(),
                          Message = error,
                          StatusCode = HttpStatusCode.BadRequest
                      })
                      .Bind(option => option.ToEither(() => new ApiErrorWithStatusCode
                      {
                          Code = new ApiErrorCode.InvalidConditionalHeader(),
                          Message = "'If-Match' header must be specified.",
                          StatusCode = HttpStatusCode.PreconditionRequired
                      }))
                      .Bind<ETag>(ifMatchHeader => ifMatchHeader.Value.ToSeq() switch
                      {
                          [null] => Prelude.Left(new ApiErrorWithStatusCode
                          {
                              Code = new ApiErrorCode.InvalidConditionalHeader(),
                              Message = "'If-Match' header cannot be null.",
                              StatusCode = HttpStatusCode.BadRequest
                          }),
                          [var eTag] when string.IsNullOrWhiteSpace(eTag) => Prelude.Left(new ApiErrorWithStatusCode
                          {
                              Code = new ApiErrorCode.InvalidConditionalHeader(),
                              Message = "'If-Match' header cannot be empty or whitespace.",
                              StatusCode = HttpStatusCode.BadRequest
                          }),
                          [var eTag] => Prelude.Right(new ETag(eTag)),
                          _ => Prelude.Left(new ApiErrorWithStatusCode
                          {
                              Code = new ApiErrorCode.InvalidConditionalHeader(),
                              Message = "Must specify exactly one 'If-Match' header.",
                              StatusCode = HttpStatusCode.BadRequest
                          }),
                      })
                      .MapLeft(error => error.ToIResult());
    }

    private static async ValueTask<IResult> Delete<TId>(TId id, ETag eTag, Func<TId, ETag, ValueTask<Either<DeleteError, Unit>>> tryDeleteRecord)
    {
        var result = await tryDeleteRecord(id, eTag);

        return result.Match(_ => TypedResults.NoContent(),
                            deleteError => deleteError switch
                            {
                                DeleteError.ETagMismatch => new ApiErrorWithStatusCode
                                {
                                    Code = new ApiErrorCode.ETagMismatch(),
                                    Message = "The eTag passed in the 'If-Match' header is invalid. Another process might have updated the resource.",
                                    StatusCode = HttpStatusCode.PreconditionFailed
                                }.ToIResult(),
                                _ => throw new NotImplementedException()
                            });
    }
}