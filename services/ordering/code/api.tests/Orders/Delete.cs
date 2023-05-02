using common;
using core;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace api.tests.Orders;

public class DeleteTests
{
    [Property]
    public Property Invalid_id_fails()
    {
        var generator = from fixture in GenerateFixture()
                        from invalidOrderId in core.Generator.AlphaNumericString
                        let requestUri = Fixture.GetRequestUri(invalidOrderId)
                        from request in fixture.GenerateValidHttpRequestMessage()
                                               .Select(request =>
                                               {
                                                   request.RequestUri = requestUri;
                                                   return request;
                                               })
                        select (fixture, request);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var (fixture, request) = x;
            using var _ = request;
            var cancellationToken = CancellationToken.None;

            // Act
            using var response = await fixture.SendRequest(request, cancellationToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var apiError = await response.Content.DeserializeAs<ApiError>(cancellationToken);
            apiError.Code.Should().BeOfType<ApiErrorCode.InvalidId>();
        });
    }

    [Property]
    public Property Missing_If_Match_header_fails()
    {
        var generator = from fixture in GenerateFixture()
                        from request in fixture.GenerateValidHttpRequestMessage()
                                               .Select(request =>
                                               {
                                                   request.Headers.Remove("If-Match");
                                                   return request;
                                               })
                        select (fixture, request);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var (fixture, request) = x;
            using var _ = request;
            var cancellationToken = CancellationToken.None;

            // Act
            using var response = await fixture.SendRequest(request, cancellationToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);

            var apiError = await response.Content.DeserializeAs<ApiError>(cancellationToken);
            apiError.Code.Should().BeOfType<ApiErrorCode.InvalidConditionalHeader>();
        });
    }

    [Property]
    public Property Multiple_If_Match_headers_fail()
    {
        var generator = from fixture in GenerateFixture()
                        from invalidHeader in core.Generator.AlphaNumericString
                                                            .NonEmptySeqOf()
                                                            .Where(x => x.Count > 1)
                                                            .Select(x => x.ToArray())
                        from request in fixture.GenerateValidHttpRequestMessage()
                                               .Select(request =>
                                               {
                                                   request.Headers.TryAddWithoutValidation("If-Match", invalidHeader);
                                                   return request;
                                               })
                        where request.Headers.Contains("If-Match")
                        select (fixture, request);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var (fixture, request) = x;
            using var _ = request;
            var cancellationToken = CancellationToken.None;

            // Act
            using var response = await fixture.SendRequest(request, cancellationToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var apiError = await response.Content.DeserializeAs<ApiError>(cancellationToken);
            apiError.Code.Should().BeOfType<ApiErrorCode.InvalidConditionalHeader>();
        });
    }

    [Property]
    public Property Incorrect_ETag_fails()
    {
        var generator = from fixture in GenerateFixture()
                        from x in Gen.Elements(fixture.Orders.Values)
                        from badETag in core.Generator.ETag.Where(generatedETag => generatedETag != x.ETag)
                        let request = Fixture.GetRequest(x.Order.Id, badETag)
                        select (fixture, request);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var (fixture, request) = x;
            using var _ = request;
            var cancellationToken = CancellationToken.None;

            // Act
            using var response = await fixture.SendRequest(request, cancellationToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

            var apiError = await response.Content.DeserializeAs<ApiError>(cancellationToken);
            apiError.Code.Should().BeOfType<ApiErrorCode.ETagMismatch>();
        });
    }

    [Property]
    public Property Valid_request_succeeds()
    {
        var generator = from fixture in GenerateFixture()
                        from x in Gen.Elements(fixture.Orders.Values)
                        let orderId = x.Order.Id
                        let request = Fixture.GetRequest(orderId, x.ETag)
                        select (orderId, fixture, request);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var (orderId, fixture, request) = x;
            using var _ = request;
            var cancellationToken = CancellationToken.None;

            // Act
            using var response = await fixture.SendRequest(request, cancellationToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            fixture.Orders.Find(orderId).Should().BeNone();
        });
    }

    private static Gen<Fixture> GenerateFixture()
    {
        return from x in Gen.Zip(common.Generator.Order, core.Generator.ETag)
                            .NonEmptySeqOf()
                            .DistinctBy(x => x.Item1.Id.Value)
               select new Fixture
               {
                   Orders = x.Map(x => (x.Item1.Id, x))
                             .ToHashMap()
                             .ToAtom()
               };
    }

    private static Gen<StringValues> GenerateInvalidIfMatchHeader()
    {
        return Gen.OneOf(GenerateNullOrWhiteSpaceIfMatchHeader(), GenerateMultipleIfMatchHeaders());
    }

    private static Gen<StringValues> GenerateNullOrWhiteSpaceIfMatchHeader()
    {
        return from nullOrWhitespaceString in core.Generator.WhiteSpaceString
               select (StringValues)nullOrWhitespaceString;
    }

    private static Gen<StringValues> GenerateMultipleIfMatchHeaders()
    {
        return from values in GenExtensions.GenerateDefault<string>()
                                           .ArrayOf()
                                           .Where(x => x.Length > 1)
               select (StringValues)values;
    }

    private sealed record Fixture
    {
        public required AtomHashMap<OrderId, (Order Order, ETag ETag)> Orders { get; init; }

        public Gen<HttpRequestMessage> GenerateValidHttpRequestMessage()
        {
            return from x in Gen.Elements(Orders.Values)
                   select GetRequest(x.Order.Id, x.ETag);
        }

        public static HttpRequestMessage GetRequest(OrderId orderId, ETag eTag)
        {
            var request = GetRequest(orderId);

            request.Headers.TryAddWithoutValidation("If-Match", eTag.Value);

            return request;
        }

        public static HttpRequestMessage GetRequest(OrderId orderId)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Delete,
                RequestUri = GetRequestUri(orderId)
            };

            return request;
        }

        public static Uri GetRequestUri(OrderId orderId)
        {
            return GetRequestUri(orderId.Value.ToString());
        }

        public static Uri GetRequestUri(string orderId)
        {
            return new Uri($"/v1/orders/{orderId}", UriKind.Relative);
        }

        public async ValueTask<HttpResponseMessage> SendRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Console.Write(Orders.Length);
            using var factory = GetTestFactory();
            using var client = factory.CreateClient();

            return await client.SendAsync(request, cancellationToken);
        }

        private TestFactory GetTestFactory()
        {
            return new TestFactory
            {
                ConfigureServices = ConfigureServices
            };
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(DeleteOrder);
        }

        private api.Orders.Delete.DeleteOrder DeleteOrder(IServiceProvider provider)
        {
            return async (orderId, eTag, cancellationToken) =>
            {
                await ValueTask.CompletedTask;

#pragma warning disable CA1849 // Call async methods when in an async method
                return Orders.Find(orderId)
                             .Map(x => eTag.Value == x.ETag.Value
                                        ? Either<HttpHandler.DeleteError, Unit>.Right(Orders.Remove(orderId))
                                        : Either<HttpHandler.DeleteError, Unit>.Left(new HttpHandler.DeleteError.ETagMismatch()))
                             .IfNone(() => Either<HttpHandler.DeleteError, Unit>.Right(Prelude.unit));
#pragma warning restore CA1849 // Call async methods when in an async method
            };
        }
    }
}
