using System;

namespace MSC.Lighting
{
    /// <summary>
    /// Process-session handoff used only by the native save composition. The
    /// owning ProductionLightingInstaller installs and removes the authority;
    /// gameplay still receives the grid through explicit component references.
    /// </summary>
    public static class LightingSessionAuthority
    {
        public static ElectricalGridService ActiveGrid { get; private set; }

        public static void Install(ElectricalGridService grid)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (ActiveGrid != null && !ReferenceEquals(ActiveGrid, grid))
            {
                throw new InvalidOperationException(
                    "A different lighting grid already owns this game session.");
            }

            ActiveGrid = grid;
        }

        public static void Uninstall(ElectricalGridService grid)
        {
            if (ReferenceEquals(ActiveGrid, grid))
            {
                ActiveGrid = null;
            }
        }
    }
}
