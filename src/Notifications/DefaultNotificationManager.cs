using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbpDevTools.Notifications;
public class DefaultNotificationManager : INotificationManager
{
    public Task SendAsync(string title, string message, string icon = null)
    {
        return Task.CompletedTask;
    }
}
