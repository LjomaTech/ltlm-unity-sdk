using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

namespace LTLM.SDK.Editor
{
    /// <summary>
    /// Editor window for configuring LTLM SDK settings.
    /// Provides developer login, project key injection, and configuration management.
    /// 
    /// <para>Access via: <c>LTLM -> Project Settings</c> menu.</para>
    /// </summary>
    public class ProjectSettingsEditor : EditorWindow
    {
        private string _devEmail;
        private string _devPassword;
        private string _status = "Ready";
        private List<LTLM.SDK.Core.Models.DevOrgData> _availableOrgs;
        private List<LTLM.SDK.Core.Models.DevProjectData> _availableProjects;
        private string[] _projectNames;
        private int _selectedProjectIndex = 0;
        private bool _isAuthenticating = false;
        private bool _isTestingConnection = false;

        // MFA State
        private bool _mfaRequired = false;
        private string _mfaToken;
        private string _mfaOtp;
        private int _mfaUserId;

        // UI State
        private bool _showAdvanced = false;
        private Vector2 _scrollPos;

        [MenuItem("LTLM/Project Settings", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<ProjectSettingsEditor>("LTLM Settings");
            window.minSize = new Vector2(400, 500);
        }

        [MenuItem("LTLM/Documentation", priority = 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://docs.ltlm.io");
        }

        [MenuItem("LTLM/Dashboard", priority = 101)]
        public static void OpenDashboard()
        {
            Application.OpenURL("https://dashboard.ltlm.io");
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            DrawHeader();
            EditorGUILayout.Space(10);
            
            var settings = LoadOrCreateSettings();
            
            DrawProjectConfiguration(settings);
            EditorGUILayout.Space(10);
            
            DrawSecurityKeys(settings);
            EditorGUILayout.Space(10);
            
            DrawDeveloperLogin(settings);
            EditorGUILayout.Space(10);
            
            if (_availableProjects != null && _availableProjects.Count > 0)
            {
                DrawProjectSelector(settings);
                EditorGUILayout.Space(10);
            }
            
            DrawAdvancedSection(settings);
            EditorGUILayout.Space(10);
            
            DrawStatusBar();
            
            EditorGUILayout.EndScrollView();
        }

        #region UI Sections

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            
            GUILayout.Label("LTLM SDK Configuration", EditorStyles.boldLabel);
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Docs", GUILayout.Width(50)))
            {
                Application.OpenURL("https://docs.ltlm.io");
            }
            
            if (GUILayout.Button("Dashboard", GUILayout.Width(70)))
            {
                Application.OpenURL("https://dashboard.ltlm.io");
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                $"SDK Version: {LTLM.SDK.Core.LTLMConstants.Version}\n" +
                $"Backend: {LTLM.SDK.Core.LTLMConstants.BackendUrl}",
                MessageType.Info
            );
        }

        private void DrawProjectConfiguration(LTLM.SDK.Core.LTLMSettings settings)
        {
            EditorGUILayout.LabelField("Project Configuration", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            settings.projectId = EditorGUILayout.TextField(
                new GUIContent("Project ID", "Your unique project identifier from the LTLM dashboard."),
                settings.projectId
            );
            
            settings.projectName = EditorGUILayout.TextField(
                new GUIContent("Project Name", "Display name for this project (informational only)."),
                settings.projectName
            );
            
            EditorGUILayout.EndVertical();
            
            if (EditorGUI.EndChangeCheck())
            {
                SaveSettings(settings);
            }
        }

        private void DrawSecurityKeys(LTLM.SDK.Core.LTLMSettings settings)
        {
            EditorGUILayout.LabelField("Security Keys", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Public Key (Ed25519)", EditorStyles.miniLabel);
            settings.publicKey = EditorGUILayout.TextArea(settings.publicKey, GUILayout.Height(60));
            
            EditorGUILayout.Space(5);
            
            settings.secretKey = EditorGUILayout.PasswordField(
                new GUIContent("Secret Key (64-char hex)", "⚠️ Keep this secret! Injected at build time for PRO apps."),
                settings.secretKey
            );
            
            EditorGUILayout.EndVertical();
            
            if (EditorGUI.EndChangeCheck())
            {
                SaveSettings(settings);
            }
            
            // Validation warnings
            if (!string.IsNullOrEmpty(settings.secretKey) && settings.secretKey.Length != 64)
            {
                EditorGUILayout.HelpBox("Secret key should be 64 characters (32 bytes hex). Current length: " + settings.secretKey.Length, MessageType.Warning);
            }
            
            if (!string.IsNullOrEmpty(settings.publicKey) && !settings.publicKey.Contains("BEGIN PUBLIC KEY"))
            {
                EditorGUILayout.HelpBox("Public key should be in PEM format (BEGIN PUBLIC KEY).", MessageType.Warning);
            }
            
            // Action buttons
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Test Connection"))
            {
                TestBackendConnection(settings);
            }
            
            if (GUILayout.Button("Save Settings"))
            {
                SaveSettings(settings);
                _status = "Settings saved to disk.";
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDeveloperLogin(LTLM.SDK.Core.LTLMSettings settings)
        {
            EditorGUILayout.LabelField("Developer Login", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (_mfaRequired)
            {
                // MFA OTP Input
                EditorGUILayout.HelpBox("Enter the verification code from your authenticator app.", MessageType.Info);
                
                _mfaOtp = EditorGUILayout.TextField("Verification Code", _mfaOtp);
                
                EditorGUILayout.BeginHorizontal();
                
                EditorGUI.BeginDisabledGroup(_isAuthenticating || string.IsNullOrEmpty(_mfaOtp));
                if (GUILayout.Button(_isAuthenticating ? "Verifying..." : "Verify & Login", GUILayout.Height(28)))
                {
                    PerformMfaVerify(settings);
                }
                EditorGUI.EndDisabledGroup();
                
                if (GUILayout.Button("Cancel", GUILayout.Width(80), GUILayout.Height(28)))
                {
                    _mfaRequired = false;
                    _mfaToken = null;
                    _mfaOtp = "";
                    _status = "MFA cancelled. Please try again.";
                }
                
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // Normal Login
                EditorGUILayout.HelpBox("Log in with your LTLM dashboard credentials to automatically fetch and inject project keys.", MessageType.None);
                
                _devEmail = EditorGUILayout.TextField("Email / Username", _devEmail);
                _devPassword = EditorGUILayout.PasswordField("Password", _devPassword);

                EditorGUI.BeginDisabledGroup(_isAuthenticating || string.IsNullOrEmpty(_devEmail) || string.IsNullOrEmpty(_devPassword));
                
                if (GUILayout.Button(_isAuthenticating ? "Logging in..." : "Login & Fetch Projects", GUILayout.Height(28)))
                {
                    PerformDevLogin(settings);
                }
                
                EditorGUI.EndDisabledGroup();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawProjectSelector(LTLM.SDK.Core.LTLMSettings settings)
        {
            EditorGUILayout.LabelField("Project Selection", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            _selectedProjectIndex = EditorGUILayout.Popup("Available Projects", _selectedProjectIndex, _projectNames);
            
            var selectedProject = _availableProjects[_selectedProjectIndex];
            
            // Project info with organization
            EditorGUILayout.LabelField("Organization: " + (selectedProject.orgName ?? "Unknown"), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Status: " + selectedProject.status, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Public: " + (selectedProject.isPublic ? "Yes" : "No"), EditorStyles.miniLabel);
            
            EditorGUILayout.Space(5);
            
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("🔑 Inject Selected Project Keys", GUILayout.Height(32)))
            {
                InjectProjectKeys(settings, selectedProject);
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndVertical();
        }

        private void DrawAdvancedSection(LTLM.SDK.Core.LTLMSettings settings)
        {
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced / Metadata", true);
            
            if (_showAdvanced)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Capabilities
                if (settings.capabilities != null && settings.capabilities.Count > 0)
                {
                    EditorGUILayout.LabelField("Enabled Capabilities", EditorStyles.miniBoldLabel);
                    foreach (var cap in settings.capabilities)
                    {
                        EditorGUILayout.LabelField("  • " + cap, EditorStyles.miniLabel);
                    }
                    EditorGUILayout.Space(5);
                }
                
                // Analytics Events
                if (settings.analyticsEvents != null && settings.analyticsEvents.Count > 0)
                {
                    EditorGUILayout.LabelField("Configured Analytics", EditorStyles.miniBoldLabel);
                    foreach (var evt in settings.analyticsEvents)
                    {
                        EditorGUILayout.LabelField("  • " + evt, EditorStyles.miniLabel);
                    }
                    EditorGUILayout.Space(5);
                }
                
                // Platform status
                EditorGUILayout.LabelField("Platform Storage", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField($"  Current: {Application.platform}", EditorStyles.miniLabel);
                
                string storageType = "File + HMAC";
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                storageType = "Windows Registry";
#elif UNITY_WEBGL
                storageType = "PlayerPrefs (Limited)";
#endif
                EditorGUILayout.LabelField($"  Storage: {storageType}", EditorStyles.miniLabel);
                
                EditorGUILayout.Space(5);
                
                // Dangerous actions
                EditorGUILayout.LabelField("Danger Zone", EditorStyles.miniBoldLabel);
                GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
                if (GUILayout.Button("Clear All Settings"))
                {
                    if (EditorUtility.DisplayDialog("Clear Settings", "Are you sure you want to clear all LTLM settings?", "Yes, Clear", "Cancel"))
                    {
                        settings.projectId = "";
                        settings.projectName = "";
                        settings.publicKey = "";
                        settings.secretKey = "";
                        settings.capabilities.Clear();
                        settings.analyticsEvents.Clear();
                        SaveSettings(settings);
                        _status = "All settings cleared.";
                    }
                }
                GUI.backgroundColor = Color.white;
                
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.Space(10);
            
            MessageType messageType = MessageType.Info;
            if (_status.Contains("Error") || _status.Contains("failed"))
                messageType = MessageType.Error;
            else if (_status.Contains("Warning") || _status.Contains("should"))
                messageType = MessageType.Warning;
            else if (_status.Contains("success") || _status.Contains("saved") || _status.Contains("injected"))
                messageType = MessageType.Info;
            
            EditorGUILayout.HelpBox("Status: " + _status, messageType);
        }

        #endregion

        #region Actions

        private void InjectProjectKeys(LTLM.SDK.Core.LTLMSettings settings, LTLM.SDK.Core.Models.DevProjectData project)
        {
            settings.projectId = project.id.ToString();
            settings.projectName = project.name;
            settings.publicKey = project.publicKey;
            settings.secretKey = project.secretKey;
            
            // Update metadata
            settings.capabilities = project.capabilities ?? new List<string>();
            settings.analyticsEvents = new List<string>();
            if (project.analytics != null && project.analytics.events != null)
            {
                foreach (var e in project.analytics.events)
                {
                    settings.analyticsEvents.Add($"{e.type} ({e.description})");
                }
            }

            SaveSettings(settings);
            _status = $"✓ Keys injected for '{project.name}'";
        }

        private void TestBackendConnection(LTLM.SDK.Core.LTLMSettings settings)
        {
            if (_isTestingConnection) return;
            
            _isTestingConnection = true;
            _status = "Testing connection...";
            
            string url = LTLM.SDK.Core.LTLMConstants.BackendUrl.TrimEnd('/') + "/health";
            var webRequest = UnityWebRequest.Get(url);
            
            var operation = webRequest.SendWebRequest();
            
            EditorApplication.CallbackFunction checkProgress = null;
            checkProgress = () =>
            {
                if (operation.isDone)
                {
                    _isTestingConnection = false;
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        _status = "✓ Backend connection successful!";
                    }
                    else
                    {
                        _status = $"✗ Connection failed: {webRequest.error}";
                    }
                    
                    EditorApplication.update -= checkProgress;
                    Repaint();
                    webRequest.Dispose();
                }
            };

            EditorApplication.update += checkProgress;
        }

        private void SaveSettings(LTLM.SDK.Core.LTLMSettings settings)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private LTLM.SDK.Core.LTLMSettings LoadOrCreateSettings()
        {
            var settings = Resources.Load<LTLM.SDK.Core.LTLMSettings>("LTLMSettings");
            if (settings == null)
            {
                string resourcesPath = "Assets/LTLM/Resources";
                if (!Directory.Exists(resourcesPath))
                {
                    Directory.CreateDirectory(resourcesPath);
                }
                
                settings = CreateInstance<LTLM.SDK.Core.LTLMSettings>();
                AssetDatabase.CreateAsset(settings, resourcesPath + "/LTLMSettings.asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                _status = "Created new LTLMSettings asset.";
            }
            return settings;
        }

        private void PerformDevLogin(LTLM.SDK.Core.LTLMSettings settings)
        {
            _isAuthenticating = true;
            _status = "Logging in...";
            
            var requestData = new LTLM.SDK.Core.Models.DevLoginRequest
            {
                username = _devEmail,
                password = _devPassword
            };

            string json = JsonUtility.ToJson(requestData);
            string url = LTLM.SDK.Core.LTLMConstants.BackendUrl.TrimEnd('/') + "/v1/sdk/pro/auth/dev-login";

            var webRequest = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            var operation = webRequest.SendWebRequest();
            
            EditorApplication.CallbackFunction checkProgress = null;
            checkProgress = () =>
            {
                if (operation.isDone)
                {
                    _isAuthenticating = false;
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            var response = JsonUtility.FromJson<LTLM.SDK.Core.Models.DevLoginResponse>(webRequest.downloadHandler.text);
                            
                            // Check for MFA requirement
                            if (response.mfaRequired)
                            {
                                _mfaRequired = true;
                                _mfaToken = response.mfaToken;
                                _mfaUserId = response.userId;
                                _mfaOtp = "";
                                _status = "MFA required. Enter your authenticator code.";
                                EditorApplication.update -= checkProgress;
                                Repaint();
                                webRequest.Dispose();
                                return;
                            }
                            
                            _availableOrgs = response.organizations ?? new List<LTLM.SDK.Core.Models.DevOrgData>();
                            _availableProjects = response.projects ?? new List<LTLM.SDK.Core.Models.DevProjectData>();
                            _projectNames = new string[_availableProjects.Count];
                            
                            // Format project names with organization
                            for (int i = 0; i < _availableProjects.Count; i++)
                            {
                                var p = _availableProjects[i];
                                string orgName = !string.IsNullOrEmpty(p.orgName) ? p.orgName : "Unknown Org";
                                _projectNames[i] = $"[{orgName}] {p.name}";
                            }
                            
                            _selectedProjectIndex = 0;
                            
                            // Status message with org count
                            int orgCount = _availableOrgs.Count;
                            string orgText = orgCount > 1 ? $"{orgCount} organizations" : (orgCount == 1 ? "1 organization" : "");
                            string projectText = _availableProjects.Count == 1 ? "1 project" : $"{_availableProjects.Count} projects";
                            
                            _status = $"✓ Logged in! Found {projectText}" + (orgCount > 0 ? $" across {orgText}" : "") + ".";
                        }
                        catch (System.Exception ex)
                        {
                            _status = "Parse error: " + ex.Message;
                        }
                    }
                    else
                    {
                        string errorText = webRequest.downloadHandler.text;
                        try
                        {
                            var errorDetails = JsonUtility.FromJson<LTLM.SDK.Core.Models.LTLMError>(errorText);
                            if (errorDetails != null && !string.IsNullOrEmpty(errorDetails.message))
                            {
                                _status = "✗ " + errorDetails.message;
                            }
                            else
                            {
                                _status = "✗ " + errorText;
                            }
                        }
                        catch
                        {
                            _status = "✗ " + (string.IsNullOrEmpty(errorText) ? webRequest.error : errorText);
                        }
                    }
                    
                    EditorApplication.update -= checkProgress;
                    Repaint();
                    webRequest.Dispose();
                }
            };

            EditorApplication.update += checkProgress;
        }

        private void PerformMfaVerify(LTLM.SDK.Core.LTLMSettings settings)
        {
            _isAuthenticating = true;
            _status = "Verifying OTP...";

            // Backend expects mfaToken from initial login response
            string json = $"{{\"mfaToken\":\"{_mfaToken}\",\"otp\":\"{_mfaOtp}\"}}";
            string url = LTLM.SDK.Core.LTLMConstants.BackendUrl.TrimEnd('/') + "/v1/sdk/pro/auth/dev-login/verify-otp";

            var webRequest = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            var operation = webRequest.SendWebRequest();

            EditorApplication.CallbackFunction checkProgress = null;
            checkProgress = () =>
            {
                if (operation.isDone)
                {
                    _isAuthenticating = false;
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            var response = JsonUtility.FromJson<LTLM.SDK.Core.Models.DevLoginResponse>(webRequest.downloadHandler.text);

                            // Clear MFA state
                            _mfaRequired = false;
                            _mfaToken = null;
                            _mfaOtp = "";

                            // Process login response
                            _availableOrgs = response.organizations ?? new List<LTLM.SDK.Core.Models.DevOrgData>();
                            _availableProjects = response.projects ?? new List<LTLM.SDK.Core.Models.DevProjectData>();
                            _projectNames = new string[_availableProjects.Count];

                            for (int i = 0; i < _availableProjects.Count; i++)
                            {
                                var p = _availableProjects[i];
                                string orgName = !string.IsNullOrEmpty(p.orgName) ? p.orgName : "Unknown Org";
                                _projectNames[i] = $"[{orgName}] {p.name}";
                            }

                            _selectedProjectIndex = 0;

                            int orgCount = _availableOrgs.Count;
                            string orgText = orgCount > 1 ? $"{orgCount} organizations" : (orgCount == 1 ? "1 organization" : "");
                            string projectText = _availableProjects.Count == 1 ? "1 project" : $"{_availableProjects.Count} projects";

                            _status = $"✓ MFA verified! Found {projectText}" + (orgCount > 0 ? $" across {orgText}" : "") + ".";
                        }
                        catch (System.Exception ex)
                        {
                            _status = "Parse error: " + ex.Message;
                        }
                    }
                    else
                    {
                        string errorText = webRequest.downloadHandler.text;
                        long responseCode = webRequest.responseCode;
                        Debug.LogError($"[LTLM] MFA Verify failed: HTTP {responseCode}, Error: {webRequest.error}, Body: {errorText}");
                        
                        try
                        {
                            var errorDetails = JsonUtility.FromJson<LTLM.SDK.Core.Models.LTLMError>(errorText);
                            if (errorDetails != null && !string.IsNullOrEmpty(errorDetails.message))
                            {
                                _status = $"✗ {errorDetails.message}";
                            }
                            else if (responseCode == 401)
                            {
                                _status = "✗ MFA session expired. Please login again.";
                            }
                            else if (responseCode == 400)
                            {
                                _status = "✗ Invalid verification code.";
                            }
                            else if (responseCode == 404)
                            {
                                _status = "✗ Endpoint not found. Please check your backend URL.";
                            }
                            else
                            {
                                _status = $"✗ Error ({responseCode}): {webRequest.error}";
                            }
                        }
                        catch
                        {
                            _status = $"✗ Error ({responseCode}): " + (string.IsNullOrEmpty(errorText) ? webRequest.error : errorText);
                        }
                    }

                    EditorApplication.update -= checkProgress;
                    Repaint();
                    webRequest.Dispose();
                }
            };

            EditorApplication.update += checkProgress;
        }

        #endregion
    }
}
