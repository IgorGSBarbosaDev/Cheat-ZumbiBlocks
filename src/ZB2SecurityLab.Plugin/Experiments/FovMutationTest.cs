using System;
using System.Globalization;
using ZB2SecurityLab.Core.Experiments;

namespace ZB2SecurityLab.Plugin.Experiments;

internal sealed class FovMutationTest : ILabExperiment
{
    internal const float TargetFov = 110f;
    private const float Tolerance = 0.01f;

    private float _originalFov;
    private bool _baselineCaptured;

    internal FovMutationTest(string targetToken)
    {
        TargetToken = targetToken ?? throw new ArgumentNullException(nameof(targetToken));
    }

    public string Id => "FOV";

    public string TargetToken { get; }

    public string? OriginalValue { get; private set; }

    public string? RequestedValue { get; private set; } = FormatSource(TargetFov);

    public string? LocalObservedValue { get; private set; }

    public bool RequestedValueObserved { get; private set; }

    public bool InterferenceDetected { get; private set; }

    public bool RestoreConfirmed { get; private set; }

    public void CaptureBaseline()
    {
        _originalFov = FOVController.UserDefinedFOV;
        EnsureFinite(_originalFov, "FOV baseline");
        OriginalValue = FormatSource(_originalFov);
        _baselineCaptured = true;
    }

    public void Apply()
    {
        EnsureBaseline();
        FOVController.UserDefinedFOV = TargetFov;
    }

    public void Observe()
    {
        EnsureBaseline();
        var source = FOVController.UserDefinedFOV;
        EnsureFinite(source, "FOV source");

        float? current = null;
        float? rendered = null;
        float? zoom = null;
        var mainCamera = MainCamera.instance;
        var controller = mainCamera == null ? null : mainCamera.fovController;
        if (controller != null)
        {
            current = controller.CurrentFOV;
            zoom = controller.curZoom;
            if (controller.cam != null)
            {
                rendered = controller.cam.fieldOfView;
            }
        }

        LocalObservedValue = string.Format(
            CultureInfo.InvariantCulture,
            "source={0};current={1};rendered={2};zoom={3}",
            Number(source),
            NullableNumber(current),
            NullableNumber(rendered),
            NullableNumber(zoom));

        if (Approximately(source, TargetFov))
        {
            RequestedValueObserved = true;
        }
        else if (RequestedValueObserved)
        {
            InterferenceDetected = true;
        }
    }

    public void Restore()
    {
        EnsureBaseline();
        FOVController.UserDefinedFOV = _originalFov;
        var restored = FOVController.UserDefinedFOV;
        RestoreConfirmed = Approximately(restored, _originalFov);
        LocalObservedValue = FormatSource(restored);
    }

    private void EnsureBaseline()
    {
        if (!_baselineCaptured)
        {
            throw new InvalidOperationException("FOV baseline has not been captured.");
        }
    }

    private static void EnsureFinite(float value, string label)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new InvalidOperationException($"{label} is not finite.");
        }
    }

    private static bool Approximately(float left, float right) => Math.Abs(left - right) <= Tolerance;

    private static string FormatSource(float value) => $"source={Number(value)}";

    private static string NullableNumber(float? value) => value.HasValue ? Number(value.Value) : "UNKNOWN";

    private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
