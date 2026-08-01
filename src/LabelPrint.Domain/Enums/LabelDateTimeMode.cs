namespace LabelPrint.Domain.Enums;

/// <summary>How label Date/Time fields are resolved.</summary>
public enum LabelDateTimeMode
{
    /// <summary>Use wall clock at print time.</summary>
    Realtime = 0,

    /// <summary>Use <see cref="Entities.AppSettings.ManualLabelDateTime"/>.</summary>
    Manual = 1
}
