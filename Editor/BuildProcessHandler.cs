using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

namespace LTLM.SDK.Editor
{
    public class BuildProcessHandler : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[LTLM] Verifying SDK configuration before build...");
            
            var settings = Resources.Load<LTLM.SDK.Core.LTLMSettings>("LTLMSettings");
            if (settings == null || string.IsNullOrEmpty(settings.projectId) || string.IsNullOrEmpty(settings.publicKey))
            {
                throw new BuildFailedException("[LTLM] SDK not configured. Please go to LTLM -> Project Settings and sync your keys.");
            }

            if (string.IsNullOrEmpty(settings.secretKey))
            {
                Debug.LogWarning("[LTLM] No Secret Key found. PRO features will not function in the build.");
            }
            else
            {
                Debug.Log("[LTLM] SDK verified and ready for build.");
            }
        }
    }
}
