using System;

namespace common;

public record OrderId
{
    public required Guid Value { get; init; }
}

public record Order
{
    public required OrderId Id { get; init; }
}