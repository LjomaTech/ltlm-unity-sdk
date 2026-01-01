# Error Handling

How to handle errors gracefully in your application.

---

## Error Callback Pattern

All SDK methods that communicate with the server include an error callback:

```csharp
LTLMManager.Instance.ActivateLicense(
    licenseKey,
    (license, status) => { /* success */ },
    error => {
        // Handle error here
        Debug.LogError("Error: " + error);
    }
);
```

---

## Common Errors

### Network Errors

```csharp
void HandleError(string error)
{
    if (error.Contains("network") || error.Contains("timeout"))
    {
        ShowRetryDialog("Connection failed. Check your internet and try again.");
    }
}
```

---

### Invalid License Key

```csharp
void HandleError(string error)
{
    if (error.Contains("Invalid license") || error.Contains("not found"))
    {
        ShowError("License key not recognized. Please check and try again.");
        ClearInputField();
    }
}
```

---

### Already Activated

```csharp
void HandleError(string error)
{
    if (error.Contains("already activated") || error.Contains("activation limit"))
    {
        ShowError("This license is already in use on another device.");
        ShowContactSupport();
    }
}
```

---

### Insufficient Tokens

```csharp
void HandleTokenError(string error)
{
    if (error.Contains("insufficient") || error.Contains("tokens"))
    {
        ShowTopUpPrompt();
    }
}
```

---

## Error Handler Component

```csharp
using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;

public class ErrorHandler : MonoBehaviour
{
    public GameObject errorDialog;
    public Text errorTitle;
    public Text errorMessage;
    public Button retryButton;
    public Button contactButton;

    private System.Action _retryAction;

    public void ShowError(string title, string message, System.Action onRetry = null)
    {
        errorTitle.text = title;
        errorMessage.text = message;
        
        _retryAction = onRetry;
        retryButton.gameObject.SetActive(onRetry != null);
        
        errorDialog.SetActive(true);
    }

    public void OnRetryClicked()
    {
        errorDialog.SetActive(false);
        _retryAction?.Invoke();
    }

    public void OnContactClicked()
    {
        Application.OpenURL("mailto:support@yourapp.com");
    }

    public void OnCloseClicked()
    {
        errorDialog.SetActive(false);
    }

    // Call this from your error callbacks
    public void HandleLicenseError(string error)
    {
        if (error.Contains("network") || error.Contains("timeout") || error.Contains("Unable to connect"))
        {
            ShowError(
                "Connection Error",
                "Could not reach the server. Please check your internet connection.",
                () => RetryLastOperation()
            );
        }
        else if (error.Contains("Invalid") || error.Contains("not found"))
        {
            ShowError(
                "Invalid License",
                "The license key you entered was not recognized. Please check for typos."
            );
        }
        else if (error.Contains("limit") || error.Contains("activated"))
        {
            ShowError(
                "Activation Limit Reached",
                "This license is already activated on the maximum number of devices."
            );
            contactButton.gameObject.SetActive(true);
        }
        else if (error.Contains("expired"))
        {
            ShowError(
                "License Expired",
                "Your license has expired. Please renew to continue."
            );
        }
        else if (error.Contains("version"))
        {
            ShowError(
                "Version Not Allowed",
                "Your app version is not compatible with this license. Please update."
            );
        }
        else
        {
            ShowError(
                "Error",
                error
            );
        }
    }

    void RetryLastOperation()
    {
        // Implement based on your app's flow
    }
}
```

---

## Retry with Exponential Backoff

For network errors, retry with increasing delays:

```csharp
using System.Collections;
using UnityEngine;
using LTLM.SDK.Unity;

public class RobustActivation : MonoBehaviour
{
    private int _retryCount = 0;
    private const int MaxRetries = 3;
    private float[] _retryDelays = { 1f, 3f, 7f };

    public void ActivateWithRetry(string licenseKey)
    {
        _retryCount = 0;
        StartCoroutine(TryActivate(licenseKey));
    }

    IEnumerator TryActivate(string licenseKey)
    {
        bool success = false;
        string lastError = "";

        LTLMManager.Instance.ActivateLicense(licenseKey,
            (license, status) => {
                success = true;
                OnActivationSuccess(license, status);
            },
            error => {
                lastError = error;
            }
        );

        // Wait for callback
        while (!success && string.IsNullOrEmpty(lastError))
        {
            yield return null;
        }

        if (!success)
        {
            // Check if retryable error
            if (IsNetworkError(lastError) && _retryCount < MaxRetries)
            {
                Debug.Log("Retrying in " + _retryDelays[_retryCount] + " seconds...");
                yield return new WaitForSeconds(_retryDelays[_retryCount]);
                _retryCount++;
                StartCoroutine(TryActivate(licenseKey));
            }
            else
            {
                OnActivationFailed(lastError);
            }
        }
    }

    bool IsNetworkError(string error)
    {
        return error.Contains("network") || 
               error.Contains("timeout") || 
               error.Contains("Unable to connect");
    }

    void OnActivationSuccess(LicenseData license, LicenseStatus status)
    {
        Debug.Log("Activated successfully!");
    }

    void OnActivationFailed(string error)
    {
        Debug.LogError("Activation failed: " + error);
    }
}
```

---

## User-Friendly Error Messages

Map technical errors to user-friendly messages:

```csharp
public static class ErrorMessages
{
    public static string GetUserMessage(string technicalError)
    {
        if (technicalError.Contains("network") || technicalError.Contains("timeout"))
            return "Please check your internet connection and try again.";
        
        if (technicalError.Contains("Invalid license"))
            return "The license key you entered is not valid.";
        
        if (technicalError.Contains("activation limit"))
            return "This license has reached its device limit. Contact support.";
        
        if (technicalError.Contains("expired"))
            return "Your license has expired.";
        
        if (technicalError.Contains("suspended"))
            return "Your license has been suspended. Contact support.";
        
        if (technicalError.Contains("version"))
            return "Please update your app to the latest version.";
        
        if (technicalError.Contains("insufficient tokens"))
            return "Not enough credits. Add more to continue.";
        
        if (technicalError.Contains("TAMPERED"))
            return "License data was corrupted. Please reactivate.";
        
        // Default
        return "An error occurred. Please try again.";
    }
}

// Usage:
void OnError(string error)
{
    string userMessage = ErrorMessages.GetUserMessage(error);
    statusLabel.text = userMessage;
}
```

---

## Validation Events for Loading State

Use events to manage loading indicators:

```csharp
using UnityEngine;
using LTLM.SDK.Unity;

public class LoadingHandler : MonoBehaviour
{
    public GameObject loadingOverlay;
    public Text loadingText;

    void OnEnable()
    {
        LTLMManager.OnValidationStarted += ShowLoading;
        LTLMManager.OnValidationCompleted += HideLoading;
    }

    void OnDisable()
    {
        LTLMManager.OnValidationStarted -= ShowLoading;
        LTLMManager.OnValidationCompleted -= HideLoading;
    }

    void ShowLoading()
    {
        loadingText.text = "Checking license...";
        loadingOverlay.SetActive(true);
    }

    void HideLoading(bool success, LicenseStatus status)
    {
        loadingOverlay.SetActive(false);
        
        if (!success)
        {
            // Handle failure case
            ShowActivationScreen();
        }
    }

    void ShowActivationScreen()
    {
        // Navigate to activation
    }
}
```

---

## Next Steps

- [Status Codes](status-codes.md) - Understand all status values
- [Troubleshooting](troubleshooting.md) - Common issues and fixes
