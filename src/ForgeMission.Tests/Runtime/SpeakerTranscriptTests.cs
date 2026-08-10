using ForgeMission.Core.Runtime;
using Microsoft.Extensions.AI;

namespace ForgeMission.Tests.Runtime;

public class SpeakerTranscriptTests
{
    [Fact]
    public void AsMessages_RetagsTurnsForTheReceivingSpeaker()
    {
        var transcript = new SpeakerTranscript();
        transcript.Add("Proposer", "draft plan");
        transcript.Add("Approver", "add rollback details");

        var proposerView = transcript.AsMessages("Proposer");
        var approverView = transcript.AsMessages("Approver");

        Assert.Equal([ChatRole.Assistant, ChatRole.User], proposerView.Select(m => m.Role));
        Assert.Equal([ChatRole.User, ChatRole.Assistant], approverView.Select(m => m.Role));
        Assert.Equal("draft plan", proposerView[0].Text);
        Assert.Equal("add rollback details", proposerView[1].Text);
    }
}
