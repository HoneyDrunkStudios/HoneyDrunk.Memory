using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using System.Diagnostics;

namespace HoneyDrunk.Memory.Canary;

/// <summary>An activity factory that samples nothing.</summary>
internal sealed class NullTelemetryActivityFactory : ITelemetryActivityFactory
{
    /// <inheritdoc />
    public Activity? Start(string name, IReadOnlyDictionary<string, object?>? additionalTags = null) => null;

    /// <inheritdoc />
    public Activity? StartExplicit(string name, IGridContext gridContext, IOperationContext? operationContext = null, IReadOnlyDictionary<string, object?>? additionalTags = null) => null;
}
