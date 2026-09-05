using System;

namespace ZB2SecurityLab.Core.Experiments;

public sealed class ExperimentCoordinator
{
    public ILabExperiment? ActiveExperiment { get; private set; }

    public void Start(ILabExperiment experiment)
    {
        if (experiment is null)
        {
            throw new ArgumentNullException(nameof(experiment));
        }

        if (ActiveExperiment is not null)
        {
            throw new InvalidOperationException($"Experiment '{ActiveExperiment.Id}' is already active.");
        }

        ActiveExperiment = experiment;
        try
        {
            experiment.CaptureBaseline();
            experiment.Apply();
        }
        catch
        {
            RestoreActive();
            throw;
        }
    }

    public void ObserveActive()
    {
        ActiveExperiment?.Observe();
    }

    public void RestoreActive()
    {
        var experiment = ActiveExperiment;
        ActiveExperiment = null;
        experiment?.Restore();
    }
}

