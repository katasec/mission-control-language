namespace ForgeUI.Models;

/// <summary>
/// Shell-chrome state cascaded from <c>MainLayout</c> (40.3) so a page can ask the shell to change
/// shape — specifically, hide the bottom tab bar for an immersive mobile conversation. Event-driven
/// and tiny: the cascaded reference is fixed, pages mutate its flags and fire <see cref="Changed"/>,
/// and the layout re-renders. No JS interop, no body-class magic.
/// </summary>
public sealed class ShellChrome
{
    /// <summary>True while a room is open, so the shell can drop the tab bar on the phone
    /// (the CSS gates this to &lt; 720px; on desktop the flag is inert).</summary>
    public bool ImmersiveConversation { get; private set; }

    public event Action? Changed;

    public void SetImmersive(bool value)
    {
        if (ImmersiveConversation == value)
            return;
        ImmersiveConversation = value;
        Changed?.Invoke();
    }
}
