// This file is deprecated. Use LTLM.SDK.Core.Hardware.DeviceID instead.
// Kept for backwards compatibility only.

namespace LTLM.SDK.Unity.Hardware
{
    /// <summary>
    /// [DEPRECATED] Use LTLM.SDK.Core.Hardware.DeviceID instead.
    /// This wrapper exists for backwards compatibility.
    /// </summary>
    public static class DeviceID
    {
        public static string GetHWID() => LTLM.SDK.Core.Hardware.DeviceID.GetHWID();
        public static string GetDeviceName() => LTLM.SDK.Core.Hardware.DeviceID.GetDeviceName();
        public static string GetOS() => LTLM.SDK.Core.Hardware.DeviceID.GetOS();
    }
}
