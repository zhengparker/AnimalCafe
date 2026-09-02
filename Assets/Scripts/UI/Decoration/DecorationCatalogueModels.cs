using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.UI.Decoration
{
    public enum DecorationCatalogueItemKind { Furniture, Floor, WallSurface, WallMounted }
    public enum DecorationCatalogueItemAvailability { Available }

    public enum DecorationCatalogueValidationIssueCode
    {
        NullEntry, WrongCategoryKind, InvalidStableId, MissingDisplayName, DuplicateItemId,
        MissingMaterial, MissingThumbnail, NoneWrongKind, NoneHasMaterial, NoneMissingIcon,
        MissingPrefab, InvalidFootprint, InvalidVisualDepth
    }

    public sealed class DecorationCatalogueValidationException : ArgumentException
    {
        public DecorationCatalogueValidationException(
            DecorationCatalogueValidationIssueCode code, string categoryId, string itemId, string message)
            : base(message)
        {
            Code = code;
            CategoryId = categoryId;
            ItemId = itemId;
        }

        public DecorationCatalogueValidationIssueCode Code { get; }
        public string CategoryId { get; }
        public string ItemId { get; }
    }

    public sealed class DecorationCatalogueItemModel
    {
        public DecorationCatalogueItemModel(string itemId, string displayName, Sprite thumbnail,
            DecorationCatalogueItemKind kind, bool isNoneOption,
            FurnitureDefinitionAsset furnitureDefinition = null)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Thumbnail = thumbnail;
            Kind = kind;
            IsNoneOption = isNoneOption;
            Availability = DecorationCatalogueItemAvailability.Available;
            FurnitureDefinition = furnitureDefinition;
        }

        public string ItemId { get; }
        public string DisplayName { get; }
        public Sprite Thumbnail { get; }
        public DecorationCatalogueItemKind Kind { get; }
        public bool IsNoneOption { get; }
        public DecorationCatalogueItemAvailability Availability { get; }
        public FurnitureDefinitionAsset FurnitureDefinition { get; }
    }

    public sealed class DecorationCategoryModel
    {
        public DecorationCategoryModel(string categoryId, string displayName,
            IReadOnlyList<DecorationCatalogueItemModel> items)
        {
            CategoryId = categoryId;
            DisplayName = displayName;
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var snapshot = items.ToArray();
            if (snapshot.Any(item => item == null))
            {
                throw new ArgumentException("Category items must not contain null.", nameof(items));
            }

            Items = Array.AsReadOnly(snapshot);
        }

        public string CategoryId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<DecorationCatalogueItemModel> Items { get; }
    }

    public static class DecorationCatalogueModelBuilder
    {
        private static readonly Regex StableIdPattern = new Regex(
            "^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant);

        public static IReadOnlyList<DecorationCategoryModel> Build(
            DecorationCatalogueAsset furnitureCatalogue, SurfaceStyleCatalogueAsset floorCatalogue,
            SurfaceStyleCatalogueAsset wallpaperCatalogue, SurfaceStyleCatalogueAsset paintCatalogue,
            SurfaceStyleCatalogueAsset wainscotingCatalogue, WallMountedCatalogueAsset wallDecorCatalogue,
            WallMountedCatalogueAsset windowsCatalogue)
        {
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            var furniture = BuildFurniture(furnitureCatalogue, "furniture", knownIds);
            var floor = BuildSurface(floorCatalogue, "floor", SurfaceStyleKind.Floor, knownIds);
            var wallpaper = BuildSurface(wallpaperCatalogue, "wallpaper", SurfaceStyleKind.Wallpaper, knownIds);
            var paint = BuildSurface(paintCatalogue, "paint", SurfaceStyleKind.Paint, knownIds);
            var wainscoting = BuildSurface(wainscotingCatalogue, "wainscoting", SurfaceStyleKind.Wainscoting, knownIds);
            var wallDecor = BuildWallMounted(wallDecorCatalogue, "wall-decor", WallMountedCatalogueKind.WallDecor, knownIds);
            var windows = BuildWallMounted(windowsCatalogue, "windows", WallMountedCatalogueKind.Windows, knownIds);

            return Array.AsReadOnly(new[]
            {
                new DecorationCategoryModel("furniture", "Furniture", furniture),
                new DecorationCategoryModel("floor", "Floor", floor),
                new DecorationCategoryModel("wallpaper", "Wallpaper", wallpaper),
                new DecorationCategoryModel("paint", "Paint", paint),
                new DecorationCategoryModel("wainscoting", "Wainscoting", wainscoting),
                new DecorationCategoryModel("wall-decor", "Wall Decor", wallDecor),
                new DecorationCategoryModel("windows", "Windows", windows)
            });
        }

        private static IReadOnlyList<DecorationCatalogueItemModel> BuildFurniture(
            DecorationCatalogueAsset catalogue, string categoryId, ISet<string> knownIds)
        {
            if (catalogue == null) throw new ArgumentNullException(nameof(catalogue));
            var items = new List<DecorationCatalogueItemModel>();
            for (var index = 0; index < catalogue.Entries.Count; index++)
            {
                var entry = catalogue.Entries[index];
                if (entry == null || entry.Definition == null) Throw(DecorationCatalogueValidationIssueCode.NullEntry, categoryId, null);
                var definition = entry.Definition;
                ValidateStableId(definition.DefinitionId, categoryId);
                ValidateDisplayName(definition.DisplayName, categoryId, definition.DefinitionId);
                if (definition.AllowedPlacementSurfaces != PlacementSurfaceType.Floor)
                    Throw(DecorationCatalogueValidationIssueCode.WrongCategoryKind, categoryId, definition.DefinitionId);
                if (definition.Prefab == null) Throw(DecorationCatalogueValidationIssueCode.MissingPrefab, categoryId, definition.DefinitionId);
                if (entry.Thumbnail == null) Throw(DecorationCatalogueValidationIssueCode.MissingThumbnail, categoryId, definition.DefinitionId);
                AddUniqueId(knownIds, categoryId, definition.DefinitionId);
                items.Add(new DecorationCatalogueItemModel(definition.DefinitionId, definition.DisplayName,
                    entry.Thumbnail, DecorationCatalogueItemKind.Furniture, false, definition));
            }

            return Array.AsReadOnly(items.ToArray());
        }

        private static IReadOnlyList<DecorationCatalogueItemModel> BuildSurface(
            SurfaceStyleCatalogueAsset catalogue, string categoryId, SurfaceStyleKind expectedKind,
            ISet<string> knownIds)
        {
            if (catalogue == null) throw new ArgumentNullException(nameof(catalogue));
            if (catalogue.Kind != expectedKind)
                Throw(DecorationCatalogueValidationIssueCode.WrongCategoryKind, categoryId, null);

            var items = new List<DecorationCatalogueItemModel>();
            for (var index = 0; index < catalogue.Entries.Count; index++)
            {
                var definition = catalogue.Entries[index];
                if (definition == null) Throw(DecorationCatalogueValidationIssueCode.NullEntry, categoryId, null);
                ValidateSurface(definition, categoryId, expectedKind);
                AddUniqueId(knownIds, categoryId, definition.StyleId);
                items.Add(new DecorationCatalogueItemModel(definition.StyleId, definition.DisplayName,
                    definition.Thumbnail, expectedKind == SurfaceStyleKind.Floor
                        ? DecorationCatalogueItemKind.Floor : DecorationCatalogueItemKind.WallSurface,
                    definition.IsNoneOption));
            }

            return Array.AsReadOnly(items.ToArray());
        }

        private static void ValidateSurface(SurfaceStyleDefinitionAsset definition, string categoryId,
            SurfaceStyleKind expectedKind)
        {
            if (definition.IsNoneOption && definition.Kind != SurfaceStyleKind.Wainscoting)
                Throw(DecorationCatalogueValidationIssueCode.NoneWrongKind, categoryId, definition.StyleId);
            if (definition.Kind != expectedKind)
                Throw(DecorationCatalogueValidationIssueCode.WrongCategoryKind, categoryId, definition.StyleId);
            ValidateStableId(definition.StyleId, categoryId);
            ValidateDisplayName(definition.DisplayName, categoryId, definition.StyleId);
            if (definition.IsNoneOption)
            {
                if (definition.Material != null) Throw(DecorationCatalogueValidationIssueCode.NoneHasMaterial, categoryId, definition.StyleId);
                if (definition.Thumbnail == null) Throw(DecorationCatalogueValidationIssueCode.NoneMissingIcon, categoryId, definition.StyleId);
                return;
            }

            if (definition.Material == null) Throw(DecorationCatalogueValidationIssueCode.MissingMaterial, categoryId, definition.StyleId);
            if (definition.Thumbnail == null) Throw(DecorationCatalogueValidationIssueCode.MissingThumbnail, categoryId, definition.StyleId);
        }

        private static IReadOnlyList<DecorationCatalogueItemModel> BuildWallMounted(
            WallMountedCatalogueAsset catalogue, string categoryId, WallMountedCatalogueKind expectedKind,
            ISet<string> knownIds)
        {
            if (catalogue == null) throw new ArgumentNullException(nameof(catalogue));
            if (catalogue.Kind != expectedKind)
                Throw(DecorationCatalogueValidationIssueCode.WrongCategoryKind, categoryId, null);

            var items = new List<DecorationCatalogueItemModel>();
            for (var index = 0; index < catalogue.Entries.Count; index++)
            {
                var definition = catalogue.Entries[index];
                if (definition == null) Throw(DecorationCatalogueValidationIssueCode.NullEntry, categoryId, null);
                ValidateStableId(definition.DefinitionId, categoryId);
                ValidateDisplayName(definition.DisplayName, categoryId, definition.DefinitionId);
                if (definition.Prefab == null) Throw(DecorationCatalogueValidationIssueCode.MissingPrefab, categoryId, definition.DefinitionId);
                if (definition.Thumbnail == null) Throw(DecorationCatalogueValidationIssueCode.MissingThumbnail, categoryId, definition.DefinitionId);
                if (definition.FootprintWidth < 1 || definition.FootprintHeight < 1)
                    Throw(DecorationCatalogueValidationIssueCode.InvalidFootprint, categoryId, definition.DefinitionId);
                var depth = definition.MaxVisualDepth;
                if (float.IsNaN(depth) || float.IsInfinity(depth) || depth < 0f || depth > WallMountedDefinitionAsset.MaximumVisualDepth)
                    Throw(DecorationCatalogueValidationIssueCode.InvalidVisualDepth, categoryId, definition.DefinitionId);
                AddUniqueId(knownIds, categoryId, definition.DefinitionId);
                items.Add(new DecorationCatalogueItemModel(definition.DefinitionId, definition.DisplayName,
                    definition.Thumbnail, DecorationCatalogueItemKind.WallMounted, false));
            }

            return Array.AsReadOnly(items.ToArray());
        }

        private static void ValidateStableId(string itemId, string categoryId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || !StableIdPattern.IsMatch(itemId))
                Throw(DecorationCatalogueValidationIssueCode.InvalidStableId, categoryId, itemId);
        }

        private static void ValidateDisplayName(string displayName, string categoryId, string itemId)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                Throw(DecorationCatalogueValidationIssueCode.MissingDisplayName, categoryId, itemId);
        }

        private static void AddUniqueId(ISet<string> knownIds, string categoryId, string itemId)
        {
            if (!knownIds.Add(itemId)) Throw(DecorationCatalogueValidationIssueCode.DuplicateItemId, categoryId, itemId);
        }

        private static void Throw(DecorationCatalogueValidationIssueCode code, string categoryId, string itemId)
        {
            throw new DecorationCatalogueValidationException(code, categoryId, itemId,
                $"Catalogue validation failed: {code} at '{categoryId}' for '{itemId ?? "<category>"}'.");
        }
    }
}
