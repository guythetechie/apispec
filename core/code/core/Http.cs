using Flurl;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace core;

public sealed record ETag
{
    public string Value { get; init; }

    public ETag(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"ETag cannot be null or whitespace.", nameof(value));
        }

        Value = value;
    }
}

[JsonConverter(typeof(ApiErrorCodeJsonConverter))]
public abstract record ApiErrorCode
{
    public sealed record ResourceNotFound : ApiErrorCode;
    public sealed record ResourceAlreadyExists : ApiErrorCode;
    public sealed record InvalidConditionalHeader : ApiErrorCode;
    public sealed record InvalidJsonBody : ApiErrorCode;
    public sealed record InvalidId : ApiErrorCode;
    public sealed record ETagMismatch : ApiErrorCode;
    public sealed record InternalServerError : ApiErrorCode;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    private sealed class ApiErrorCodeJsonConverter : JsonConverter<ApiErrorCode>
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
    {
        public override ApiErrorCode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonNode.Parse(ref reader) switch
            {
                null => null,
                var node => node.AsValue().GetValue<string>() switch
                {
                    nameof(ResourceNotFound) => new ResourceNotFound(),
                    nameof(ResourceAlreadyExists) => new ResourceAlreadyExists(),
                    nameof(InvalidConditionalHeader) => new InvalidConditionalHeader(),
                    nameof(InvalidJsonBody) => new InvalidJsonBody(),
                    nameof(InvalidId) => new InvalidId(),
                    nameof(ETagMismatch) => new ETagMismatch(),
                    nameof(InternalServerError) => new InternalServerError(),
                    var value => throw new JsonException($"'{value}' is not a valid API error code.")
                }
            };
        }

        public override void Write(Utf8JsonWriter writer, ApiErrorCode value, JsonSerializerOptions options)
        {
            var stringValue = value switch
            {
                ResourceNotFound => nameof(ResourceNotFound),
                ResourceAlreadyExists => nameof(ResourceAlreadyExists),
                InvalidConditionalHeader => nameof(InvalidConditionalHeader),
                InvalidJsonBody => nameof(InvalidJsonBody),
                InvalidId => nameof(InvalidId),
                ETagMismatch => nameof(ETagMismatch),
                InternalServerError => nameof(InternalServerError),
                _ => throw new NotImplementedException()
            };

            writer.WriteStringValue(stringValue);
        }
    }
}

[JsonConverter(typeof(ApiErrorJsonConverter))]
public record ApiError
{
    public required ApiErrorCode Code { get; init; }
    public required string Message { get; init; }
    public Seq<ApiError> Details { get; init; } = Seq<ApiError>.Empty;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    private sealed class ApiErrorJsonConverter : JsonConverter<ApiError>
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
    {
        public override ApiError? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonNode.Parse(ref reader) switch
            {
                null => null,
                var node => Deserialize(node.AsObject(), options)
            };
        }

        public override void Write(Utf8JsonWriter writer, ApiError value, JsonSerializerOptions options)
        {
            var jsonObject = Serialize(value, options);
            jsonObject.WriteTo(writer, options);
        }

        private static JsonObject Serialize(ApiError apiError, JsonSerializerOptions options)
        {
            var jsonObject = new JsonObject
            {
                ["code"] = JsonSerializer.SerializeToNode(apiError.Code, options),
                ["message"] = apiError.Message
            };

            if (apiError.Details.Any())
            {
                jsonObject.Add("details", apiError.Details
                                                  .Map(detail => Serialize(detail, options))
                                                  .ToJsonArray());
            }

            return jsonObject;
        }

        private static ApiError Deserialize(JsonObject jsonObject, JsonSerializerOptions options)
        {
            var codeValidation = jsonObject.TryGetProperty("code")
                                           .Bind<ApiErrorCode>(node => JsonSerializer.Deserialize<ApiErrorCode>(node, options) switch
                                           {
                                               null => Prelude.Left("Failed to deserialize API error code."),
                                               var value => Prelude.Right(value)
                                           })
                                           .ToValidation();
            var messageValidation = jsonObject.TryGetStringProperty("message").ToValidation();
            var detailsValidation = jsonObject.TryGetOptionalJsonObjectArrayProperty("details")
                                              .Map(option => option.IfNone(Seq<JsonObject>.Empty))
                                              .MapT(jsonObject => Prelude.Optional(JsonSerializer.Deserialize<ApiError>(jsonObject, options)))
                                              .ToValidation()
                                              .Bind(options => options.Sequence()
                                                                      .ToValidation("Property 'details' must be an array of API errors."));

            return (codeValidation, messageValidation, detailsValidation)
                    .Apply((code, message, details) => new ApiError
                    {
                        Code = code,
                        Message = message,
                        Details = details
                    })
                    .IfFail(errors => throw new JsonException($"Could not deserialize API errors. {string.Join("; ", errors)}"));
        }
    }
}

public record ApiErrorWithStatusCode : ApiError
{
    public required HttpStatusCode StatusCode { get; set; }

    public IResult ToIResult()
    {
        return TypedResults.Json(this, statusCode: (int)StatusCode);
    }
}

public record IfMatchHeader
{
    public IfMatchHeader(StringValues value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"'{nameof(value)}' cannot be null or whitespace.", nameof(value));
        }

        Value = value;
    }

    public StringValues Value { get; }
}

public static class IHeaderDictionaryExtensions
{
    public static Either<string, Option<IfMatchHeader>> TryGetIfMatchHeader(this IHeaderDictionary headerDictionary)
    {
        return headerDictionary.TryGetValue("If-Match", out var ifMatch) switch
        {
            true when string.IsNullOrWhiteSpace(ifMatch) => "'If-Match' header cannot be empty or whitespace.",
            true => Option<IfMatchHeader>.Some(new IfMatchHeader(ifMatch!)),
            false => Option<IfMatchHeader>.None
        };
    }
}

public static class HttpIResultExtensions
{
    public static IResult Coalesce<TError, TSuccess>(this Either<TError, TSuccess> either) where TSuccess : IResult where TError : IResult
    {
        return either.Match(success => success as IResult, error => error as IResult);
    }

    public static async ValueTask<IResult> Coalesce<TError, TSuccess>(this Either<TError, ValueTask<TSuccess>> either) where TSuccess : IResult where TError : IResult
    {
        return await either.Sequence()
                           .Map(Coalesce);
    }
}

public static class HttpRequestExtensions
{
    public static string GetLastPathSegment(this HttpRequest request)
    {
        return Url.ParsePathSegments(request.Path).Last();
    }
}