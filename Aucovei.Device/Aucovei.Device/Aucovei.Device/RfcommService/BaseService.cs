using System;

namespace Aucovei.Device.RfcommService
{
    public abstract class BaseService
    {
        public delegate void UINotificationEventHandler(object sender, NotifyUIEventArgs e);

        public event UINotificationEventHandler NotifyUIEventHandler;

        protected void NotifyUIEvent(NotifyUIEventArgs args)
        {
            this.NotifyUIEventHandler?.Invoke(this, args);
        }
    }

    public enum NotificationType
    {
        Console,
        ControlMode,
        ButtonState
    }

    public class NotifyUIEventArgs : EventArgs
    {
        public NotificationType NotificationType;

        public string Name;

        public string Data;
    }

}