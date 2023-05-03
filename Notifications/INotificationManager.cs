namespace AbpDevTools.Notifications;
public interface INotificationManager
{
    Task SendAsync(string title, string message = null, string icon = null);
}
