using System;
using System.Globalization;
using System.Text;

namespace ZB2SecurityLab.Core.Diagnostics;

public enum TestOutcome
{
    LOCAL_ONLY,
    SERVER_REJECTED,
    SERVER_CORRECTED,
    SERVER_ACCEPTED,
    INCONCLUSIVE
}

public sealed class SecurityTestEvent
{
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

    public string SessionId { get; set; } = string.Empty;

    public string BuildId { get; set; } = string.Empty;

    public string BuildFingerprint { get; set; } = string.Empty;

    public string Test { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string? Event { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? Context { get; set; }

    public string? OriginalValue { get; set; }

    public string? RequestedValue { get; set; }

    public string? LocalObservedValue { get; set; }

    public string? RemoteObservedValue { get; set; }

    public string? ServerEvidence { get; set; }

    public bool? ServerCorrected { get; set; }

    public bool? ServerAccepted { get; set; }

    public bool Disconnected { get; set; }

    public string? Error { get; set; }
}

public static class SecurityTestEventJson
{
    public static string Serialize(SecurityTestEvent value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var builder = new StringBuilder(512);
        builder.Append('{');
        AppendString(builder, "timestampUtc", value.TimestampUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        AppendString(builder, "sessionId", value.SessionId);
        AppendString(builder, "buildId", value.BuildId);
        AppendString(builder, "buildFingerprint", value.BuildFingerprint);
        AppendString(builder, "test", value.Test);
        AppendString(builder, "phase", value.Phase);
        AppendNullableString(builder, "event", value.Event);
        AppendNullableString(builder, "oldValue", value.OldValue);
        AppendNullableString(builder, "newValue", value.NewValue);
        AppendNullableString(builder, "context", value.Context);
        AppendNullableString(builder, "originalValue", value.OriginalValue);
        AppendNullableString(builder, "requestedValue", value.RequestedValue);
        AppendNullableString(builder, "localObservedValue", value.LocalObservedValue);
        AppendNullableString(builder, "remoteObservedValue", value.RemoteObservedValue);
        AppendNullableString(builder, "serverEvidence", value.ServerEvidence);
        AppendNullableBoolean(builder, "serverCorrected", value.ServerCorrected);
        AppendNullableBoolean(builder, "serverAccepted", value.ServerAccepted);
        AppendBoolean(builder, "disconnected", value.Disconnected);
        AppendNullableString(builder, "error", value.Error, appendComma: false);
        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendString(StringBuilder builder, string name, string value, bool appendComma = true)
    {
        AppendPropertyName(builder, name);
        AppendEscapedString(builder, value);
        if (appendComma)
        {
            builder.Append(',');
        }
    }

    private static void AppendNullableString(StringBuilder builder, string name, string? value, bool appendComma = true)
    {
        AppendPropertyName(builder, name);
        if (value is null)
        {
            builder.Append("null");
        }
        else
        {
            AppendEscapedString(builder, value);
        }

        if (appendComma)
        {
            builder.Append(',');
        }
    }

    private static void AppendBoolean(StringBuilder builder, string name, bool value, bool appendComma = true)
    {
        AppendPropertyName(builder, name);
        builder.Append(value ? "true" : "false");
        if (appendComma)
        {
            builder.Append(',');
        }
    }

    private static void AppendNullableBoolean(StringBuilder builder, string name, bool? value, bool appendComma = true)
    {
        AppendPropertyName(builder, name);
        builder.Append(value.HasValue ? (value.Value ? "true" : "false") : "null");
        if (appendComma)
        {
            builder.Append(',');
        }
    }

    private static void AppendPropertyName(StringBuilder builder, string name)
    {
        AppendEscapedString(builder, name);
        builder.Append(':');
    }

    private static void AppendEscapedString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }

        builder.Append('"');
    }
}
