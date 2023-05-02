using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Primitives;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static core.HttpHandler;

namespace core.tests;

public class HttpDeleteHandlerTests
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
            var result = await fixture.Delete();

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
    public Property Missing_If_Match_header_fails()
    {
        var generator = from fixture in GenerateValidFixture()
                        select fixture with
                        {
                            Headers = new HeaderDictionary()
                        };

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async fixture =>
        {
            // Act
            var result = await fixture.Delete();

            // Assert
            var httpResult = result.Should().BeOfType<JsonHttpResult<ApiErrorWithStatusCode>>().Subject;
            httpResult.StatusCode.Should().Be(StatusCodes.Status428PreconditionRequired);

            var httpResultValue = httpResult.Value;
            httpResultValue.Should().NotBeNull();
            var apiError = httpResultValue.Should().BeAssignableTo<ApiError>().Subject;

            apiError.Code.Should().BeOfType<ApiErrorCode.InvalidConditionalHeader>();
        });
    }

    [Property]
    public Property Invalid_If_Match_headers_fail()
    {
        var generator = from fixture in GenerateValidFixture()
                        from headers in Gen.OneOf(GenerateNullOrWhiteSpaceIfMatchHeader(), GenerateMultipleIfMatchHeaders())
                        select fixture with
                        {
                            Headers = headers
                        };

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async fixture =>
        {
            // Act
            var result = await fixture.Delete();

            // Assert
            var httpResult = result.Should().BeOfType<JsonHttpResult<ApiErrorWithStatusCode>>().Subject;
            httpResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

            var httpResultValue = httpResult.Value;
            httpResultValue.Should().NotBeNull();
            var apiError = httpResultValue.Should().BeAssignableTo<ApiError>().Subject;

            apiError.Code.Should().BeOfType<ApiErrorCode.InvalidConditionalHeader>();
        });
    }

    [Property]
    public Property Incorrect_ETag_fails()
    {
        var generator = from fixture in GenerateValidFixture()
                        select fixture with
                        {
                            DeleteResult = Prelude.Left(new DeleteError.ETagMismatch() as DeleteError)
                        };

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async fixture =>
        {
            // Act
            var result = await fixture.Delete();

            // Assert
            var httpResult = result.Should().BeOfType<JsonHttpResult<ApiErrorWithStatusCode>>().Subject;
            httpResult.StatusCode.Should().Be(StatusCodes.Status412PreconditionFailed);

            var httpResultValue = httpResult.Value;
            httpResultValue.Should().NotBeNull();
            var apiError = httpResultValue.Should().BeAssignableTo<ApiError>().Subject;

            apiError.Code.Should().BeOfType<ApiErrorCode.ETagMismatch>();
        });
    }

    [Property]
    public Property Valid_request_succeeds()
    {
        var arbitrary = GenerateValidFixture().ToArbitrary();

        return Prop.ForAll(arbitrary, async fixture =>
        {
            // Act
            var result = await fixture.Delete();

            // Assert
            var httpResult = result.Should().BeOfType<NoContent>().Subject;
            httpResult.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        });
    }

    private static Gen<Fixture<object>> GenerateValidFixture()
    {
        return from internet in TestGenerator.Internet
               let uri = internet.UrlWithPath()
               from eTag in Generator.ETag
               let headers = new HeaderDictionary
               {
                   ["If-Match"] = eTag.Value
               }
               from id in GenExtensions.GenerateDefault<object>()
               select new Fixture<object>
               {
                   Headers = headers,
                   RequestUri = new Uri(uri, UriKind.Absolute),
                   GetIdResult = Prelude.Right(id),
                   DeleteResult = Prelude.Right(Prelude.unit)
               };
    }

    private static Gen<IHeaderDictionary> GenerateMissingIfMatchHeader()
    {
        return Gen.Constant(new HeaderDictionary() as IHeaderDictionary);
    }

    private static Gen<IHeaderDictionary> GenerateNullOrWhiteSpaceIfMatchHeader()
    {
        return from nullOrWhitespaceString in Generator.WhiteSpaceString
               select new HeaderDictionary
               {
                   ["If-Match"] = nullOrWhitespaceString
               } as IHeaderDictionary;
    }

    private static Gen<IHeaderDictionary> GenerateMultipleIfMatchHeaders()
    {
        return from values in GenExtensions.GenerateDefault<string>()
                                           .ArrayOf()
                                           .Where(x => x.Length > 1)
               select new HeaderDictionary
               {
                   ["If-Match"] = values
               } as IHeaderDictionary;
    }

    private sealed record Fixture<TId>
    {
        public required Uri RequestUri { get; init; }

        public IHeaderDictionary Headers { get; init; } = new HeaderDictionary();

        public required Either<string, TId> GetIdResult { get; init; }

        public required Either<DeleteError, Unit> DeleteResult { get; init; }

        public async ValueTask<IResult> Delete()
        {
            var request = new TestHttpRequest(RequestUri, Headers);
            return await HttpHandler.Delete<TId>(request, TryGetIdFromString, TryDeleteRecord);
        }

        private Either<string, TId> TryGetIdFromString(string id) => GetIdResult;

        private async ValueTask<Either<DeleteError, Unit>> TryDeleteRecord(TId id, ETag eTag) =>
            await ValueTask.FromResult(DeleteResult);

        private sealed class TestHttpRequest : HttpRequest
        {
            private readonly Uri uri;

            public TestHttpRequest(Uri uri, IHeaderDictionary headers)
            {
                this.uri = uri;
                Headers = headers;
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

            public override IHeaderDictionary Headers { get; }

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