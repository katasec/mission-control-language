using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

// Exposes the private namespace-based Generate primitive to ConversationHost.Tests only, so the
// RFC 4122 known-vector assertion (NAMESPACE_DNS + "python.org") exercises the actual algorithm
// under test rather than a duplicate reimplementation — the four public methods' locked namespace
// and name formats are unaffected.
[assembly: InternalsVisibleTo("ForgeMission.ConversationHost.Tests")]

namespace ForgeMission.Conversations.Contracts;

/// <summary>
/// RFC 4122 UUID v5 (SHA-1, name-based) IDs deterministic in their inputs — used wherever a
/// durable fact's identity must be reproducible from data already on hand (a command ID, a tool
/// result's event ID) rather than freshly generated, so retries and repairs never mint a second
/// identity for the same logical fact. Dependency-free: only BCL SHA-1, no package. Host and
/// Worker both depend on this for the mission-command outbox's continuation command, Worker
/// progress-fact ordinals, Worker tool-request/provider-call correlation, and both sides'
/// dead-letter-derived facts.
/// </summary>
public static class ConversationDeterministicIds
{
    private static readonly Guid Namespace = Guid.Parse("d6a0c730-a25d-5cbf-a047-8ee9a1c0f171");

    /// <summary>The deterministic conversation ID for a <c>POST /conversations</c> start request —
    /// derived from the client's own <c>CommandId</c> so an exact retry (lost response, redelivery)
    /// always lands on the same conversation instead of minting a second one.</summary>
    public static Guid Conversation(Guid commandId)
        => Generate($"conversation:{commandId:N}");

    /// <summary>The deterministic first-run ID paired with <see cref="Conversation"/> for the same
    /// start request.</summary>
    public static Guid InitialRun(Guid commandId)
        => Generate($"initial-run:{commandId:N}");

    /// <summary>The ContinueAfterTool command derived from a matching ToolResult.</summary>
    public static Guid Continuation(Guid toolResultEventId)
        => Generate($"continuation:{toolResultEventId:N}");

    /// <summary>The Client Runtime's own <c>SubmitToolResultRequest.CommandId</c> for one
    /// authorized local tool hand-off — stable across an HTTP retry or Client Runtime reconnect,
    /// so a resend reaches the grain's original accepted response rather than appending a second
    /// result/continuation for the same <paramref name="toolRequestId"/>.</summary>
    public static Guid ClientToolResult(Guid toolRequestId)
        => Generate($"client-tool-result:{toolRequestId:N}");

    /// <summary>The <c>CommandId</c> Client Runtime sends to create a Project's Mission Control
    /// conversation — derived from the stable local manifest <c>projectId</c>, so a retry after
    /// Host acceptance but before the manifest write re-derives the same command ID, lands on the
    /// same deterministic conversation, and returns its original acceptance instead of creating a
    /// second control conversation for the same Project (43.20 task 2).</summary>
    public static Guid ProjectControlCreate(Guid projectId)
        => Generate($"project-control-create:{projectId:N}");

    /// <summary>The <c>CommandId</c> Client Runtime sends to create a Project's Mission container
    /// (43.21 task 1) — derived from the stable local manifest <c>projectId</c> for the same reason
    /// as <see cref="ProjectControlCreate"/>: a retry after Host acceptance but before the manifest
    /// write re-derives the same command ID and lands on the same container. Deliberately a
    /// DIFFERENT name prefix from the control create, so a migrated Project's new container can
    /// never collide with the control conversation it is replacing.</summary>
    public static Guid ProjectMissionContainerCreate(Guid projectId)
        => Generate($"project-mission-container-create:{projectId:N}");

    /// <summary>The child Mission Run a Project Mission command starts (43.21 task 1). Derived
    /// from the submission's own <c>CommandId</c> rather than freshly generated, because the
    /// duplicate-resolution path compares a RECONSTRUCTED command against the stored one: a fresh
    /// run id would make every legitimate retry look like a conflicting reuse of the command id.
    /// Deterministic here means an equal retry returns the original run, which is exactly the
    /// contract.</summary>
    public static Guid ProjectMissionRun(Guid commandId)
        => Generate($"project-mission-run:{commandId:N}");

    /// <summary>The Nth progress fact the Worker sends while processing one command.</summary>
    public static Guid Progress(Guid commandId, int ordinal)
        => Generate($"progress:{commandId:N}:{ordinal}");

    /// <summary>The one supported tool request's Forge-level correlation ID — matched against a
    /// later <c>ConversationToolResult.RequestId</c>. Distinct from the provider's own tool-call
    /// ID, which Worker session state retains separately for reconstructing the continuation
    /// conversation.</summary>
    public static Guid ToolRequest(Guid commandId, int ordinal)
        => Generate($"tool-request:{commandId:N}:{ordinal}");

    /// <summary>A dead-letter-derived fact (Host progress DLQ or Worker command DLQ).</summary>
    public static Guid DeadLetter(Guid eventId, string kind)
        => Generate($"dead-letter:{kind}:{eventId:N}");

    private static Guid Generate(string name) => Generate(Namespace, name);

    // RFC 4122 §4.3: SHA-1(namespace-bytes-in-network-order + name-bytes), version nibble set to
    // 0101, variant bits set to 10xx. Guid.ToByteArray()'s first three fields are little-endian on
    // this platform, the opposite of the network byte order the RFC requires, so those three
    // fields are byte-swapped both when hashing the namespace and when reassembling the result.
    // Internal (not private) solely so the RFC known-vector test can exercise this exact algorithm
    // against NAMESPACE_DNS rather than a duplicate reimplementation.
    internal static Guid Generate(Guid ns, string name)
    {
        var namespaceBytes = ns.ToByteArray();
        SwapFieldByteOrder(namespaceBytes);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var buffer = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(buffer, 0);
        nameBytes.CopyTo(buffer, namespaceBytes.Length);

        var hash = SHA1.HashData(buffer);
        var result = hash[..16];
        result[6] = (byte)((result[6] & 0x0F) | 0x50); // version 5
        result[8] = (byte)((result[8] & 0x3F) | 0x80); // variant RFC 4122

        SwapFieldByteOrder(result);
        return new Guid(result);
    }

    private static void SwapFieldByteOrder(byte[] guidBytes)
    {
        Array.Reverse(guidBytes, 0, 4);
        Array.Reverse(guidBytes, 4, 2);
        Array.Reverse(guidBytes, 6, 2);
    }
}
