using System;
using Notification.Domain.Entities;
using Moq;
using Shouldly;
using Xunit;

namespace Notification.UnitTests;

public class NotificationDomainTests
{
    [Fact]
    public void NotificationLog_Initialization_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var eventType = "OrderShipped";
        var email = "user@example.com";
        var subject = "Your Order Has Shipped!";
        var content = "Tracking ID: 123456";

        // Act
        var log = new NotificationLog
        {
            EventType = eventType,
            RecipientEmail = email,
            Subject = subject,
            Content = content
        };

        // Assert
        log.Id.ShouldNotBe(Guid.Empty);
        log.EventType.ShouldBe(eventType);
        log.RecipientEmail.ShouldBe(email);
        log.Subject.ShouldBe(subject);
        log.Content.ShouldBe(content);
        log.SentAt.ShouldNotBe(default);
    }

    [Fact]
    public void MockNotificationService_UsingMoq_ShouldBeSupported()
    {
        // Arrange
        var mockService = new Mock<IDisposable>();
        mockService.Setup(s => s.Dispose());

        // Act
        mockService.Object.Dispose();

        // Assert
        mockService.Verify(s => s.Dispose(), Times.Once);
    }
}
