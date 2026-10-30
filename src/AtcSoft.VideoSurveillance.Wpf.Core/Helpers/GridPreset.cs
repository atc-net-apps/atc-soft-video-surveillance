namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

/// <summary>
/// The size of a camera grid. The value of a fixed size is its number of
/// cameras along a side.
/// </summary>
public enum GridPreset
{
    /// <summary>The grid grows with the number of cameras and shows them all.</summary>
    Auto = 0,

    /// <summary>One camera at a time.</summary>
    OneByOne = 1,

    /// <summary>Four cameras, two by two.</summary>
    TwoByTwo = 2,

    /// <summary>Nine cameras, three by three.</summary>
    ThreeByThree = 3,

    /// <summary>Sixteen cameras, four by four.</summary>
    FourByFour = 4,
}