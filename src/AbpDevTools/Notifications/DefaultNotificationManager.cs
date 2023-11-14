namespace AbpDevTools.Notifications;
public class DefaultNotificationManager : INotificationManager
{
    public Task SendAsync(string title, string message, string icon = null)
    {
        return Task.CompletedTask;
    }
}
