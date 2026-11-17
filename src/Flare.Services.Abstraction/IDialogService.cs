namespace Flare.Services.Abstraction;

public interface IDialogService
{
    Task ShowErrorAsync(string title, string message);
}
