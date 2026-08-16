using UnityEngine;

namespace AnimalCafe.UI.Foundation
{
    /// <summary>
    /// Records the validation-scene recipe version so an unchanged build is byte-stable.
    /// 记录 validation scene recipe 版本，避免重复构建改变 Unity 内部 file ID。
    /// </summary>
    public sealed class Phase5UiFoundationSceneMarker : MonoBehaviour
    {
        [SerializeField] private int recipeVersion;

        public int RecipeVersion => recipeVersion;

        public void Configure(int version) => recipeVersion = version;
    }
}
