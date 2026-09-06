using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;

namespace ForgeMission.Presentation;

/// <summary>
/// Renders one durable participant message's Markdown as inert HTML. The only code in the Desktop
/// path that touches Markdig, and the only source whose output <see cref="MarkupString"/> may
/// bypass Razor encoding.
/// </summary>
internal static class ConversationMarkdownRenderer
{
    // Fixed feature set, built once. Pipe tables and task lists are the only extensions; advanced
    // extensions are deliberately absent because they enable media, generic HTML attributes, and
    // diagrams this seam neither needs nor owns.
    //
    // DisableHtml() MUST stay last. Verified on Markdig 1.3.2: any Use<>() call placed after it
    // restores the raw-HTML parsers, and a participant response's <script> then reaches the DOM
    // live instead of escaped. The failure is silent — nothing throws and every other feature still
    // renders. AParticipantMessage_ContainingRawHtml_ShowsItAsTextWithNoLiveElement is the
    // regression guard; it fails if this line is moved.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseTaskLists()
        .Use<InertLinkExtension>()
        .DisableHtml()
        .Build();

    public static MarkupString Render(string? markdown)
        => new(string.IsNullOrEmpty(markdown) ? string.Empty : Markdown.ToHtml(markdown, Pipeline));

    /// <summary>
    /// Replaces Markdig's stock link renderer so no response content can reach the DOM as a
    /// navigation target or a remote request. Structural, not a post-render string filter.
    /// </summary>
    private sealed class InertLinkExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline) { }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is HtmlRenderer html)
                html.ObjectRenderers.Replace<LinkInlineRenderer>(new InertLinkRenderer());
        }
    }

    /// <summary>Writes a link's label and an image's alt text as ordinary escaped text.</summary>
    private sealed class InertLinkRenderer : HtmlObjectRenderer<LinkInline>
    {
        protected override void Write(HtmlRenderer renderer, LinkInline link)
            => renderer.WriteChildren(link);
    }
}
