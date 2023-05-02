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
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace api.tests.Orders;

public class GetTests
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
    public Property Missing_resource_returns_NotFound()
    {
        var generator = from fixture in GenerateFixture()
                        from orderId in common.Generator.OrderId.Where(id => fixture.Orders.ContainsKey(id) is false)
                        let request = Fixture.GetRequest(orderId)
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
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var apiError = await response.Content.DeserializeAs<ApiError>(cancellationToken);
            apiError.Code.Should().BeOfType<ApiErrorCode.ResourceNotFound>();
        });
    }

    [Property]
    public Property Valid_request_returns_resource_with_eTag()
    {
        var generator = from fixture in GenerateFixture()
                        from x in Gen.Elements(fixture.Orders.Values)
                        let order = x.Order
                        let eTag = x.ETag
                        let request = Fixture.GetRequest(order.Id)
                        select (order, eTag, fixture, request);

        var arbitrary = generator.ToArbitrary();

        return Prop.ForAll(arbitrary, async x =>
        {
            // Arrange
            var (order, eTag, fixture, request) = x;
            using var _ = request;
            var cancellationToken = CancellationToken.None;

            // Act
            using var response = await fixture.SendRequest(request, cancellationToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var responseJson = await response.Content.DeserializeAs<JsonObject>(cancellationToken);
            var responseOrder = api.Orders.Serialization.Deserialize(responseJson);
            responseOrder.Should().BeEquivalentTo(order, options => options.ComparingRecordsByMembers());

            var responseETag = responseJson.GetStringProperty("eTag");
            eTag.Value.Should().Be(responseETag);
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
                   select GetRequest(x.Order.Id);
        }

        public static HttpRequestMessage GetRequest(OrderId orderId)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
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
            services.AddSingleton(FindOrder);
        }

        private api.Orders.Get.FindOrder FindOrder(IServiceProvider provider)
        {
            return async (orderId, cancellationToken) =>
            {
                await ValueTask.CompletedTask;

                return Orders.Find(orderId);
            };
        }
    }
}
