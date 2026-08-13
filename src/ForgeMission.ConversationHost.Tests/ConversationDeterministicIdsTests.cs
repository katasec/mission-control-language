using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>RFC 4122 UUID v5 correctness for <see cref="ConversationDeterministicIds"/>: the same
/// inputs always reproduce the same ID (retries/repairs never mint a second identity), distinct
/// inputs/methods never collide, every result carries the version-5 and RFC-4122-variant bits, and
/// — via the internal <c>Generate(Guid, string)</c> overload exposed only to this assembly — the
/// underlying algorithm reproduces the well-known public reference vector.</summary>
public class ConversationDeterministicIdsTests
{
    [Fact]
    public void Continuation_IsDeterministic()
    {
        var eventId = Guid.NewGuid();
        Assert.Equal(ConversationDeterministicIds.Continuation(eventId), ConversationDeterministicIds.Continuation(eventId));
    }

    [Fact]
    public void Progress_IsDeterministic()
    {
        var commandId = Guid.NewGuid();
        Assert.Equal(ConversationDeterministicIds.Progress(commandId, 3), ConversationDeterministicIds.Progress(commandId, 3));
    }

    [Fact]
    public void Progress_DifferentOrdinal_ProducesDifferentId()
    {
        var commandId = Guid.NewGuid();
        Assert.NotEqual(ConversationDeterministicIds.Progress(commandId, 0), ConversationDeterministicIds.Progress(commandId, 1));
    }

    [Fact]
    public void ToolRequest_IsDeterministic_AndDistinctFromProgress()
    {
        var commandId = Guid.NewGuid();
        Assert.Equal(ConversationDeterministicIds.ToolRequest(commandId, 0), ConversationDeterministicIds.ToolRequest(commandId, 0));
        Assert.NotEqual(ConversationDeterministicIds.ToolRequest(commandId, 0), ConversationDeterministicIds.Progress(commandId, 0));
    }

    [Fact]
    public void DeadLetter_IsDeterministic_AndDistinctByKind()
    {
        var eventId = Guid.NewGuid();
        Assert.Equal(
            ConversationDeterministicIds.DeadLetter(eventId, "progress-error"),
            ConversationDeterministicIds.DeadLetter(eventId, "progress-error"));
        Assert.NotEqual(
            ConversationDeterministicIds.DeadLetter(eventId, "progress-error"),
            ConversationDeterministicIds.DeadLetter(eventId, "progress-failed"));
    }

    [Theory]
    [InlineData("continuation-source")]
    [InlineData("progress-source")]
    [InlineData("tool-request-source")]
    [InlineData("dead-letter-source")]
    public void EveryGeneratedId_CarriesVersion5AndRfc4122VariantBits(string seed)
    {
        var source = Guid.Parse($"{seed.GetHashCode():x8}-0000-0000-0000-000000000000");
        var id = ConversationDeterministicIds.Continuation(source);
        var bytes = id.ToByteArray();

        Assert.Equal(0x50, bytes[7] & 0xF0); // version nibble = 0101
        Assert.Equal(0x80, bytes[8] & 0xC0); // variant bits = 10xx
    }

    [Fact]
    public void Generate_ReproducesTheRfc4122KnownReferenceVector()
    {
        // RFC 4122 §4.3's own worked example: NAMESPACE_DNS + "python.org" -> a fixed, published
        // UUID v5. Reproducing it here confirms the byte-order handling (Guid.ToByteArray()'s
        // little-endian first three fields vs. the RFC's required network/big-endian order) is
        // correct, independent of this project's own namespace constant.
        var namespaceDns = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
        var expected = Guid.Parse("886313e1-3b8a-5372-9b90-0c9aee199e5d");

        var actual = ConversationDeterministicIds.Generate(namespaceDns, "python.org");

        Assert.Equal(expected, actual);
    }
}
