# LTLMManager API Reference

This documentation has been consolidated into the main API reference.

---

**→ See [API Reference](../api-reference.md) for complete documentation.**

---

## Quick Reference

| Category | Methods |
|----------|---------|
| License | `ActivateLicense`, `ValidateLicense`, `TryLoadStoredLicense` |
| Tokens | `ConsumeTokens`, `GetTokenBalance`, `DoesHaveTokens` |
| Entitlements | `HasCapability`, `IsEntitled`, `GetMetadata` |
| Seats | `DeactivateSeat`, `GetActiveSeats`, `ReleaseSeat`, `ReactivateSeat` |
| OTP Auth | `RequestLoginOTP`, `VerifyLoginOTP`, `GetCustomerLicenses` |
| Commerce | `GetBuyablePolicies`, `CreateCheckoutSession`, `CreateTopUpSession` |
| Topups | `GetAvailableTopups`, `CanBuyTopups`, `GetTopupOption`, `PurchaseTopup` |
| Settings | `GetUserSettings`, `SaveUserSettings`, `GetLocalUserSettings` |
| Session | `ReleaseSeat`, `ReactivateSeat`, `DeleteLicenseData` |
| Events | `OnLicenseStatusChanged`, `OnTokensConsumed`, `OnKicked`, `OnSeatReleased` |
