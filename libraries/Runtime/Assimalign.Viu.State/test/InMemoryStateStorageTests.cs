using Shouldly;
using Xunit;

namespace Assimalign.Viu.State.Tests;

// [STA-11] DOM-free hosts use the same explicit string storage contract as browser persistence.
public sealed class InMemoryStateStorageTests
{
    [Fact]
    public void WriteAndTryRead_RepeatedKey_ReplacesExactStringValue()
    {
        IStateStorage storage = new InMemoryStateStorage();
        storage.TryRead("preferences", out _).ShouldBeFalse();

        storage.Write("preferences", "first");
        storage.Write("preferences", "{\"title\":\"second\"}");

        storage.TryRead("preferences", out string value).ShouldBeTrue();
        value.ShouldBe("{\"title\":\"second\"}");
    }

    [Fact]
    public void Keys_DifferentCase_AreOrdinalAndIndependent()
    {
        IStateStorage storage = new InMemoryStateStorage();
        storage.Write("Preferences", "upper");
        storage.Write("preferences", "lower");

        storage.TryRead("Preferences", out string upper).ShouldBeTrue();
        storage.TryRead("preferences", out string lower).ShouldBeTrue();
        upper.ShouldBe("upper");
        lower.ShouldBe("lower");
    }

    [Fact]
    public void Remove_ExistingAndMissingKeys_IsIdempotentAndPreservesOtherKeys()
    {
        IStateStorage storage = new InMemoryStateStorage();
        storage.Write("remove", "gone");
        storage.Write("keep", string.Empty);

        storage.Remove("remove");
        storage.Remove("remove");

        storage.TryRead("remove", out _).ShouldBeFalse();
        storage.TryRead("keep", out string value).ShouldBeTrue();
        value.ShouldBe(string.Empty);
    }

    [Fact]
    public void Instances_SameKey_DoNotShareValues()
    {
        IStateStorage first = new InMemoryStateStorage();
        IStateStorage second = new InMemoryStateStorage();
        first.Write("preferences", "first host");

        second.TryRead("preferences", out _).ShouldBeFalse();
    }
}
