using Bogus;
using FsCheck;
using FsCheck.Fluent;
using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;

namespace core;

public static class Generator
{
    public static Gen<Randomizer> Randomizer { get; } = Gen.Constant(new Randomizer());

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

    public static Gen<ETag> ETag { get; } =
        from value in GenExtensions.GenerateDefault<NonWhiteSpaceString>()
        select new ETag(value.Item);
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