using UnityEngine;
using UnityEngine.UI;
using LTLM.SDK.Unity;
using LTLM.SDK.Core.Models;
using System.Collections.Generic;

namespace LTLM.SDK.Demos
{
    /// <summary>
    /// Demonstrates concurrent seat management:
    /// - Handling VALID_NO_SEAT status
    /// - Viewing active seats
    /// - Releasing another device's seat
    /// - Being kicked notification
    /// </summary>
    public class SeatManagementDemo : MonoBehaviour
    {
        [Header("Panels")]
        public GameObject loadingPanel;
        public GameObject activePanel;         // Shown when we have a seat
        public GameObject waitingPanel;        // Shown when waiting for seat (VALID_NO_SEAT)
        public GameObject kickedNoticePanel;

        [Header("Active Panel")]
        public Text activeStatusText;
        public Text seatInfoText;
        public Button signOutButton;

        [Header("Waiting Panel")]
        public Text waitingStatusText;
        public Transform seatListContainer;
        public GameObject seatItemPrefab;
        public Button refreshButton;
        public Text cooldownText;

        [Header("Kicked Notice")]
        public Text kickedMessageText;
        public Button kickedOkButton;

        private float _refreshCooldown = 0f;
        private List<GameObject> _seatItems = new List<GameObject>();

        void OnEnable()
        {
            // Subscribe to SDK events
            LTLMManager.OnValidationStarted += OnValidationStarted;
            LTLMManager.OnValidationCompleted += OnValidationCompleted;
            LTLMManager.OnSeatStatusChanged += OnSeatStatusChanged;
            LTLMManager.OnKicked += OnKicked;
        }

        void OnDisable()
        {
            LTLMManager.OnValidationStarted -= OnValidationStarted;
            LTLMManager.OnValidationCompleted -= OnValidationCompleted;
            LTLMManager.OnSeatStatusChanged -= OnSeatStatusChanged;
            LTLMManager.OnKicked -= OnKicked;
        }

        void Start()
        {
            // Setup button listeners
            if (signOutButton != null)
                signOutButton.onClick.AddListener(OnSignOutClicked);
            if (refreshButton != null)
                refreshButton.onClick.AddListener(OnRefreshClicked);
            if (kickedOkButton != null)
                kickedOkButton.onClick.AddListener(() => kickedNoticePanel.SetActive(false));

            // Initial state
            HideAllPanels();

            // If already authenticated, show appropriate panel
            if (LTLMManager.Instance.IsAuthenticated)
            {
                if (LTLMManager.Instance.IsWaitingForSeat)
                {
                    ShowWaitingPanel();
                }
                else
                {
                    ShowActivePanel();
                }
            }
        }

        void Update()
        {
            // Update cooldown timer
            if (_refreshCooldown > 0)
            {
                _refreshCooldown -= Time.deltaTime;
                if (cooldownText != null)
                {
                    cooldownText.text = $"Next release in {Mathf.CeilToInt(_refreshCooldown)}s";
                    cooldownText.gameObject.SetActive(true);
                }
                if (refreshButton != null)
                    refreshButton.interactable = false;
            }
            else
            {
                if (cooldownText != null)
                    cooldownText.gameObject.SetActive(false);
                if (refreshButton != null)
                    refreshButton.interactable = true;
            }
        }

        // ============================================================
        // EVENT HANDLERS
        // ============================================================

        void OnValidationStarted()
        {
            ShowLoading("Checking license...");
        }

        void OnValidationCompleted(bool success, LicenseStatus status)
        {
            HideLoading();

            if (!success)
            {
                Debug.Log("[SeatDemo] Validation failed, showing activation screen");
                // In a real app, navigate to activation screen
                return;
            }

            switch (status)
            {
                case LicenseStatus.Active:
                    ShowActivePanel();
                    break;

                case LicenseStatus.ValidNoSeat:
                    ShowWaitingPanel();
                    LoadActiveSeats();
                    break;

                case LicenseStatus.Expired:
                case LicenseStatus.Suspended:
                case LicenseStatus.Revoked:
                    // Handle blocked states
                    activeStatusText.text = "License " + status.ToString();
                    break;
            }
        }

        void OnSeatStatusChanged(string seatStatus, int activeSeats, int maxSeats)
        {
            Debug.Log($"[SeatDemo] Seat status: {seatStatus} ({activeSeats}/{maxSeats})");

            if (seatInfoText != null)
            {
                seatInfoText.text = $"Seats: {activeSeats}/{maxSeats}";
            }

            // Check if we lost our seat
            if (seatStatus == "NO_SEAT" && activePanel.activeSelf)
            {
                ShowWaitingPanel();
                LoadActiveSeats();
            }
            // Check if we gained a seat
            else if (seatStatus == "OCCUPIED" && waitingPanel.activeSelf)
            {
                ShowActivePanel();
            }
        }

        void OnKicked(KickedNotice notice)
        {
            string nickname = notice.kickedByNickname ?? "Another device";
            kickedMessageText.text = $"Your session was ended by '{nickname}'";
            kickedNoticePanel.SetActive(true);

            // Transition to waiting mode
            ShowWaitingPanel();
            LoadActiveSeats();
        }

        // ============================================================
        // UI ACTIONS
        // ============================================================

        void OnSignOutClicked()
        {
            // DeactivateSeat returns false if offline (abuse prevention)
            if (!LTLMManager.Instance.DeactivateSeat())
            {
                if (activeStatusText != null)
                {
                    activeStatusText.text = "Cannot sign out while offline";
                }
                return;
            }
            
            LTLMManager.Instance.ClearLicenseCache();
            HideAllPanels();
            // In real app, navigate to activation screen
        }

        void OnRefreshClicked()
        {
            LoadActiveSeats();
        }

        void OnReleaseSeatClicked(string targetHwid, string targetNickname)
        {
            if (waitingStatusText != null)
                waitingStatusText.text = $"Releasing {targetNickname ?? "device"}...";

            LTLMManager.Instance.ReleaseSeat(targetHwid, claimSeat: true,
                response => {
                    if (response.seatClaimed)
                    {
                        waitingStatusText.text = "Seat claimed!";
                        // ShowActivePanel will be called by OnValidationCompleted
                    }
                    else
                    {
                        waitingStatusText.text = "Seat released but not claimed";
                        LoadActiveSeats(); // Refresh the list
                    }
                },
                error => {
                    // Check for cooldown error
                    if (error.Contains("wait"))
                    {
                        // Parse seconds from error if possible
                        _refreshCooldown = 300f; // Default 5 min cooldown
                    }
                    waitingStatusText.text = error;
                }
            );
        }

        // ============================================================
        // SEAT LOADING
        // ============================================================

        void LoadActiveSeats()
        {
            if (waitingStatusText != null)
                waitingStatusText.text = "Loading active seats...";

            LTLMManager.Instance.GetActiveSeats(
                response => {
                    DisplaySeats(response);
                },
                error => {
                    waitingStatusText.text = "Error: " + error;
                }
            );
        }

        void DisplaySeats(GetSeatsResponse response)
        {
            // Clear existing items
            foreach (var item in _seatItems)
            {
                Destroy(item);
            }
            _seatItems.Clear();

            waitingStatusText.text = $"All {response.maxSeats} seats are in use";

            // Create seat items for each non-current device
            foreach (var seat in response.seats)
            {
                if (seat.isCurrentDevice) continue; // Skip our own device

                var item = Instantiate(seatItemPrefab, seatListContainer);
                _seatItems.Add(item);

                // Setup seat item UI
                var nicknameText = item.transform.Find("NicknameText")?.GetComponent<Text>();
                var lastSeenText = item.transform.Find("LastSeenText")?.GetComponent<Text>();
                var releaseButton = item.transform.Find("ReleaseButton")?.GetComponent<Button>();

                if (nicknameText != null)
                    nicknameText.text = seat.nickname ?? MaskHwid(seat.hwid);
                
                if (lastSeenText != null)
                    lastSeenText.text = "Last seen: " + FormatLastSeen(seat.lastSeen);

                if (releaseButton != null)
                {
                    string hwid = seat.hwid;
                    string nickname = seat.nickname;
                    releaseButton.interactable = response.canReleaseSeat;
                    releaseButton.onClick.AddListener(() => OnReleaseSeatClicked(hwid, nickname));
                }
            }

            // If no seats to release, show message
            if (_seatItems.Count == 0)
            {
                waitingStatusText.text = "No other devices to disconnect";
            }
        }

        // ============================================================
        // UI HELPERS
        // ============================================================

        void HideAllPanels()
        {
            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (activePanel != null) activePanel.SetActive(false);
            if (waitingPanel != null) waitingPanel.SetActive(false);
            if (kickedNoticePanel != null) kickedNoticePanel.SetActive(false);
        }

        void ShowLoading(string message)
        {
            HideAllPanels();
            if (loadingPanel != null) loadingPanel.SetActive(true);
        }

        void HideLoading()
        {
            if (loadingPanel != null) loadingPanel.SetActive(false);
        }

        void ShowActivePanel()
        {
            HideAllPanels();
            if (activePanel != null) activePanel.SetActive(true);

            var license = LTLMManager.Instance.ActiveLicense;
            if (license != null && activeStatusText != null)
            {
                activeStatusText.text = "License Active";
                if (seatInfoText != null)
                {
                    seatInfoText.text = $"Seats: {license.activeSeats ?? 1}/{license.maxConcurrentSeats ?? 1}";
                }
            }
        }

        void ShowWaitingPanel()
        {
            HideAllPanels();
            if (waitingPanel != null) waitingPanel.SetActive(true);
            waitingStatusText.text = "Waiting for available seat...";
        }

        string MaskHwid(string hwid)
        {
            if (string.IsNullOrEmpty(hwid) || hwid.Length < 8) return "Device";
            return hwid.Substring(0, 4) + "...";
        }

        string FormatLastSeen(string lastSeen)
        {
            if (string.IsNullOrEmpty(lastSeen)) return "Unknown";
            
            if (System.DateTime.TryParse(lastSeen, out var dt))
            {
                var diff = System.DateTime.UtcNow - dt;
                if (diff.TotalMinutes < 1) return "Just now";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
                return $"{(int)diff.TotalDays}d ago";
            }
            return lastSeen;
        }
    }
}
