# LTLM SDK for Unity

[![Unity 2020.3+](https://img.shields.io/badge/Unity-2020.3%2B-black.svg?logo=unity)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.md)

**License & Token Management SDK** for Unity applications.

## Quick Start

```csharp
using LTLM.SDK.Unity;

LTLMManager.Instance.ActivateLicense("YOUR-LICENSE-KEY",
    (license, status) => Debug.Log("Activated: " + status),
    error => Debug.LogError(error)
);
```

## Documentation

See the full documentation in the [Documentation~](Documentation~/) folder or visit [docs.ltlm.io](https://docs.ltlm.io).

## Support

- 📧 Email: support@ltlm.io
- 🌐 Dashboard: [dashboard.ltlm.io](https://dashboard.ltlm.io)

## License

[MIT License](LICENSE.md) © 2025 Ljomatech
