using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace api.tests;

internal static class HttpContentExtensions
{
    public static async ValueTask<T> DeserializeAs<T>(this HttpContent content, CancellationToken cancellationToken)
    {
        return await content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
                ?? throw new JsonException("Could not deserialize response content.");
    }
}
