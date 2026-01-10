using Shouldly;
using System.Text;

namespace AbpDevTools.Tests.Helpers;

/// <summary>
/// Base class for command tests following AAA (Arrange-Act-Assert) pattern.
/// Provides common setup for testing CLI commands using CliFx.
/// </summary>
public abstract class CommandTestBase : IDisposable
{
    protected List<string> OutputLines { get; }
    protected List<string> ErrorLines { get; }

    protected CommandTestBase()
    {
        OutputLines = new List<string>();
        ErrorLines = new List<string>();
    }

    /// <summary>
    /// Creates a string builder for tracking test output.
    /// </summary>
    protected StringBuilder CreateOutputTracker()
    {
        return new StringBuilder();
    }

    /// <summary>
    /// Creates a string builder for tracking test errors.
    /// </summary>
    protected StringBuilder CreateErrorTracker()
    {
        return new StringBuilder();
    }

    /// <summary>
    /// Gets all output lines as a single string.
    /// </summary>
    protected string GetOutput() => string.Join(Environment.NewLine, OutputLines);

    /// <summary>
    /// Gets all error lines as a single string.
    /// </summary>
    protected string GetError() => string.Join(Environment.NewLine, ErrorLines);

    /// <summary>
    /// Clears captured output and error lines.
    /// </summary>
    protected void ClearOutput()
    {
        OutputLines.Clear();
        ErrorLines.Clear();
    }

    /// <summary>
    /// Assert helper methods for common command test assertions.
    /// </summary>
    protected static class CommandAssertions
    {
        /// <summary>
        /// Asserts that the output contains the specified text.
        /// </summary>
        public static void OutputShouldContain(string output, string expectedText)
        {
            output.ShouldContain(expectedText);
        }

        /// <summary>
        /// Asserts that the output does not contain the specified text.
        /// </summary>
        public static void OutputShouldNotContain(string output, string unexpectedText)
        {
            output.ShouldNotContain(unexpectedText);
        }

        /// <summary>
        /// Asserts that the error output contains the specified text.
        /// </summary>
        public static void ErrorShouldContain(string error, string expectedText)
        {
            error.ShouldContain(expectedText);
        }

        /// <summary>
        /// Asserts that the output contains at least one line.
        /// </summary>
        public static void ShouldHaveOutput(string output)
        {
            output.ShouldNotBeNullOrWhiteSpace("Should have output");
        }

        /// <summary>
        /// Asserts that the output contains the specified number of lines.
        /// </summary>
        public static void ShouldHaveLineCount(string output, int expectedCount)
        {
            var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            lines.Length.ShouldBe(expectedCount, $"Output should have {expectedCount} lines");
        }

        /// <summary>
        /// Asserts that the output contains a line matching the specified pattern.
        /// </summary>
        public static void ShouldContainLineMatching(string output, string pattern)
        {
            var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            lines.ShouldContain(line => line.Contains(pattern), $"Output should contain a line matching: {pattern}");
        }

        /// <summary>
        /// Asserts that the list of strings contains the specified text.
        /// </summary>
        public static void ListShouldContain(List<string> list, string expectedText)
        {
            list.ShouldContain(expectedText, $"List should contain: {expectedText}");
        }

        /// <summary>
        /// Asserts that the list is not empty.
        /// </summary>
        public static void ListShouldNotBeEmpty(List<string> list)
        {
            list.ShouldNotBeEmpty("List should not be empty");
        }

        /// <summary>
        /// Asserts that the list has the specified count.
        /// </summary>
        public static void ListShouldHaveCount(List<string> list, int expectedCount)
        {
            list.Count.ShouldBe(expectedCount, $"List should have {expectedCount} items");
        }
    }

    /// <summary>
    /// Helper methods for common test scenarios.
    /// </summary>
    protected static class TestHelpers
    {
        /// <summary>
        /// Waits for an async operation to complete with a timeout.
        /// </summary>
        public static async Task<T> WaitWithTimeoutAsync<T>(Func<Task<T>> asyncOperation, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                return await asyncOperation();
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                throw new TimeoutException($"Operation did not complete within {timeout.TotalSeconds} seconds");
            }
        }
    }

    /// <summary>
    /// Creates a StringWriter for capturing console output.
    /// </summary>
    protected static StringWriter CreateStringWriter()
    {
        return new StringWriter();
    }

    /// <summary>
    /// Creates a StringReader for providing console input.
    /// </summary>
    protected static StringReader CreateStringReader(string content)
    {
        return new StringReader(content);
    }

    /// <summary>
    /// Disposes resources used by the test base.
    /// </summary>
    public virtual void Dispose()
    {
        // Override in derived classes if needed
    }
}
