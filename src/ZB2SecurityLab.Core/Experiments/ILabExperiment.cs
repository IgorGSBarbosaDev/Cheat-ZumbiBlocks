namespace ZB2SecurityLab.Core.Experiments;

public interface ILabExperiment
{
    string Id { get; }

    string TargetToken { get; }

    string? OriginalValue { get; }

    string? RequestedValue { get; }

    string? LocalObservedValue { get; }

    bool RequestedValueObserved { get; }

    bool InterferenceDetected { get; }

    bool RestoreConfirmed { get; }

    void CaptureBaseline();

    void Apply();

    void Observe();

    void Restore();
}
