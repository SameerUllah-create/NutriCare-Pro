namespace NutriCare.Tests.Integrations;

using Microsoft.Extensions.Configuration;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;
using NutriCare.Infrastructure.Notifications;
using NutriCare.API.Hubs;

public class NotificationOutboxTests
{
    [Fact]
    public async Task OutboxHonorsPreferencesAndDeduplicatesStableEvent()
    {
        var users = new MemoryRepository<User>(); users.Items.Add(new User { Id = 1, EmailNotificationsEnabled = false, SmsNotificationsEnabled = false, InAppNotificationsEnabled = true });
        var rows = new MemoryRepository<Notification>();
        var service = new NotificationService(rows, users, new MemoryUnitOfWork(), new ConfigurationBuilder().Build());
        var request = new NotificationRequest("NewMessage", "New message", "Sign in securely", "Message", 10, "/messages", ["InApp", "Email", "SMS"]);
        await service.EnqueueAsync(new NotificationRecipient(1, "patient@example.test", "+92000000000", ""), request, "message:10");
        await service.EnqueueAsync(new NotificationRecipient(1, "patient@example.test", "+92000000000", ""), request, "message:10");
        var row = Assert.Single(rows.Items); Assert.Equal("InApp", row.Channel); Assert.Equal("Pending", row.Status); Assert.Null(row.SentAt);
    }

    [Fact]
    public async Task PresenceKeepsUserOnlineUntilLastConnectionDisconnects()
    {
        var presence = new ChatPresenceTracker();
        Assert.True(await presence.UserConnectedAsync(1, "a")); Assert.False(await presence.UserConnectedAsync(1, "b"));
        Assert.False(await presence.UserDisconnectedAsync(1, "a")); Assert.True(presence.IsOnline(1));
        Assert.True(await presence.UserDisconnectedAsync(1, "b")); Assert.False(presence.IsOnline(1));
    }
}
