using Microsoft.Extensions.Logging;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.IntegrationTests.V1;

/// <summary>
/// Console logger implementation that logs messages to the console.
/// </summary>
public class ConsoleLoggerInstance : ILogger
{
    private readonly string _categoryName;

    public ConsoleLoggerInstance(string categoryName)
    {
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        // Enable all log levels
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        Console.WriteLine($"[{DateTime.UtcNow}] {logLevel} [{_categoryName}] {message}");

        if (exception != null)
        {
            Console.WriteLine($"Exception: {exception.Message}");
            Console.WriteLine(exception.StackTrace);

            if (exception.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {exception.InnerException.Message}");
                Console.WriteLine(exception.InnerException.StackTrace);
            }
        }
    }
}
