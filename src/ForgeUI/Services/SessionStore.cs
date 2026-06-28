using ForgeUI.Models;

namespace ForgeUI.Services;

public class Session
{
    public string              Id        { get; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime            StartedAt { get; } = DateTime.UtcNow;
    public List<ChatMessage>   Messages  { get; } = [];
    public string              Title     => Messages.FirstOrDefault()?.UserText ?? "New session";
    public TrustSignal?        LastTrust => Messages.LastOrDefault()?.Trust;
}

public class SessionStore
{
    private readonly List<Session> _sessions = [];
    private Session _current = new();

    public IReadOnlyList<Session> All     => _sessions;
    public Session                Current => _current;

    public event Action? Changed;

    public Session New()
    {
        if (_current.Messages.Count > 0)
            _sessions.Insert(0, _current);
        _current = new Session();
        Changed?.Invoke();
        return _current;
    }

    public void Switch(Session session)
    {
        Persist(_current);
        _current = session;
        Changed?.Invoke();
    }

    public void Save()
    {
        Persist(_current);
        Changed?.Invoke();
    }

    private void Persist(Session session)
    {
        if (session.Messages.Count > 0 && !_sessions.Contains(session))
            _sessions.Insert(0, session);
    }
}
