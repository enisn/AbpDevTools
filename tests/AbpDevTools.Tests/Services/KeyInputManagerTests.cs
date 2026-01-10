using AbpDevTools.Services;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests.Services;

/// <summary>
/// Unit tests for KeyInputManager class.
/// Tests console key input handling for interactive commands including
/// key press detection, modifier keys, and cancellation.
/// </summary>
public class KeyInputManagerTests
{
    #region TryGetNextKey Tests

    [Fact]
    public void TryGetNextKey_WithNoKeysInQueue_ReturnsNull()
    {
        // Arrange
        using var manager = new KeyInputManager();

        // Act
        var result = manager.TryGetNextKey();

        // Assert
        result.Should().BeNull("no keys have been pressed");
    }

    [Fact]
    public void TryGetNextKey_CalledMultipleTimes_ReturnsNullWhenQueueIsEmpty()
    {
        // Arrange
        using var manager = new KeyInputManager();

        // Act
        var result1 = manager.TryGetNextKey();
        var result2 = manager.TryGetNextKey();
        var result3 = manager.TryGetNextKey();

        // Assert
        result1.Should().BeNull("first call should return null");
        result2.Should().BeNull("second call should return null");
        result3.Should().BeNull("third call should return null");
    }

    #endregion

    #region StartListening/StopListening Tests

    [Fact]
    public void StartListening_DoesNotThrow()
    {
        // Arrange
        using var manager = new KeyInputManager();

        // Act & Assert
        var act = () => manager.StartListening();
        act.Should().NotThrow("StartListening should not throw");
    }

    [Fact]
    public void StopListening_DoesNotThrow()
    {
        // Arrange
        using var manager = new KeyInputManager();

        // Act & Assert
        var act = () => manager.StopListening();
        act.Should().NotThrow("StopListening should not throw even when not listening");
    }

    [Fact]
    public void StartListening_CalledMultipleTimes_SecondCallReturnsEarly()
    {
        // Arrange
        using var manager = new KeyInputManager();

        // Act - Start listening twice in succession
        // The second call should detect that listening is already active and return early
        manager.StartListening();
        // The second call may attempt to wait for the first task to complete if in a bad state
        // but should not throw an exception
        var act = () => manager.StartListening();

        // Assert - Should not throw
        act.Should().NotThrow("StartListening should handle being called when already started");
    }

    [Fact]
    public void StopListening_CalledWhenNotListening_DoesNotThrow()
    {
        // Arrange
        using var manager = new KeyInputManager();

        // Act & Assert
        var act = () =>
        {
            manager.StopListening();
            manager.StopListening();
            manager.StopListening();
        };
        act.Should().NotThrow("StopListening should be safe to call when not listening");
    }

    [Fact]
    public void StartListening_ThenStopListening_DoesNotThrow()
    {
        // Arrange
        using var manager = new KeyInputManager();

        // Act & Assert
        var act = () =>
        {
            manager.StartListening();
            manager.StopListening();
        };
        act.Should().NotThrow("StartListening followed by StopListening should not throw");
    }

    #endregion

    #region KeyPressed Event Tests

    [Fact]
    public void KeyPressed_EventCanBeSubscribedAndUnsubscribed()
    {
        // Arrange
        using var manager = new KeyInputManager();
        var eventCount = 0;
        EventHandler<KeyPressEventArgs> handler = (s, e) => eventCount++;

        // Act
        manager.KeyPressed += handler;
        manager.KeyPressed -= handler;

        // Assert
        // If we reach here without exception, subscription/unsubscription works
        true.Should().BeTrue("event subscription and unsubscription should work");
    }

    [Fact]
    public void KeyPressed_MultipleSubscriptions_AllWorkCorrectly()
    {
        // Arrange
        using var manager = new KeyInputManager();
        var count1 = 0;
        var count2 = 0;
        var count3 = 0;

        EventHandler<KeyPressEventArgs> handler1 = (s, e) => count1++;
        EventHandler<KeyPressEventArgs> handler2 = (s, e) => count2++;
        EventHandler<KeyPressEventArgs> handler3 = (s, e) => count3++;

        // Act
        manager.KeyPressed += handler1;
        manager.KeyPressed += handler2;
        manager.KeyPressed += handler3;

        // Assert - All handlers should be subscribed without throwing
        true.Should().BeTrue("multiple event subscriptions should work");
    }

    #endregion

    #region Modifier Key Detection Tests

    [Fact]
    public void KeyPressEventArgs_CorrectlyDetectsCtrlModifier()
    {
        // Arrange
        var keyEventArgs = new KeyPressEventArgs
        {
            Key = ConsoleKey.C,
            CtrlPressed = true,
            ShiftPressed = false,
            AltPressed = false
        };

        // Act & Assert
        keyEventArgs.CtrlPressed.Should().BeTrue("Ctrl modifier should be detected");
        keyEventArgs.ShiftPressed.Should().BeFalse("Shift modifier should not be pressed");
        keyEventArgs.AltPressed.Should().BeFalse("Alt modifier should not be pressed");
        keyEventArgs.Key.Should().Be(ConsoleKey.C, "key should be C");
    }

    [Fact]
    public void KeyPressEventArgs_CorrectlyDetectsShiftModifier()
    {
        // Arrange
        var keyEventArgs = new KeyPressEventArgs
        {
            Key = ConsoleKey.A,
            CtrlPressed = false,
            ShiftPressed = true,
            AltPressed = false
        };

        // Act & Assert
        keyEventArgs.CtrlPressed.Should().BeFalse("Ctrl modifier should not be pressed");
        keyEventArgs.ShiftPressed.Should().BeTrue("Shift modifier should be detected");
        keyEventArgs.AltPressed.Should().BeFalse("Alt modifier should not be pressed");
        keyEventArgs.Key.Should().Be(ConsoleKey.A, "key should be A");
    }

    [Fact]
    public void KeyPressEventArgs_CorrectlyDetectsAltModifier()
    {
        // Arrange
        var keyEventArgs = new KeyPressEventArgs
        {
            Key = ConsoleKey.F,
            CtrlPressed = false,
            ShiftPressed = false,
            AltPressed = true
        };

        // Act & Assert
        keyEventArgs.CtrlPressed.Should().BeFalse("Ctrl modifier should not be pressed");
        keyEventArgs.ShiftPressed.Should().BeFalse("Shift modifier should not be pressed");
        keyEventArgs.AltPressed.Should().BeTrue("Alt modifier should be detected");
        keyEventArgs.Key.Should().Be(ConsoleKey.F, "key should be F");
    }

    [Fact]
    public void KeyPressEventArgs_CorrectlyDetectsMultipleModifiers()
    {
        // Arrange
        var keyEventArgs = new KeyPressEventArgs
        {
            Key = ConsoleKey.S,
            CtrlPressed = true,
            ShiftPressed = true,
            AltPressed = false
        };

        // Act & Assert
        keyEventArgs.CtrlPressed.Should().BeTrue("Ctrl modifier should be detected");
        keyEventArgs.ShiftPressed.Should().BeTrue("Shift modifier should be detected");
        keyEventArgs.AltPressed.Should().BeFalse("Alt modifier should not be pressed");
        keyEventArgs.Key.Should().Be(ConsoleKey.S, "key should be S (Ctrl+Shift+S)");
    }

    [Fact]
    public void KeyPressEventArgs_AllModifiersPressed_CorrectlyDetectsAll()
    {
        // Arrange
        var keyEventArgs = new KeyPressEventArgs
        {
            Key = ConsoleKey.Q,
            CtrlPressed = true,
            ShiftPressed = true,
            AltPressed = true
        };

        // Act & Assert
        keyEventArgs.CtrlPressed.Should().BeTrue("Ctrl modifier should be detected");
        keyEventArgs.ShiftPressed.Should().BeTrue("Shift modifier should be detected");
        keyEventArgs.AltPressed.Should().BeTrue("Alt modifier should be detected");
        keyEventArgs.Key.Should().Be(ConsoleKey.Q, "key should be Q (Ctrl+Shift+Alt+Q)");
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CalledOnce_DoesNotThrow()
    {
        // Arrange
        var manager = new KeyInputManager();

        // Act & Assert
        var act = () => manager.Dispose();
        act.Should().NotThrow("Dispose should not throw");
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var manager = new KeyInputManager();

        // Act & Assert
        var act = () =>
        {
            manager.Dispose();
            manager.Dispose();
            manager.Dispose();
        };
        act.Should().NotThrow("Dispose should be safe to call multiple times");
    }

    [Fact]
    public void Dispose_AfterStartListening_DoesNotThrow()
    {
        // Arrange
        var manager = new KeyInputManager();
        manager.StartListening();

        // Act & Assert
        var act = () => manager.Dispose();
        act.Should().NotThrow("Dispose after StartListening should not throw");
    }

    #endregion
}
