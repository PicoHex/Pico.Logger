namespace PicoLog;

internal sealed class LoggerScopeProvider
{
    private readonly AsyncLocal<Scope?> _currentScope = new();

    public ILogScope Push(object state)
    {
        var scope = new Scope(this, state, _currentScope.Value);
        _currentScope.Value = scope;
        return scope;
    }

    public IReadOnlyList<object>? Capture()
    {
        var current = _currentScope.Value;

        if (current is null)
            return null;

        var scopes = new List<object>();

        while (current is not null)
        {
            scopes.Add(current.State);
            current = current.Parent;
        }

        scopes.Reverse();
        return scopes;
    }

    public static ILogScope Empty { get; } = new EmptyScope();

    private void Pop(Scope scope)
    {
        scope.MarkDisposed();

        if (ReferenceEquals(_currentScope.Value, scope))
            _currentScope.Value = FindNearestActiveAncestor(scope.Parent);
    }

    private static Scope? FindNearestActiveAncestor(Scope? scope)
    {
        while (scope is not null)
        {
            if (!scope.IsDisposed)
                return scope;

            scope = scope.Parent;
        }

        return null;
    }

    private sealed class Scope(LoggerScopeProvider owner, object state, Scope? parent) : ILogScope
    {
        private int _disposed;

        public object State { get; } = state;

        public Scope? Parent { get; } = parent;

        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public void Dispose()
        {
            if (IsDisposed)
                return;

            owner.Pop(this);
        }

        public void MarkDisposed() => Interlocked.Exchange(ref _disposed, 1);
    }

    private sealed class EmptyScope : ILogScope
    {
        public object State { get; } = string.Empty;

        public void Dispose() { }
    }
}
