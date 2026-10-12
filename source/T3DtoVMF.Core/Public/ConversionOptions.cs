namespace T3DtoVMF.Core.Public
{
    public sealed class ConversionOptions
    {
        // Unreal mappers sometimes create brushes that are supposed
        // To be closed volume, which they are in the level - but they remove polygons that cant be seen
        // Usually, this results in planar holes that can be patched in order to not do non-manifold fallback
        // Additionaly, sometimes there may be holes by accident (AS-Guardia rock formations in the spawn cave)
        // This shouldn't touch brushes that genuinely are supposed to be non-manifold
        // Although, you never know what mapper intended, that's why this'll be behind a flag
        public bool TryNonManifoldPlanarHolesFixing { get; set; }

        public bool SkipConcave { get; set; }
        public bool SkipConvex { get; set; }
        public bool SkipNonManifold { get; set; }
        public bool SkipMovers { get; set; }
        public bool NoCSG { get; set; } // Skip the magic of translating Subtractive World CSG to Additive World
        public bool NoMoverBehavior { get; set; }
        public bool NoAreaPortals { get; set; } // Do not convert Unreal area portals
        public bool NoOutOfBounds { get; set; } // Skip brushes that have at least 1 vertex outside Source world bounds (16384 units)
        public double Scale { get; set; } = 1;

        // Walkable Surface in Source is a surface that has normal with z coord be at least 0.7
        // However, this can be changed in GMod now (sv_walkable_normal)
        public double WalkableSurface_Normal { get; set; } = 0.7;

        private static int ClampLightmapScale(int value) => Math.Clamp(value, Settings.LIGHTMAP_SCALE_MIN, Settings.LIGHTMAP_SCALE_MAX);

        private int _lightmapScale_Walkable = 16;
        public int LightmapScale_WalkableSurface {
            get => _lightmapScale_Walkable;
            set => _lightmapScale_Walkable = ClampLightmapScale(value);
        }

        private int _lightmapScale_Wall = 16;
        public int LightmapScale_Wall
        {
            get => _lightmapScale_Wall;
            set => _lightmapScale_Wall = ClampLightmapScale(value);
        }

        // Normal for ceiling isn't determined yet
        private int _lightmapScale_Ceiling = 16;
        public int LightmapScale_Ceiling
        {
            get => _lightmapScale_Ceiling;
            set => _lightmapScale_Ceiling = ClampLightmapScale(value);
        }

        public bool NoLights { get; set; }
        public bool NoPlayerSpawns { get; set; }
        public bool IncludeActive { get; set; } // CSG_Active, CSG_Intersect and CSG_Deintersect brushes are not map geometry, skipped by default
    }
}
