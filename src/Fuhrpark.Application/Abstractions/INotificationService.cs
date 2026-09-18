using Fuhrpark.Application.Dtos;
using Fuhrpark.Domain.Entities;

namespace Fuhrpark.Application.Abstractions;

/// <summary>Internes Notification Center.</summary>
public interface INotificationService
{
    Task<int> CreateAsync(Notification notification, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationListItem>> GetForCurrentUserAsync(
        bool onlyUnread = false,
        int maxCount = 100,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(CancellationToken cancellationToken = default);

    Task DismissAsync(int notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Berechnet Fristen (TUEV, Wartung, Kennzeichenreservierung, Versicherung) und legt
    /// fehlende Benachrichtigungen an. Wird beim Programmstart und zyklisch aufgerufen.
    /// </summary>
    Task<int> RefreshDueNotificationsAsync(CancellationToken cancellationToken = default);
}
