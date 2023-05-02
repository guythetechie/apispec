using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace core.tests;

public class HttpGetHandlerTests
{
    [Property]
    public Property Invalid_id_fails()
    {
        var generator = from badIdErrorMessage in GenExtensions.GenerateDefault<NonEmptyString>().Select(x => x.Item)
                        from fixture in GenerateValidFixture()
                        select (badIdErrorMessage, fixture with
                        {
                            GetIdResult = Prelude.Left(badIdErrorMessage)
                        });

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var (badIdErrorMessage, fixture) = x;

            // Act
            var result = await fixture.Get();

            // Assert
            var httpResult = result.Should().BeOfType<JsonHttpResult<ApiErrorWithStatusCode>>().Subject;
            httpResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

            var httpResultValue = httpResult.Value;
            httpResultValue.Should().NotBeNull();
            var apiError = httpResultValue.Should().BeAssignableTo<ApiError>().Subject;

            apiError.Code.Should().BeOfType<ApiErrorCode.InvalidId>();
            apiError.Message.Should().Be(badIdErrorMessage);
        });
    }

    [Property]
    public Property Missing_resource_returns_NotFound()
    {
        var generator = from fixture in GenerateValidFixture()
                        select fixture with
                        {
                            FindResourceResult = Prelude.None
                        };

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async fixture =>
        {
            // Act
            var result = await fixture.Get();

            // Assert
            var httpResult = result.Should().BeOfType<JsonHttpResult<ApiErrorWithStatusCode>>().Subject;
            httpResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

            var httpResultValue = httpResult.Value;
            httpResultValue.Should().NotBeNull();
            var apiError = httpResultValue.Should().BeAssignableTo<ApiError>().Subject;

            apiError.Code.Should().BeOfType<ApiErrorCode.ResourceNotFound>();
        });
    }

    [Property]
    public Property Existing_resource_returns_json_that_includes_eTag()
    {
        var arbitrary = GenerateValidFixture().ToArbitrary();

        return Prop.ForAll(arbitrary, async fixture =>
        {
            // Act
            var result = await fixture.Get();

            // Assert
            var httpResult = result.Should().BeOfType<Ok<JsonObject>>().Subject;
            httpResult.StatusCode.Should().Be(StatusCodes.Status200OK);
            httpResult.Value.Should().Equal(fixture.SerializedResource);
        });
    }

    private static Gen<Fixture<object, object>> GenerateValidFixture()
    {
        return from internet in Generator.Internet
               let uri = internet.UrlWithPath()
               from resource in GenExtensions.GenerateDefault<object>()
               from resourceJson in Generator.JsonObject
               from eTag in Generator.ETag
               from id in GenExtensions.GenerateDefault<object>()
               select new Fixture<object, object>
               {
                   RequestUri = new Uri(uri, UriKind.Absolute),
                   GetIdResult = Prelude.Right(id),
                   FindResourceResult = (resource, eTag),
                   SerializedResource = resourceJson
               };
    }

    private sealed record Fixture<TId, TResource>
    {
        public required Uri RequestUri { get; init; }

        public required Either<string, TId> GetIdResult { get; init; }

        public required Option<(TResource Resource, ETag ETag)> FindResourceResult { get; init; }

        public required JsonObject SerializedResource { get; init; }

        public async ValueTask<IResult> Get()
        {
            var request = new TestHttpRequest(RequestUri);
            return await HttpHandler.Get(request, TryGetIdFromString, FindResource, SerializeResource);
        }

        private Either<string, TId> TryGetIdFromString(string id) => GetIdResult;

        private async ValueTask<Option<(TResource, ETag)>> FindResource(TId id) =>
            await ValueTask.FromResult(FindResourceResult);

        private JsonObject SerializeResource(TResource resource) => SerializedResource;

        private sealed class TestHttpRequest : HttpRequest
        {
            private readonly Uri uri;

            public TestHttpRequest(Uri uri)
            {
                this.uri = uri;
            }

            public override HttpContext HttpContext => throw new NotImplementedException();

            public override string Method { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override string Scheme { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override bool IsHttps { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override HostString Host { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override PathString PathBase { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override PathString Path { get => PathString.FromUriComponent(uri); set => throw new NotImplementedException(); }
            public override QueryString QueryString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override IQueryCollection Query { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override string Protocol { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override IHeaderDictionary Headers => throw new NotImplementedException();

            public override IRequestCookieCollection Cookies { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override long? ContentLength { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override string? ContentType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override Stream Body { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override bool HasFormContentType => throw new NotImplementedException();

            public override IFormCollection Form { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }
    }
}