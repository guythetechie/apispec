using Bogus;
using Bogus.DataSets;
using FsCheck;
using FsCheck.Fluent;
using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace core;

public static class Generator
{
    public static Gen<Randomizer> Randomizer { get; } = Gen.Constant(new Randomizer());

    public static Gen<Internet> Internet { get; } = Gen.Constant(new Internet());

    public static Gen<string> WhiteSpaceString { get; } =
        Gen.OneOf(Gen.Constant(string.Empty),
                  GenExtensions.GenerateDefault<char>()
                               .Where(char.IsWhiteSpace)
                               .ArrayOf()
                               .Select(string.Concat));

    public static Gen<string> AlphaNumericString { get; } =
        from randomizer in Randomizer
        from length in GenExtensions.GenerateDefault<PositiveInt>().Select(x => x.Item)
        from alphaNumericString in ArbMap.Default
                                         .ArbFor<string>()
                                         .MapFilter(map: _ => randomizer.AlphaNumeric(length), filter: value => value.All(char.IsLetterOrDigit))
                                         .Generator
        select alphaNumericString;

    public static Gen<string> NonEmptyOrWhiteSpaceString { get; } =
        GenExtensions.GenerateDefault<NonWhiteSpaceString>()
                     .Select(x => x.Item)
                     .Where(x => string.IsNullOrWhiteSpace(x) is false);

    public static Gen<ETag> ETag { get; } =
        from value in NonEmptyOrWhiteSpaceString
        select new ETag(value);

    public static Gen<JsonNode> JsonNode { get; } = GenerateJsonNode();

    public static Gen<JsonObject> JsonObject { get; } = GenerateJsonObject();

    public static Gen<JsonValue> JsonValue { get; } = GenerateJsonValue();

    public static Gen<JsonArray> JsonArray { get; } = GenerateJsonArray();

    private static Gen<JsonValue> GenerateJsonValue()
    {
        return Gen.OneOf(GenerateJsonValue<bool>(),
                         GenerateJsonValue<byte>(),
                         GenerateJsonValue<char>(),
                         GenerateJsonValue<DateTime>(),
                         GenerateJsonValue<DateTimeOffset>(),
                         GenerateJsonValue<decimal>(),
                         GenerateJsonValue<double>(),
                         GenerateJsonValue<Guid>(),
                         GenerateJsonValue<short>(),
                         GenerateJsonValue<int>(),
                         GenerateJsonValue<long>(),
                         GenerateJsonValue<sbyte>(),
                         GenerateJsonValue<float>(),
                         GenerateJsonValue<string>(),
                         GenerateJsonValue<ushort>(),
                         GenerateJsonValue<uint>(),
                         GenerateJsonValue<ulong>());
    }

    public static Gen<JsonValue> GenerateJsonValue<T>()
    {
        var generator = typeof(T) switch
        {
            var type when type == typeof(double) => GenExtensions.GenerateDefault<double>()
                                                                 .Where(double.IsFinite)
                                                                 .Where(d => double.IsNaN(d) is false)
                                                                 .Select(d => (T)(object)d),
            var type when type == typeof(float) => GenExtensions.GenerateDefault<float>()
                                                                .Where(float.IsFinite)
                                                                .Where(f => float.IsNaN(f) is false)
                                                                .Select(f => (T)(object)f),
            _ => GenExtensions.GenerateDefault<T>()
        };

        return generator.Select(t => System.Text.Json.Nodes.JsonValue.Create(t)!);
    }

    private static Gen<JsonNode> GenerateJsonNode()
    {
        return Gen.Sized(GenerateJsonNode);
    }

    private static Gen<JsonNode> GenerateJsonNode(int size)
    {
        return size == 0
                ? GenerateJsonValue().Select(value => value as JsonNode)
                : Gen.OneOf(from jsonValue in GenerateJsonValue()
                            select jsonValue as JsonNode,
                            from jsonObject in GenerateJsonObject(GenerateJsonNode(size / 2))
                            select jsonObject as JsonNode,
                            from jsonArray in GenerateJsonArray(GenerateJsonNode(size / 2))
                            select jsonArray as JsonNode);
    }

    private static Gen<JsonObject> GenerateJsonObject()
    {
        return GenerateJsonObject(GenerateJsonNode());
    }

    private static Gen<JsonObject> GenerateJsonObject(Gen<JsonNode> nodeGenerator)
    {
        return GenExtensions.GenerateDefault<string>()
                            .ListOf()
                            .Select(list => list.Distinct())
                            .SelectMany(list => Gen.CollectToSequence(list,
                                                                      key => from node in nodeGenerator.OrNull()
                                                                             select KeyValuePair.Create(key, node)))
                            .Select(list => new JsonObject(list));
    }

    private static Gen<JsonArray> GenerateJsonArray()
    {
        return GenerateJsonArray(GenerateJsonNode());
    }

    private static Gen<JsonArray> GenerateJsonArray(Gen<JsonNode> nodeGenerator)
    {
        return nodeGenerator.OrNull()
                            .ArrayOf()
                            .Select(array => new JsonArray(array));
    }
}

public static class GenExtensions
{
    public static Gen<T> GenerateDefault<T>()
    {
        return ArbMap.Default.GeneratorFor<T>();
    }

    public static Gen<Seq<T>> SeqOf<T>(this Gen<T> gen)
    {
        return gen.ListOf().Select(list => list.ToSeq());
    }

    public static Gen<Seq<T>> SeqOf<T>(this Gen<T> gen, uint minimum, uint maximum)
    {
        if (minimum > maximum)
        {
            throw new InvalidOperationException("Minimum cannot be greater than maximum.");
        }

        return from count in Gen.Choose((int)minimum, (int)maximum)
               from list in gen.ListOf(count)
               select list.ToSeq();
    }

    public static Gen<T> Elements<T>(this Gen<Seq<T>> gen)
    {
        return gen.Select(x => x.AsEnumerable())
                  .SelectMany(x => Gen.Elements(x));
    }

    public static Gen<Seq<T>> NonEmptySeqOf<T>(this Gen<T> gen)
    {
        return gen.NonEmptyListOf().Select(list => list.ToSeq());
    }

    public static Gen<T?> OrNull<T>(this Gen<T> gen)
    {
        return Gen.OneOf(gen.Select(t => (T?)t), Gen.Constant(default(T?)));
    }

    public static Gen<Seq<T>> SubSeqOf<T>(IEnumerable<T> items)
    {
        return Gen.Constant(items)
                  .Select(items => items.ToSeq())
                  .SelectMany(items => items.IsEmpty
                                        ? Gen.Constant(Seq<T>.Empty)
                                        : Gen.SubListOf(items.AsEnumerable())
                                             .Select(list => list.ToSeq()));
    }

    public static Gen<Seq<T>> DistinctBy<T, TKey>(this Gen<Seq<T>> gen, Func<T, TKey> keySelector)
    {
        return gen.Select(items => items.DistinctBy(keySelector).ToSeq());
    }

    public static Gen<Option<T>> OptionOf<T>(this Gen<T> gen)
    {
        return Gen.OneOf(gen.Select(t => Option<T>.Some(t)), Gen.Constant(Option<T>.None));
    }

    public static T Sample<T>(this Gen<T> gen)
    {
        return gen.Sample(1).Head();
    }

    public static Gen<T> MapFilter<T>(this Gen<T> gen, Func<T, T> map, Func<T, bool> filter)
    {
        return gen.ToArbitrary().MapFilter(map, filter).Generator;
    }
}