using Bogus.DataSets;
using FsCheck;
using FsCheck.Fluent;

namespace core.tests;

internal static class TestGenerator
{
    public static Gen<Internet> Internet { get; } = Gen.Constant(new Internet());
}
