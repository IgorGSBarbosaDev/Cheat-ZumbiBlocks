namespace ZB2SecurityLab.Core.Experiments;

public interface ILabExperiment
{
    string Id { get; }

    void CaptureBaseline();

    void Apply();

    void Observe();

    void Restore();
}

