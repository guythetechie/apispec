import "@typespec/openapi3";
import "@typespec/versioning";
import "@apispec/common";

using ApiSpec.Common;
using TypeSpec.Versioning;
using TypeSpec.Http;

@service({
    title: "Ordering API",
    description: "Ordering API",
})
// @versioned(Versions)
namespace api;

enum Versions {
    v1,
}

@doc("The order ID")
@format("uuid")
scalar OrderId extends ResourceId;

@doc("The order")
model Order {
    @key
    id: OrderId;
}

@tag("Orders")
@route("v1/orders")
interface Orders {
    @doc("Delete an order")
    deleteOrder is deleteResource<OrderId>;

    @doc("Get an order")
    getOrder is getResource<OrderId, Order>;
}
