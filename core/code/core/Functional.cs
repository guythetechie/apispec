using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace core;

public static class OptionExtensions
{
    public static T IfNoneThrow<T>(this Option<T> option, string errorMessage)
    {
        return option.IfNone(() => throw new InvalidOperationException(errorMessage));
    }
}

public static class IAsyncEnumerableExtensions
{
    public static async ValueTask<Seq<T>> ToSeq<T>(this IAsyncEnumerable<T> enumerable, CancellationToken cancellationToken)
    {
        return await enumerable.ToListAsync(cancellationToken)
                               .Map(items => items.ToSeq());
    }
}

public static class IDictionaryExtensions
{
    public static Option<TValue> Find<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
    {
        return dictionary.TryGetValue(key, out var value)
                ? value
                : Option<TValue>.None;
    }
}

public static class EitherAsyncExtensions
{
    public static EitherAsync<TLeft, TRight> ToAsync<TLeft, TRight>(this ValueTask<Either<TLeft, TRight>> either)
    {
        return either.AsTask().ToAsync();
    }
}