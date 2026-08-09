using Mixology.Presentation.Navigation;
using Mixology.Toolkits.Tui;

namespace Mixology.Tui.Workspaces;

public interface ITuiWorkspace : IAsyncDisposable
{
    WorkspaceId Id { get; }
    string Title { get; }
    InputOwnership InputOwnership { get; }
    TuiError? Status { get; }
    event Action? Changed;
    Task ActivateAsync(CancellationToken cancellationToken = default);
    Task RefreshAsync(CancellationToken cancellationToken = default);
    string Render(Viewport viewport);
    bool Handle(char key);
}
