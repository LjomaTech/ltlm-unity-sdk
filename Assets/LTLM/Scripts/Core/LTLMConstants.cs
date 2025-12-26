namespace LTLM.SDK.Core
{
    public static class LTLMConstants
    {
        /// <summary>
        /// The fixed LTLM backend URL. 
        /// This is hardcoded to ensure it points to the correct domain when the SDK is compiled into a DLL.
        /// </summary>
        public const string BackendUrl = "https://ltlm.ljomatech.com:8000/api";
        
        /// <summary>
        /// Current SDK Version
        /// </summary>
        public const string Version = "1.2.0-PRO";
    }
}
