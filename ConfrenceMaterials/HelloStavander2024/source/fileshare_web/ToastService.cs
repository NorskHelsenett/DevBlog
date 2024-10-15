public class ToastService : IDisposable
{
    public event Action<string, ToastLevel>? OnShow;
    public event Action? OnHide;
    // private Timer? _countdown;

    public async void ShowToast(string message, ToastLevel level, TimeSpan? duration = null)
    {
        OnShow?.Invoke(message, level);
        await Task.Delay(duration ?? TimeSpan.FromSeconds(5));
        OnHide?.Invoke();
        // Task.Run(async () =>
        // {
        // });
        // StartCountdown();
    }

    // private void StartCountdown()
    // {
    //     SetCountdown();
    //
    //     if (_countdown!.Enabled)
    //     {
    //         _countdown.Stop();
    //         _countdown.Start();
    //     }
    //     else
    //     {
    //         _countdown!.Start();
    //     }
    // }
    //
    // private void SetCountdown()
    // {
    //     if (_countdown != null) return;
    //
    //     _countdown = new Timer(5000);
    //     _countdown.Elapsed += HideToast;
    //     _countdown.AutoReset = false;
    // }
    //
    // private void HideToast(object? source, ElapsedEventArgs args)
    //     => OnHide?.Invoke();

    public void Dispose()
    {
        // _countdown?.Dispose();
    }
}

public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Error
}
