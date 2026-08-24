using System;
using System.Collections.Generic;
using AnimalCafe.Content;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    [Serializable]
    public sealed class DecorationCatalogueEntry
    {
        [SerializeField] private FurnitureDefinitionAsset definition;
        [SerializeField] private Sprite thumbnail;

        public FurnitureDefinitionAsset Definition => definition;
        public Sprite Thumbnail => thumbnail;
    }

    [CreateAssetMenu(menuName = "AnimalCafe/Decoration Catalogue")]
    public sealed class DecorationCatalogueAsset : ScriptableObject
    {
        [SerializeField] private List<DecorationCatalogueEntry> entries =
            new List<DecorationCatalogueEntry>();

        public IReadOnlyList<DecorationCatalogueEntry> Entries => entries;
    }
}
