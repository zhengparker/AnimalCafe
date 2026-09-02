using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AnimalCafe.Content
{
    public enum WallMountedCatalogueKind
    {
        WallDecor,
        Windows
    }

    [CreateAssetMenu(menuName = "AnimalCafe/Content/Wall Mounted Catalogue")]
    public sealed class WallMountedCatalogueAsset : ScriptableObject
    {
        [SerializeField] private WallMountedCatalogueKind kind;
        [SerializeField] private List<WallMountedDefinitionAsset> entries =
            new List<WallMountedDefinitionAsset>();

        public WallMountedCatalogueKind Kind => kind;
        public IReadOnlyList<WallMountedDefinitionAsset> Entries =>
            System.Array.AsReadOnly(entries.ToArray());
    }
}
