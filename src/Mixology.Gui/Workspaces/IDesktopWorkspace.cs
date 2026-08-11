using System.ComponentModel;
using Mixology.Presentation.Navigation;

namespace Mixology.Gui.Workspaces;

public interface IDesktopWorkspace : INotifyPropertyChanged, IAsyncDisposable
{
    WorkspaceId Id { get; }

    string Title { get; }

    bool IsDirty { get; }

    Task ActivateAsync(CancellationToken cancellationToken = default);
}
