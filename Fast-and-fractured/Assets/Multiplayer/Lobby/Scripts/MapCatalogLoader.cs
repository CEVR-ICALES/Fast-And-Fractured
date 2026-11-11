using UnityEngine;

namespace FastAndFractured.Multiplayer.Lobby
{
    public static class MapCatalogLoader
    {
        private static MapCatalog _cached;

        public static MapCatalog Get()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<MapCatalog>("MapCatalog");
            if (_cached == null)
                Debug.LogError("[MapCatalogLoader] MapCatalog not found at Resources/MapCatalog. Create via Assets > FastAndFractured > Multiplayer > Map Catalog.");
            return _cached;
        }

        public static void InvalidateCache() => _cached = null;
    }
}
