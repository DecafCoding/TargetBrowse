using Microsoft.EntityFrameworkCore;

namespace TargetBrowse.Data;

/// <summary>
/// Base repository class providing common database operations and error handling patterns.
/// Eliminates duplication across repository implementations with shared context and logging.
/// </summary>
/// <typeparam name="TEntity">Entity type this repository manages</typeparam>
public abstract class BaseRepository<TEntity> where TEntity : class
{
    protected readonly ApplicationDbContext _context;
    protected readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the base repository.
    /// </summary>
    /// <param name="context">Database context for data access</param>
    /// <param name="logger">Logger instance for the derived repository type</param>
    protected BaseRepository(ApplicationDbContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes an operation with standard error handling and logging.
    /// Catches exceptions, logs them, and rethrows to allow higher-level handling.
    /// </summary>
    /// <typeparam name="TResult">Return type of the operation</typeparam>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="operationName">Name of the operation for logging purposes</param>
    /// <param name="errorContext">Additional context for error logging</param>
    /// <returns>Result of the operation</returns>
    protected async Task<TResult> ExecuteWithErrorHandlingAsync<TResult>(
        Func<Task<TResult>> operation,
        string operationName,
        object? errorContext = null)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            if (errorContext != null)
            {
                _logger.LogError(ex, "Error in {OperationName}: {Context}", operationName, errorContext);
            }
            else
            {
                _logger.LogError(ex, "Error in {OperationName}", operationName);
            }
            throw;
        }
    }

    /// <summary>
    /// Executes an operation with standard error handling and logging (void return).
    /// </summary>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="operationName">Name of the operation for logging purposes</param>
    /// <param name="errorContext">Additional context for error logging</param>
    protected async Task ExecuteWithErrorHandlingAsync(
        Func<Task> operation,
        string operationName,
        object? errorContext = null)
    {
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            if (errorContext != null)
            {
                _logger.LogError(ex, "Error in {OperationName}: {Context}", operationName, errorContext);
            }
            else
            {
                _logger.LogError(ex, "Error in {OperationName}", operationName);
            }
            throw;
        }
    }

    /// <summary>
    /// Gets the DbSet for the entity type managed by this repository.
    /// </summary>
    protected DbSet<TEntity> DbSet => _context.Set<TEntity>();
}
