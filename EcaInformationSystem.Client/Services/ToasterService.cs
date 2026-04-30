namespace EcaInformationSystem.Client.Services
{
    public class ToastService
    {
        public event Action<string, string, bool>? OnShow;

        public void ShowSuccess(string message, string title = "Success")
            => OnShow?.Invoke(title, message, true);

        public void ShowError(string message, string title = "Error")
            => OnShow?.Invoke(title, message, false);
    }
}