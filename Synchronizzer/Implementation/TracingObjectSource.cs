﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.Extensions.Logging;

namespace Synchronizzer.Implementation
{
    internal sealed class TracingObjectSource : IObjectSource
    {
        private readonly IObjectSource _inner;
        private readonly string _source;
        private readonly ILogger _logger;

        public TracingObjectSource(IObjectSource inner, string source, ILogger<TracingObjectSource> logger)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async IAsyncEnumerable<IReadOnlyCollection<ObjectInfo>> GetOrdered([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            IAsyncEnumerator<IReadOnlyCollection<ObjectInfo>>? enumerator = null;
            try
            {
                while (true)
                {
                    var timer = Stopwatch.StartNew();
                    IReadOnlyCollection<ObjectInfo> result;
                    using (_logger.BeginScope("{Source}", _source))
                    {
                        _logger.LogDebug(Events.Get, "Get next.");
                        try
                        {
                            enumerator ??= _inner.GetOrdered(cancellationToken).GetAsyncEnumerator(cancellationToken);
                            if (!await enumerator.MoveNextAsync())
                            {
                                break;
                            }

                            result = enumerator.Current;
                        }
                        catch (OperationCanceledException exception)
                        {
                            _logger.LogInformation(
                                Events.GetCanceled,
                                exception,
                                "Get was canceled, elapsed {Elapsed} (direct: {IsDirect}).",
                                timer.Elapsed.TotalMilliseconds,
                                cancellationToken.IsCancellationRequested);
                            throw;
                        }
                        catch (Exception exception)
                        {
                            _logger.LogWarning(exception, "Failed to get, elapsed {Elapsed}.", timer.Elapsed.TotalMilliseconds);
                            throw;
                        }

                        _logger.LogInformation(Events.Got, "Got count {Count}, elapsed {Elapsed}.", result.Count, timer.Elapsed.TotalMilliseconds);
                    }

                    yield return result;
                }
            }
            finally
            {
                if (enumerator is not null)
                {
                    await enumerator.DisposeAsync();
                }
            }
        }

        private static class Events
        {
            public static readonly EventId Get = new(1, nameof(Get));
            public static readonly EventId Got = new(2, nameof(Got));
            public static readonly EventId GetCanceled = new(3, nameof(GetCanceled));
        }
    }
}
