using HoneyDrunk.Kernel.Abstractions.Context;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using System.Diagnostics;

namespace HoneyDrunk.Memory.Tests;

/// <summary>Starts real activities and keeps them, so tests can read every tag that was set.</summary>
internal sealed class RecordingTelemetryActivityFactory : ITelemetryActivityFactory
{
    private readonly List<Activity> activities = [];

    /// <summary>Gets the activities started, in order.</summary>
    public IReadOnlyList<Activity> Activities
    {
        get
        {
            lock (this.activities)
            {
                return [.. this.activities];
            }
        }
    }

    /// <summary>Gets every tag set on any activity.</summary>
    public IReadOnlyList<KeyValuePair<string, object?>> AllTags => [.. this.Activities.SelectMany(activity => activity.TagObjects)];

    /// <inheritdoc />
    public Activity? Start(string name, IReadOnlyDictionary<string, object?>? additionalTags = null)
    {
        var activity = new Activity(name);
        foreach (var tag in additionalTags ?? new Dictionary<string, object?>())
        {
            activity.SetTag(tag.Key, tag.Value);
        }

        lock (this.activities)
        {
            this.activities.Add(activity);
        }

        return activity.Start();
    }

    /// <inheritdoc />
    public Activity? StartExplicit(string name, IGridContext gridContext, IOperationContext? operationContext = null, IReadOnlyDictionary<string, object?>? additionalTags = null) =>
        this.Start(name, additionalTags);
}
