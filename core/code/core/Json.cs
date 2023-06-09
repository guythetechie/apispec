﻿using LanguageExt;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace core;

internal static class JsonValueExtensions
{
    public static Option<T> TryGetValue<T>(this JsonValue jsonValue)
    {
        return jsonValue.TryGetValue<T>(out var value)
                ? value
                : Prelude.None;
    }
}

public static class JsonNodeExtensions
{
    [return: NotNullIfNotNull(nameof(node))]
    public static JsonNode? Clone(this JsonNode? node)
    {
        return node switch
        {
            JsonArray jsonArray => jsonArray.Map(node => node.Clone())
                                            .ToJsonArray(),
            JsonObject jsonObject => new JsonObject(jsonObject.Map(kvp => KeyValuePair.Create(kvp.Key, kvp.Value.Clone()))),
            JsonValue jsonValue => (jsonValue.TryGetValue<bool>().Map(x => JsonValue.Create<bool>(x)!) ||
                                    jsonValue.TryGetValue<byte>().Map(x => JsonValue.Create<byte>(x)!) ||
                                    jsonValue.TryGetValue<char>().Map(x => JsonValue.Create<char>(x)!) ||
                                    jsonValue.TryGetValue<DateTime>().Map(x => JsonValue.Create<DateTime>(x)!) ||
                                    jsonValue.TryGetValue<DateTimeOffset>().Map(x => JsonValue.Create<DateTimeOffset>(x)!) ||
                                    jsonValue.TryGetValue<decimal>().Map(x => JsonValue.Create<decimal>(x)!) ||
                                    jsonValue.TryGetValue<double>().Map(x => JsonValue.Create<double>(x)!) ||
                                    jsonValue.TryGetValue<Guid>().Map(x => JsonValue.Create<Guid>(x)!) ||
                                    jsonValue.TryGetValue<short>().Map(x => JsonValue.Create<short>(x)!) ||
                                    jsonValue.TryGetValue<int>().Map(x => JsonValue.Create<int>(x)!) ||
                                    jsonValue.TryGetValue<long>().Map(x => JsonValue.Create<long>(x)!) ||
                                    jsonValue.TryGetValue<sbyte>().Map(x => JsonValue.Create<sbyte>(x)!) ||
                                    jsonValue.TryGetValue<float>().Map(x => JsonValue.Create<float>(x)!) ||
                                    jsonValue.TryGetValue<string>().Map(x => JsonValue.Create<string>(x)!) ||
                                    jsonValue.TryGetValue<ushort>().Map(x => JsonValue.Create<ushort>(x)!) ||
                                    jsonValue.TryGetValue<uint>().Map(x => JsonValue.Create<uint>(x)!) ||
                                    jsonValue.TryGetValue<ulong>().Map(x => JsonValue.Create<ulong>(x)!)
                                   ).IfNone(() => throw new NotSupportedException()),
            null => null,
            _ => throw new NotImplementedException()
        };
    }

    public static Option<JsonValue> TryAsJsonValue(this JsonNode? node)
    {
        return node as JsonValue;
    }

    public static Option<JsonObject> TryAsJsonObject(this JsonNode? node)
    {
        return node as JsonObject;
    }

    public static Option<JsonArray> TryAsJsonArray(this JsonNode? node)
    {
        return node as JsonArray;
    }
}

public static class JsonObjectExtensions
{
    public static JsonNode GetProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetProperty(propertyName)
                  .IfLeftThrow();

    public static Option<JsonNode> GetOptionalProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetProperty(propertyName)
                  .ToOption();

    public static Either<string, JsonNode> TryGetProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetPropertyValue(propertyName, out var node)
            ? node is null
                ? $"Property '{propertyName}' is null."
                : Either<string, JsonNode>.Right(node)
            : $"Property '{propertyName}' is missing.";

    private static T IfLeftThrow<T>(this Either<string, T> either) =>
        either.IfLeft(error => throw new JsonException(error));

    public static JsonObject GetJsonObjectProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetJsonObjectProperty(propertyName)
                  .IfLeftThrow();

    public static Option<JsonObject> GetOptionalJsonObjectProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetOptionalJsonObjectProperty(propertyName)
                  .IfLeftThrow();

    public static Either<string, Option<JsonObject>> TryGetOptionalJsonObjectProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.GetOptionalProperty(propertyName)
                  .Sequence(node => node.TryAsJsonObject(propertyName));

    public static Either<string, JsonObject> TryGetJsonObjectProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetProperty(propertyName)
                  .Bind(node => node.TryAsJsonObject(propertyName));

    private static Either<string, JsonObject> TryAsJsonObject(this JsonNode node, string propertyName)
    {
        return node.TryAsJsonObject()
                   .ToEither($"Property '{propertyName}' is not a JSON object.");
    }

    public static JsonArray GetJsonArrayProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetJsonArrayProperty(propertyName)
                  .IfLeftThrow();

    public static Option<JsonArray> GetOptionalJsonArrayProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetOptionalJsonArrayProperty(propertyName)
                  .IfLeftThrow();

    public static Either<string, Option<JsonArray>> TryGetOptionalJsonArrayProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.GetOptionalProperty(propertyName)
                  .Sequence(node => node.TryAsJsonArray(propertyName));

    public static Either<string, JsonArray> TryGetJsonArrayProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetProperty(propertyName)
                  .Bind(node => node.TryAsJsonArray(propertyName));

    private static Either<string, JsonArray> TryAsJsonArray(this JsonNode node, string propertyName)
    {
        return node.TryAsJsonArray()
                   .ToEither($"Property '{propertyName}' is not a JSON array.");
    }

    public static Seq<JsonObject> GetJsonObjectArrayProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetJsonObjectArrayProperty(propertyName)
                  .IfLeftThrow();

    public static Option<Seq<JsonObject>> GetOptionalJsonObjectArrayProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetOptionalJsonObjectArrayProperty(propertyName)
                  .IfLeftThrow();

    public static Either<string, Option<Seq<JsonObject>>> TryGetOptionalJsonObjectArrayProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.GetOptionalProperty(propertyName)
                  .Sequence(node => node.TryAsJsonObjectArray(propertyName));

    public static Either<string, Seq<JsonObject>> TryGetJsonObjectArrayProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetProperty(propertyName)
                  .Bind(node => node.TryAsJsonObjectArray(propertyName));

    private static Either<string, Seq<JsonObject>> TryAsJsonObjectArray(this JsonNode node, string propertyName)
    {
        return node.TryAsJsonArray()
                   .ToEither($"Property '{propertyName}' is not a JSON array.")
                   .Bind(jsonArray => jsonArray.ToSeq()
                                               .Sequence(node => node.TryAsJsonObject())
                                               .ToEither($"Property '{propertyName}' is not an array of JSON objects."));
    }

    public static JsonValue GetJsonValueProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetJsonValueProperty(propertyName)
                  .IfLeftThrow();

    public static Either<string, JsonValue> TryGetJsonValueProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetProperty(propertyName)
                  .Map(node => node.TryAsJsonValue())
                  .Bind(option => option.ToEither($"Property '{propertyName}' is not a JSON value."));

    public static string GetStringProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetStringProperty(propertyName)
                  .IfLeftThrow();

    public static Either<string, string> TryGetStringProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetJsonValueProperty(propertyName)
                  .Map(node => node.TryGetValue<string>())
                  .Bind(option => option.ToEither($"Property '{propertyName}''s value cannot be converted to string."));

    public static Guid GetGuidProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetGuidProperty(propertyName)
                  .IfLeftThrow();

    public static Either<string, Guid> TryGetGuidProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetJsonValueProperty(propertyName)
                  .Map(node => node.TryGetValue<Guid>())
                  .Bind(option => option.ToEither($"Property '{propertyName}''s value cannot be converted to Guid."));

    public static bool GetBoolProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetBoolProperty(propertyName)
                  .IfLeftThrow();

    public static Either<string, bool> TryGetBoolProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetJsonValueProperty(propertyName)
                  .Map(node => node.TryGetValue<bool>())
                  .Bind(option => option.ToEither($"Property '{propertyName}''s value cannot be converted to bool."));

    public static int GetIntProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetIntProperty(propertyName)
                  .IfLeftThrow();

    public static Either<string, int> TryGetIntProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetJsonValueProperty(propertyName)
                  .Map(node => node.TryGetValue<int>())
                  .Bind(option => option.ToEither($"Property '{propertyName}''s value cannot be converted to int."));

    public static double GetDoubleProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetDoubleProperty(propertyName)
                  .IfLeftThrow();

    public static Either<string, double> TryGetDoubleProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetJsonValueProperty(propertyName)
                  .Map(node => node.TryGetValue<double>())
                  .Bind(option => option.ToEither($"Property '{propertyName}''s value cannot be converted to double."));

    public static DateTimeOffset GetDateTimeOffsetProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetDateTimeOffsetProperty(propertyName)
                  .IfLeftThrow();

    public static Either<string, DateTimeOffset> TryGetDateTimeOffsetProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.TryGetJsonValueProperty(propertyName)
                  .Map(node => node.TryGetValue<DateTimeOffset>())
                  .Bind(option => option.ToEither($"Property '{propertyName}''s value cannot be converted to DateTimeOffset."));

    public static JsonObject AddProperty(this JsonObject jsonObject, string propertyName, Option<string> propertyOption)
    {
        return propertyOption.Map(property => jsonObject.AddProperty(propertyName, (JsonNode?)property))
                             .IfNone(jsonObject);
    }

    public static JsonObject AddProperty(this JsonObject jsonObject, string propertyName, JsonNode? property)
    {
        var clonedObject = jsonObject.Clone();
        clonedObject.Add(propertyName, property);
        return clonedObject;
    }

    private static JsonObject Clone(this JsonObject jsonObject) => JsonNodeExtensions.Clone(jsonObject).AsObject();

    public static JsonObject SetProperty(this JsonObject jsonObject, string propertyName, JsonNode? property)
    {
        var clonedObject = jsonObject.Clone();
        clonedObject[propertyName] = property;
        return clonedObject;
    }

    public static async ValueTask<JsonObject> FromStream(Stream? stream, CancellationToken cancellationToken)
    {
        var result = await TryFromStream(stream, cancellationToken);

        return result.IfLeftThrow();
    }

    public static async ValueTask<Either<string, JsonObject>> TryFromStream(Stream? stream, CancellationToken cancellationToken)
    {
        if (stream is null)
        {
            return "Stream cannot be null.";
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        try
        {
            var result = await JsonSerializer.DeserializeAsync<JsonObject>(stream, cancellationToken: cancellationToken);

            return result is null
                    ? "Cannot deserialize stream to JSON object."
                    : result;

        }
        catch (JsonException jsonException)
        {
            return jsonException.Message;
        }
    }

    public static async ValueTask<Stream> ToStream(this JsonObject jsonObject, CancellationToken cancellationToken)
    {
        var stream = new MemoryStream();

        await JsonSerializer.SerializeAsync(stream, jsonObject, cancellationToken: cancellationToken);
        stream.Position = 0;

        return stream;
    }
}

public static class JsonArrayExtensions
{
    public static async ValueTask<JsonArray> ToJsonArray(this IAsyncEnumerable<JsonNode> nodes, CancellationToken cancellationToken)
    {
        var nodesList = await nodes.ToListAsync(cancellationToken);
        return nodesList.ToJsonArray();
    }

    public static JsonArray ToJsonArray(this IEnumerable<JsonNode?> nodes) => new(nodes.ToArray());
}