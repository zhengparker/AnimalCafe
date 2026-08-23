using AnimalCafe.Core.Events;
using AnimalCafe.Core.Time;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI
{
    /// <summary>
    /// 将 placeholder UI buttons 连接到统一 Game Time service。
    /// Connects placeholder UI buttons to the shared Game Time service.
    /// </summary>
    public sealed class TimeControlPanel : MonoBehaviour
    {
        [SerializeField]
        private GameTimeService gameTimeService;

        [SerializeField]
        private Button pauseButton;

        [SerializeField]
        private Button normalButton;

        [SerializeField]
        private Button fastButton;

        [SerializeField]
        private GameObject pauseSelectedVisual;

        [SerializeField]
        private GameObject normalSelectedVisual;

        [SerializeField]
        private GameObject fastSelectedVisual;

        [SerializeField]
        private TMP_Text pauseLabel;

        private bool listenersRegistered;
        private bool speedListenerRegistered;
        private bool decorationPauseLocked;

        private void Start()
        {
            if (!HasRequiredReferences())
            {
                Debug.LogError(
                    "[TimeControlPanel] Game Time service and all three buttons are required.",
                    this);
                enabled = false;
                return;
            }

            RegisterListeners();
            RegisterSpeedListener();
            RefreshSelectedVisuals();
        }

        private void OnEnable()
        {
            RegisterListeners();
            RegisterSpeedListener();
            RefreshSelectedVisuals();
        }

        private void OnDisable()
        {
            RemoveListeners();
            RemoveSpeedListener();
        }

        public void Configure(
            GameTimeService service,
            Button pause,
            Button normal,
            Button fast)
        {
            RemoveListeners();
            gameTimeService = service;
            pauseButton = pause;
            normalButton = normal;
            fastButton = fast;
            ResolveSelectedVisuals();
            RegisterListeners();
            RegisterSpeedListener();
            RefreshSelectedVisuals();
        }

        /// <summary>
        /// Decoration Mode owns a Pause lease, so 1x/2x cannot override it.
        /// Decoration Mode 持有 Pause lease 时，禁止 1x/2x 覆盖该状态。
        /// </summary>
        public void SetDecorationPauseLock(bool locked)
        {
            decorationPauseLocked = locked;
            if (normalButton != null)
            {
                normalButton.interactable = !locked;
            }

            if (fastButton != null)
            {
                fastButton.interactable = !locked;
            }

            if (pauseButton != null)
            {
                pauseButton.interactable = !locked;
            }

            RefreshSelectedVisuals();
        }

        private bool HasRequiredReferences()
        {
            return gameTimeService != null
                && pauseButton != null
                && normalButton != null
                && fastButton != null;
        }

        private void RegisterListeners()
        {
            if (listenersRegistered || !HasRequiredReferences())
            {
                return;
            }

            pauseButton.onClick.AddListener(gameTimeService.TogglePaused);
            normalButton.onClick.AddListener(gameTimeService.SetNormal);
            fastButton.onClick.AddListener(gameTimeService.SetFast);
            listenersRegistered = true;
        }

        private void RegisterSpeedListener()
        {
            if (speedListenerRegistered || gameTimeService == null)
            {
                return;
            }

            GameEventBus.GameSpeedChanged += HandleGameSpeedChanged;
            speedListenerRegistered = true;
        }

        private void RemoveListeners()
        {
            if (!listenersRegistered)
            {
                return;
            }

            pauseButton.onClick.RemoveListener(gameTimeService.TogglePaused);
            normalButton.onClick.RemoveListener(gameTimeService.SetNormal);
            fastButton.onClick.RemoveListener(gameTimeService.SetFast);
            listenersRegistered = false;
        }

        private void RemoveSpeedListener()
        {
            if (!speedListenerRegistered)
            {
                return;
            }

            GameEventBus.GameSpeedChanged -= HandleGameSpeedChanged;
            speedListenerRegistered = false;
        }

        private void HandleGameSpeedChanged(GameSpeedChangedEvent speedChanged)
        {
            RefreshSelectedVisuals(speedChanged.Current);
        }

        private void RefreshSelectedVisuals()
        {
            ResolveSelectedVisuals();
            RefreshSelectedVisuals(gameTimeService != null
                ? gameTimeService.CurrentSpeed
                : GameSpeed.Normal);
        }

        private void RefreshSelectedVisuals(GameSpeed speed)
        {
            ResolvePauseLabel();
            if (pauseLabel != null)
            {
                pauseLabel.text = speed == GameSpeed.Paused ? "Resume" : "Pause";
            }

            if (decorationPauseLocked)
            {
                speed = GameSpeed.Paused;
            }

            SetSelected(pauseSelectedVisual, speed == GameSpeed.Paused);
            SetSelected(normalSelectedVisual, speed == GameSpeed.Normal);
            SetSelected(fastSelectedVisual, speed == GameSpeed.Fast);
        }

        private void ResolveSelectedVisuals()
        {
            pauseSelectedVisual ??= FindSelectedVisual(pauseButton);
            normalSelectedVisual ??= FindSelectedVisual(normalButton);
            fastSelectedVisual ??= FindSelectedVisual(fastButton);
            ResolvePauseLabel();
        }

        private void ResolvePauseLabel()
        {
            pauseLabel ??= pauseButton != null
                ? pauseButton.GetComponentInChildren<TMP_Text>(true)
                : null;
        }

        private static GameObject FindSelectedVisual(Button button)
        {
            return button != null
                ? button.transform.Find("SelectedVisual")?.gameObject
                : null;
        }

        private static void SetSelected(GameObject visual, bool selected)
        {
            if (visual != null && visual.activeSelf != selected)
            {
                visual.SetActive(selected);
            }
        }
    }
}
