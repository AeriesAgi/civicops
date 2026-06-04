using CivicOps.Application.DTOs.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CivicOps.Application.Behaviors;

/// <summary>
/// Validates all commands/queries that have registered FluentValidation validators.
/// Runs before handler execution — invalid requests never reach the handler.
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (!failures.Any()) return await next();

        var errors = failures.Select(f => f.ErrorMessage).ToList();

        // If TResponse wraps Result, return failure without throwing
        if (typeof(TResponse).IsGenericType)
        {
            var responseType = typeof(TResponse);
            if (responseType == typeof(Result))
                return (TResponse)(object)Result.Failure(string.Join("; ", errors));

            var genericArg = responseType.GetGenericArguments().FirstOrDefault();
            if (genericArg is not null)
            {
                var failureMethod = typeof(Result)
                    .GetMethod(nameof(Result.Failure))!
                    .MakeGenericMethod(genericArg);
                return (TResponse)failureMethod.Invoke(null, new object[] { string.Join("; ", errors) })!;
            }
        }

        throw new ValidationException(failures);
    }
}

/// <summary>
/// Logs all requests and responses. Slow requests (>500ms) are logged as warnings.
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Handling {Request}", requestName);
            var response = await next();
            sw.Stop();

            if (sw.ElapsedMilliseconds > 500)
                _logger.LogWarning("Slow request {Request} took {Ms}ms", requestName, sw.ElapsedMilliseconds);
            else
                _logger.LogDebug("Handled {Request} in {Ms}ms", requestName, sw.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error handling {Request} after {Ms}ms", requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}

/// <summary>
/// Caches query results using ICacheService.
/// Queries must implement ICacheableQuery to opt in.
/// </summary>
public interface ICacheableQuery
{
    string CacheKey { get; }
    TimeSpan CacheDuration { get; }
}

public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICivicOpsCacheService _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(ICivicOpsCacheService cache, ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not ICacheableQuery cacheableQuery)
            return await next();

        var cached = await _cache.GetAsync<TResponse>(cacheableQuery.CacheKey, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for {Key}", cacheableQuery.CacheKey);
            return cached;
        }

        var response = await next();
        if (response is not null)
            await _cache.SetAsync(cacheableQuery.CacheKey, response, cacheableQuery.CacheDuration, ct);

        return response;
    }
}

// Placeholder for cache service used in behaviors (avoids circular dependency)
public interface ICivicOpsCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default) where T : class;
}
