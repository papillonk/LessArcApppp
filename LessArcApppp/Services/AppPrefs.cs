using Microsoft.Maui.Storage;
using System;

namespace LessArcApppp
{
    public static class AppPrefs
    {
        private const string NotifLastSeenKey = "notif_last_seen_unix";

        public static DateTimeOffset GetNotifLastSeen()
            => DateTimeOffset.FromUnixTimeSeconds(Preferences.Get(NotifLastSeenKey, 0L));

        public static void TouchNotifLastSeen()
            => Preferences.Set(NotifLastSeenKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }
}
