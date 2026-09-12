namespace ForgeMission.Application;

/// <summary>
/// The managed chat Project's local routing marker (Phase 48). It holds the Project GUID and
/// nothing else: no path, transcript, account record, credential, endpoint, or new user setting.
/// Its home is derived from that GUID, so the marker is the only thing that has to be durable.
/// </summary>
internal sealed record ManagedChatProjectMarker(Guid ProjectId);

/// <summary>What reading the marker found. <see cref="Invalid"/> covers both an unreadable file and
/// one that parses but names no Project, because the repair is identical for either.</summary>
internal enum ManagedChatProjectMarkerState { Missing, Found, Invalid }

internal sealed record ManagedChatProjectMarkerRead(ManagedChatProjectMarkerState State, Guid ProjectId);
