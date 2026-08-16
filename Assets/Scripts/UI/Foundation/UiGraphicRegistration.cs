using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace AnimalCafe.UI.Foundation
{
    /// <summary>
    /// Refreshes uGUI Graphic registration after a generated hierarchy is loaded.
    /// 在 generated hierarchy 加载后刷新 uGUI Graphic registration。
    /// </summary>
    public sealed class UiGraphicRegistration : MonoBehaviour
    {
        private Coroutine refreshRoutine;

        private void OnEnable()
        {
            refreshRoutine = StartCoroutine(RefreshAfterHierarchyLoad());
        }

        private void OnDisable()
        {
            if (refreshRoutine != null)
            {
                StopCoroutine(refreshRoutine);
                refreshRoutine = null;
            }
        }

        private IEnumerator RefreshAfterHierarchyLoad()
        {
            yield return null;
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.enabled) continue;
                graphic.enabled = false;
                graphic.enabled = true;
                graphic.SetAllDirty();
            }
            Canvas.ForceUpdateCanvases();
            refreshRoutine = null;
        }
    }
}
