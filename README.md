# LTLM SDK for Unity

[![Unity 2020.3+](https://img.shields.io/badge/Unity-2020.3%2B-black.svg?logo=unity)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.md)

**License & Token Management SDK** for Unity applications.

## Quick Start

To install the SDK, open the **Package Manager** in Unity (`Window` > `Package Manager`), click the **+** icon, select **Add package from git URL...**, and enter:

`https://github.com/LjomaTech/ltlm-unity-sdk.git`


```csharp
using LTLM.SDK.Unity;

LTLMManager.Instance.ActivateLicense("YOUR-LICENSE-KEY",
    (license, status) => Debug.Log("Activated: " + status),
    error => Debug.LogError(error)
);
```

## Documentation

See the full documentation in the [Documentation~](Documentation~/) folder.

## Support

- 📧 Email: support@ljomatech.com
- 🌐 Dashboard: [ltlm.ljomatech.com](https://ltlm.ljomatech.com)

## License

[MIT License](LICENSE.md) © 2025 Ljomatech
