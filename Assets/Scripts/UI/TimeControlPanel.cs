using AnimalCafe.Core.Time;
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

        private bool listenersRegistered;

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
        }

        private void OnEnable()
        {
            RegisterListeners();
        }

        private void OnDisable()
        {
            RemoveListeners();
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
            RegisterListeners();
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

            pauseButton.onClick.AddListener(gameTimeService.SetPaused);
            normalButton.onClick.AddListener(gameTimeService.SetNormal);
            fastButton.onClick.AddListener(gameTimeService.SetFast);
            listenersRegistered = true;
        }

        private void RemoveListeners()
        {
            if (!listenersRegistered)
            {
                return;
            }

            pauseButton.onClick.RemoveListener(gameTimeService.SetPaused);
            normalButton.onClick.RemoveListener(gameTimeService.SetNormal);
            fastButton.onClick.RemoveListener(gameTimeService.SetFast);
            listenersRegistered = false;
        }
    }
}
