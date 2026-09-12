using Bunit;
using ForgeMission.Presentation;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.Presentation;

public sealed class ConversationTranscriptViewTests : BunitContext
{
    [Fact]
    public void Render_ApprovedEntry_ShowsApprovedText()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.Approval, ConversationParticipant.Approver, ApprovalOutcome: ConversationApprovalOutcome.Approved),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Contains("Approved", component.Markup);
    }

    [Fact]
    public void Render_RevisionRequestedEntry_ShowsFeedback()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.Approval, ConversationParticipant.Approver,
                ApprovalOutcome: ConversationApprovalOutcome.RevisionRequested, Feedback: "needs tests"),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Contains("Revision requested", component.Markup);
        Assert.Contains("needs tests", component.Markup);
    }

    [Fact]
    public void Render_NotApprovedEntry_ShowsNotApprovedText()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.NotApproved, ConversationParticipant.Forge, Feedback: "needs tests"),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Contains("Not approved", component.Markup);
    }

    [Fact]
    public void Render_RehydratedCompletedToolResult_ShowsExactlyOneToolRow()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.ToolCall, ConversationParticipant.Implementer,
                ToolRequestId: Guid.NewGuid(), ToolName: "Read", ToolCompleted: true, ToolIsError: false),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Single(component.FindAll(".convo-tool-row"));
        Assert.Contains("completed", component.Markup);
        // Completed work is history, not activity (43.18).
        Assert.Empty(component.FindAll(".convo-activity"));
    }

    // --- 43.18 task 3: durable entries use the shared renderer ---------------------------------

    [Fact]
    public void Render_TypingEntry_UsesTheSharedThinkingActivity()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.Typing, ConversationParticipant.Implementer, Attempt: 1),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Single(component.FindAll(".convo-activity-thinking"));
        Assert.Contains("Implementer is thinking…", component.Find(".convo-activity-text").TextContent);
        Assert.Empty(component.FindAll(".convo-typing"));
    }

    [Fact]
    public void Render_UnfinishedToolCall_UsesTheSharedWorkingActivityWithTheRunningLabel()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.ToolCall, ConversationParticipant.Implementer,
                ToolRequestId: Guid.NewGuid(), ToolName: "Read", ToolCompleted: false),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Single(component.FindAll(".convo-activity-working"));
        Assert.Contains("Implementer Read running…", component.Find(".convo-activity-text").TextContent);
        Assert.Empty(component.FindAll(".convo-tool-row"));
    }

    [Fact]
    public void Render_UnfinishedThenCompletedToolCalls_KeepsCompletedHistoryAndOneActivity()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.ToolCall, ConversationParticipant.Implementer,
                ToolRequestId: Guid.NewGuid(), ToolName: "Read", ToolCompleted: true),
            new(ConversationEntryKind.ToolCall, ConversationParticipant.Implementer,
                ToolRequestId: Guid.NewGuid(), ToolName: "Edit", ToolCompleted: false),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Single(component.FindAll(".convo-tool-row"));
        Assert.Contains("Read completed", component.Find(".convo-tool-row").TextContent);
        Assert.Single(component.FindAll(".convo-activity-working"));
        Assert.Contains("Edit running…", component.Find(".convo-activity-text").TextContent);
    }

    [Fact]
    public void Render_DurableEntries_NeverClaimStreaming()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.Typing, ConversationParticipant.Proposer, Attempt: 1),
            new(ConversationEntryKind.ToolCall, ConversationParticipant.Implementer,
                ToolRequestId: Guid.NewGuid(), ToolName: "Bash", ToolCompleted: false),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Empty(component.FindAll(".convo-activity-streaming"));
    }

    [Fact]
    public void Render_EntriesFromATwiceAppliedDuplicateEvent_NeverProducesADuplicateBubble()
    {
        var transcript = new ConversationTranscript();
        var evt = new ConversationEvent(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), 1,
            ConversationEventKind.UserMessage, ConversationParticipant.User, null, "Build the thing.",
            null, null, null, null, null, null, DateTimeOffset.UtcNow);
        transcript.Apply(evt);
        transcript.Apply(evt); // duplicate delivery (live/replay overlap)

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, transcript.Entries));

        Assert.Single(component.FindAll(".convo-user-bubble"));
    }

    // Labelled as itself, never folded into a Janus participant.
    [Fact]
    public void AMissionControlMessage_IsLabelledMissionControl()
    {
        var view = Render<ConversationTranscriptView>(parameters => parameters.Add(
            component => component.Entries,
            [new ConversationEntry(ConversationEntryKind.ParticipantMessage,
                ConversationParticipant.MissionControl, Text: "What would done look like?")]));

        Assert.Equal("Mission Control", view.Find(".convo-participant-name").TextContent);
        Assert.Contains("What would done look like?", view.Find(".convo-participant-markdown").TextContent);
    }

    // --- 43.24 task 1: durable participant Markdown renders structurally and inertly -----------

    private static IRenderedComponent<ConversationTranscriptView> RenderParticipant(string markdown, BunitContext context)
        => context.Render<ConversationTranscriptView>(parameters => parameters.Add(
            component => component.Entries,
            [new ConversationEntry(ConversationEntryKind.ParticipantMessage,
                ConversationParticipant.Proposer, Text: markdown)]));

    // Every attribute name on every rendered element — the raw-HTML and link negatives assert
    // against real DOM attributes, never against the markup string, because escaped hostile source
    // is supposed to stay visible and legitimately contains text like "onerror".
    private static IEnumerable<string> AttributeNames(IRenderedComponent<ConversationTranscriptView> view)
        => view.Find(".convo-participant-markdown").QuerySelectorAll("*")
               .SelectMany(element => element.Attributes.Select(attribute => attribute.Name));

    [Fact]
    public void AParticipantMessage_RendersHeadingsListsEmphasisAndInlineCode()
    {
        var view = RenderParticipant(
            """
            ### Step 1: Setup Project Structure

            1. **Create Project Directory**
               - Create a directory named `todolist`.
            2. **Initialize Go Module**
            """, this);

        var markdown = view.Find(".convo-participant-markdown");
        Assert.Single(view.FindAll(".convo-participant-markdown h3"));
        Assert.Equal("Step 1: Setup Project Structure", view.Find(".convo-participant-markdown h3").TextContent);
        Assert.Single(view.FindAll(".convo-participant-markdown ol"));
        Assert.NotEmpty(view.FindAll(".convo-participant-markdown ul"));
        Assert.NotEmpty(view.FindAll(".convo-participant-markdown strong"));
        Assert.Equal("todolist", view.Find(".convo-participant-markdown code").TextContent);
        Assert.DoesNotContain("###", markdown.TextContent);
        Assert.DoesNotContain("**", markdown.TextContent);
        Assert.DoesNotContain("`", markdown.TextContent);
    }

    // Only participant messages are in scope: the same source in a user bubble stays literal.
    [Fact]
    public void AUserMessage_WithTheSameSource_StaysPlainText()
    {
        const string source = "### Heading with **strong** and `code`";

        var view = Render<ConversationTranscriptView>(parameters => parameters.Add(
            component => component.Entries,
            [new ConversationEntry(ConversationEntryKind.UserMessage, ConversationParticipant.User, Text: source)]));

        Assert.Equal(source, view.Find(".convo-user-bubble").TextContent);
        Assert.Empty(view.FindAll(".convo-participant-markdown"));
    }

    [Fact]
    public void AParticipantMessage_RendersFencedCodeTablesTaskListsAndBlockquotes()
    {
        var view = RenderParticipant(
            """
            > Proposed plan.

            ```go
            func main() {}
            ```

            | Field | Type |
            |---|---|
            | ID | int |

            - [x] Define the Task struct
            - [ ] Persist to disk
            """, this);

        Assert.Single(view.FindAll(".convo-participant-markdown blockquote"));
        Assert.Single(view.FindAll(".convo-participant-markdown pre > code"));
        Assert.Contains("func main() {}", view.Find(".convo-participant-markdown pre").TextContent);
        Assert.Single(view.FindAll(".convo-participant-markdown table"));
        Assert.NotEmpty(view.FindAll(".convo-participant-markdown th"));

        // A disabled checkbox is not focusable and adds no new action.
        var checkboxes = view.FindAll(".convo-participant-markdown input[type=\"checkbox\"]");
        Assert.Equal(2, checkboxes.Count);
        Assert.All(checkboxes, checkbox => Assert.True(checkbox.HasAttribute("disabled")));
    }

    // The local rule that hides the redundant marker keys off Markdig's own class, so this pins the
    // class contract the stylesheet depends on — a Markdig change that renamed it would fail here
    // rather than silently restoring a bullet beside every checkbox.
    [Fact]
    public void AParticipantTaskList_MarksItsItemsSoTheRedundantBulletCanBeHidden()
    {
        var view = RenderParticipant("- [x] Define the Task struct\n- [ ] Persist to disk\n\n- an ordinary bullet", this);

        var taskItems = view.FindAll(".convo-participant-markdown li.task-list-item");
        Assert.Equal(2, taskItems.Count);
        Assert.All(taskItems, item => Assert.NotEmpty(item.QuerySelectorAll("input[type=\"checkbox\"]")));

        // Scoped to task-list items only: an ordinary bullet in the same list keeps its marker.
        var plain = view.FindAll(".convo-participant-markdown li").Where(li => !li.ClassList.Contains("task-list-item")).ToList();
        Assert.Single(plain);
        Assert.Empty(plain[0].QuerySelectorAll("input[type=\"checkbox\"]"));

        Assert.Contains(".convo-participant-markdown li.task-list-item { list-style: none; }", view.Markup);
    }

    [Fact]
    public void AParticipantMessage_ContainingRawHtml_ShowsItAsTextWithNoLiveElement()
    {
        var view = RenderParticipant(
            """
            <script>alert(1)</script>

            <img src="https://example.com/x.png" onerror="alert(2)">
            """, this);

        Assert.Empty(view.FindAll(".convo-participant-markdown script"));
        Assert.Empty(view.FindAll(".convo-participant-markdown img"));
        Assert.DoesNotContain(AttributeNames(view), name => name.StartsWith("on", StringComparison.OrdinalIgnoreCase));

        // The escaped source stays visible; that is why these negatives read the DOM, not the markup.
        Assert.Contains("alert(1)", view.Find(".convo-participant-markdown").TextContent);
    }

    [Fact]
    public void AParticipantMessage_ContainingLinksOrImages_EmitsNoNavigationOrFetch()
    {
        var view = RenderParticipant(
            """
            [run it](javascript:alert(1)) and [the docs](https://example.com/docs).

            ![a remote diagram](https://example.com/diagram.png)
            """, this);

        // Negatives first: a content assertion must never shadow the inertness assertion.
        Assert.Empty(view.FindAll(".convo-participant-markdown a"));
        Assert.Empty(view.FindAll(".convo-participant-markdown img"));
        Assert.DoesNotContain("href", AttributeNames(view));
        Assert.DoesNotContain("src", AttributeNames(view));

        var markdown = view.Find(".convo-participant-markdown");
        Assert.Contains("run it", markdown.TextContent);
        Assert.Contains("the docs", markdown.TextContent);
        Assert.Contains("a remote diagram", markdown.TextContent);
    }

    [Fact]
    public void AParticipantMessage_ThatIsMalformedMarkdown_StaysReadableAndLeavesOtherEntriesIntact()
    {
        var view = Render<ConversationTranscriptView>(parameters => parameters.Add(
            component => component.Entries,
            [
                new ConversationEntry(ConversationEntryKind.ParticipantMessage, ConversationParticipant.Proposer,
                    Text: "```go\nfunc main() {\n\n| ragged | table\n|---\n\nstray * asterisk and **unclosed"),
                new ConversationEntry(ConversationEntryKind.Approval, ConversationParticipant.Approver,
                    ApprovalOutcome: ConversationApprovalOutcome.Approved),
            ]));

        var markdown = view.Find(".convo-participant-markdown");
        Assert.Contains("func main()", markdown.TextContent);
        Assert.Contains("stray * asterisk", markdown.TextContent);
        Assert.NotEqual(string.Empty, markdown.TextContent.Trim());
        Assert.Single(view.FindAll(".convo-approval"));
    }

    // ── Phase 48: an event's own expert name is preferred for display ────────────────────────

    [Fact]
    public void Render_ParticipantMessageWithAnActorName_NamesThatExpertRatherThanTheGenericParticipant()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.ParticipantMessage, ConversationParticipant.Forge, Text: "Two-week plan.", ActorName: "Proposer"),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Equal("Proposer", component.Find(".convo-participant-name").TextContent);
        Assert.DoesNotContain("Forge", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WithoutAnActorName_KeepsTheLabelItAlreadyHad()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.ParticipantMessage, ConversationParticipant.Proposer, Text: "Two-week plan."),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Equal("Proposer", component.Find(".convo-participant-name").TextContent);
    }

    [Fact]
    public void Render_TypingWithAnActorName_AnnouncesThatExpertIsWorking()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.Typing, ConversationParticipant.Forge, Attempt: 1, ActorName: "Approver"),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Contains("Approver", component.Markup, StringComparison.Ordinal);
    }
}
