using System.ComponentModel;
using Mixology.Presentation.Navigation;

namespace Mixology.Desktop.Workspaces;

public interface IDesktopWorkspace : INotifyPropertyChanged, IAsyncDisposable
{
    WorkspaceId Id { get; }

    string Title { get; }

    bool IsDirty { get; }

    Task ActivateAsync(CancellationToken cancellationToken = default);
}
