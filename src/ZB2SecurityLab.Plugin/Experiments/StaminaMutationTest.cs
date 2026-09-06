using System;
using System.Globalization;
using ZB2SecurityLab.Core.Experiments;

namespace ZB2SecurityLab.Plugin.Experiments;

internal sealed class StaminaMutationTest : ILabExperiment
{
    private const float Tolerance = 0.01f;

    private readonly PlayerMain _player;
    private float _originalFast;
    private float _originalSlow;
    private float _originalMax;
    private bool _baselineCaptured;

    internal StaminaMutationTest(PlayerMain player, string targetToken)
    {
        _player = player == null ? throw new ArgumentNullException(nameof(player)) : player;
        TargetToken = targetToken ?? throw new ArgumentNullException(nameof(targetToken));
    }

    public string Id => "STAMINA";

    public string TargetToken { get; }

    public string? OriginalValue { get; private set; }

    public string? RequestedValue { get; private set; }

    public string? LocalObservedValue { get; private set; }

    public bool RequestedValueObserved { get; private set; }

    public bool InterferenceDetected => false;

    public bool RestoreConfirmed { get; private set; }

    public void CaptureBaseline()
    {
        EnsurePlayerAvailable();
        _originalFast = _player.staminaFast;
        _originalSlow = _player.staminaSlow;
        _originalMax = _player.maxStamina;
        EnsureFinite(_originalFast, "staminaFast");
        EnsureFinite(_originalSlow, "staminaSlow");
        EnsureFinite(_originalMax, "maxStamina");
        if (_originalMax <= 0f)
        {
            throw new InvalidOperationException("maxStamina must be greater than zero.");
        }

        OriginalValue = Format(_originalFast, _originalSlow, _originalMax);
        RequestedValue = Format(_originalMax, _originalMax, _originalMax);
        _baselineCaptured = true;
    }

    public void Apply()
    {
        EnsureBaselineAndPlayer();
        _player.staminaFast = _originalMax;
        _player.staminaSlow = _originalMax;
    }

    public void Observe()
    {
        EnsureBaselineAndPlayer();
        if (!Approximately(_player.maxStamina, _originalMax))
        {
            throw new InvalidOperationException("maxStamina changed during the mutation window.");
        }

        var fast = _player.staminaFast;
        var slow = _player.staminaSlow;
        EnsureFinite(fast, "staminaFast");
        EnsureFinite(slow, "staminaSlow");
        LocalObservedValue = FormatObserved(fast, slow, _player.maxStamina, _player.HasStamina, _player.CanUseStamina());
        if (Approximately(fast, _originalMax) && Approximately(slow, _originalMax))
        {
            RequestedValueObserved = true;
        }
    }

    public void Restore()
    {
        EnsureBaselineAndPlayer();
        _player.staminaFast = _originalFast;
        _player.staminaSlow = _originalSlow;
        RestoreConfirmed = Approximately(_player.staminaFast, _originalFast) && Approximately(_player.staminaSlow, _originalSlow);
        LocalObservedValue = FormatObserved(
            _player.staminaFast,
            _player.staminaSlow,
            _player.maxStamina,
            _player.HasStamina,
            _player.CanUseStamina());
    }

    private void EnsureBaselineAndPlayer()
    {
        if (!_baselineCaptured)
        {
            throw new InvalidOperationException("Stamina baseline has not been captured.");
        }

        EnsurePlayerAvailable();
    }

    private void EnsurePlayerAvailable()
    {
        if (_player == null)
        {
            throw new InvalidOperationException("Captured PlayerMain is no longer available.");
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

    private static string Format(float fast, float slow, float max)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "fast={0};slow={1};max={2}",
            Number(fast),
            Number(slow),
            Number(max));
    }

    private static string FormatObserved(float fast, float slow, float max, bool hasStamina, bool canUseStamina)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "fast={0};slow={1};max={2};hasStamina={3};canUseStamina={4}",
            Number(fast),
            Number(slow),
            Number(max),
            hasStamina,
            canUseStamina);
    }

    private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
