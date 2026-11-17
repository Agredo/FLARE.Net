using Flare.Services.Abstraction;

namespace Flare.Maui.Services;

/// <summary>Shows alerts on the current main page.</summary>
public sealed class MauiDialogService : IDialogService
{
    public Task ShowErrorAsync(string title, string message)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null) return Task.CompletedTask;
        return MainThread.InvokeOnMainThreadAsync(() => page.DisplayAlertAsync(title, message, "OK"));
    }
}
