using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AnimalCafe.Content
{
    [CreateAssetMenu(menuName = "AnimalCafe/Content/Surface Style Catalogue")]
    public sealed class SurfaceStyleCatalogueAsset : ScriptableObject
    {
        [SerializeField] private SurfaceStyleKind kind;
        [SerializeField] private List<SurfaceStyleDefinitionAsset> entries =
            new List<SurfaceStyleDefinitionAsset>();

        public SurfaceStyleKind Kind => kind;
        public IReadOnlyList<SurfaceStyleDefinitionAsset> Entries =>
            System.Array.AsReadOnly(entries.ToArray());
    }
}
