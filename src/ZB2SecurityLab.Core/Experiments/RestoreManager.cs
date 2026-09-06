using System;
using System.Collections.Generic;

namespace ZB2SecurityLab.Core.Experiments;

public sealed class RestoreManager
{
    private readonly List<ILabExperiment> _pending = new();

    public int PendingCount => _pending.Count;

    public void Register(ILabExperiment experiment)
    {
        if (experiment is null)
        {
            throw new ArgumentNullException(nameof(experiment));
        }

        if (_pending.Contains(experiment))
        {
            throw new InvalidOperationException($"Experiment '{experiment.Id}' is already registered for restore.");
        }

        _pending.Add(experiment);
    }

    public RestoreReport RestoreAll(RestoreReason reason)
    {
        var entries = new List<RestoreEntryResult>(_pending.Count);
        for (var index = _pending.Count - 1; index >= 0; index--)
        {
            var experiment = _pending[index];
            try
            {
                experiment.Restore();
                entries.Add(new RestoreEntryResult(experiment.Id, experiment.RestoreConfirmed, experiment.RestoreConfirmed ? null : "RESTORE_NOT_CONFIRMED"));
            }
            catch (Exception exception)
            {
                entries.Add(new RestoreEntryResult(experiment.Id, false, exception.Message));
            }
        }

        _pending.Clear();
        return new RestoreReport(reason, entries);
    }
}
