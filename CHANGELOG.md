# Changelog

All notable changes to the LTLM Unity SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-01-01

### Added
- **User Settings Sync** - Cloud sync of user preferences across devices
  - `GetUserSettings()` - Fetch settings from server
  - `SaveUserSettings()` - Save settings to server
  - `GetLocalUserSettings()` - Get cached settings (offline)
- **Connectivity-aware token consumption**
  - Online mode uses single consumption API for immediate sync
  - Offline mode batches consumptions for sync when back online
- **OnTokensConsumed event** - Now properly invoked after consumption

### Changed
- Token consumption now uses dedicated single-consume API when online
- Batch consumption API only used for offline sync
- Improved app pause/resume handling for mobile platforms

### Fixed
- OnTokensConsumed event was not being invoked

## [1.1.0] - 2025-12-15

### Added
- **Concurrent seat management** with `VALID_NO_SEAT` and `KICKED` statuses
- `GetActiveSeats()` - Get list of devices holding seats
- `ReleaseSeat()` - Remote seat release with optional claim
- `ReactivateSeat()` - Reclaim seat after being kicked
- `OnKicked` event for seat displacement notifications
- `OnSeatStatusChanged` event for seat state changes

### Changed
- Heartbeat now includes seat status in response
- Improved offline grace period handling

## [1.0.0] - 2025-11-01

### Added
- Initial release
- License activation (online & offline)
- License validation with auto-recovery
- Token consumption with optimistic updates
- Feature entitlements with capability checks
- In-app checkout for upgrades
- Cross-platform support (Windows, macOS, Linux, Android, iOS, WebGL)
- Secure local storage with encryption
- Heartbeat with configurable intervals
- Secure clock for tamper detection
