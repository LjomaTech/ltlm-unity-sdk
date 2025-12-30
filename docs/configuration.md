# Configuration Guide

How to configure the LTLM SDK for your Unity project.

---

## Configuration Methods

### 1. Editor Tools (Recommended)

The easiest way to configure the SDK:

1. Open **LTLM → Project Settings**
2. Log in with your dashboard credentials
3. Select your project
4. Click **Inject Selected Project Keys**

The editor will create and populate `LTLMSettings.asset` automatically.

### 2. ScriptableObject (Manual)

Create the settings asset manually:

1. Right-click in Project window
2. **Create → LTLM → Settings**
3. Name it `LTLMSettings`
4. Move to `Assets/LTLM/Resources/`
5. Fill in the fields in the Inspector

### 3. Runtime Configuration

Configure programmatically at runtime:

```csharp
var manager = LTLMManager.Instance;
manager.projectId = "your-project-id";
manager.publicKey = "-----BEGIN PUBLIC KEY-----\n...";
manager.secretKey = "your-64-char-hex-secret";
```

---

## Configuration Options

### LTLMSettings (ScriptableObject)

| Property | Type | Description |
|----------|------|-------------|
| `projectId` | string | Your LTLM Project ID |
| `projectName` | string | Display name (informational) |
| `publicKey` | string | Ed25519 public key (PEM format) |
| `secretKey` | string | AES-256 secret key (64-char hex) |
| `capabilities` | List | Enabled feature flags |
| `analyticsEvents` | List | Configured analytics events |

### LTLMManager (Inspector)

| Property | Default | Description |
|----------|---------|-------------|
| `heartbeatIntervalSeconds` | 300 | Fallback interval (⚠️ server value takes priority!) |
| `autoValidateOnStart` | true | Auto-validate stored license on Start() |
| `softwareVersion` | Application.version | Your app version (used for version-gating) |

> [!IMPORTANT]
> The `heartbeatIntervalSeconds` value from the server is included in every license response. The SDK automatically uses that value for scheduling heartbeats. The Inspector value is only a fallback if the server doesn't respond.

---

## Key Types Explained

### Project ID

A unique identifier for your project. Found in the dashboard under Project Settings.

```
Example: 42 or "proj_abc123"
```

### Public Key (Ed25519)

Used to verify server signatures. PEM format.

```
-----BEGIN PUBLIC KEY-----
MCowBQYDK2VwAyEA...
-----END PUBLIC KEY-----
```

### Secret Key (AES-256)

Used for request encryption. 64-character hexadecimal string.

```
a1b2c3d4e5f6...64 characters total
```

> ⚠️ **Security Warning**: Never commit the secret key to version control! Use environment variables or secure injection at build time.

---

## Obtaining Your Keys

### From Dashboard

1. Log in to [dashboard.ltlm.io](https://dashboard.ltlm.io)
2. Navigate to **Projects → Your Project → Settings**
3. Copy the displayed keys

### From Editor Tools

1. Open **LTLM → Project Settings**
2. Enter your dashboard credentials
3. Click **Login & Fetch Projects**
4. Select your project and click **Inject Keys**

---

## Environment-Specific Configuration

### Development

```csharp
// Enable verbose logging
#if UNITY_EDITOR
LTLMManager.Instance.softwareVersion = "dev-build";
#endif
```

### Production Build

For production, inject keys at build time:

```csharp
// BuildProcessor.cs
public class BuildProcessor : IPreprocessBuildWithReport
{
    public void OnPreprocessBuild(BuildReport report)
    {
        var settings = Resources.Load<LTLMSettings>("LTLMSettings");
        settings.secretKey = Environment.GetEnvironmentVariable("LTLM_SECRET_KEY");
        EditorUtility.SetDirty(settings);
    }
}
```

---

## Validation

The editor tools provide validation warnings:

| Warning | Cause | Solution |
|---------|-------|----------|
| "Secret key should be 64 characters" | Wrong key length | Copy the full key from dashboard |
| "Public key should be in PEM format" | Missing header | Ensure key includes BEGIN/END markers |
| "Settings not found" | Missing asset | Create LTLMSettings in Resources folder |

---

## Backend URL

The backend URL is hardcoded in `LTLMConstants.cs`:

```csharp
public const string BackendUrl = "https://api.ltlm.io/api";
```

For development/staging environments, modify before building:

```csharp
#if DEVELOPMENT_BUILD
public const string BackendUrl = "https://staging-api.ltlm.io/api";
#else
public const string BackendUrl = "https://api.ltlm.io/api";
#endif
```

---

## Next Steps

- [Quick Start](quickstart.md) - Start using the SDK
- [License Lifecycle](license-lifecycle.md) - Understand activation flows
