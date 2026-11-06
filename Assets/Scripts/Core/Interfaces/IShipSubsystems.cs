namespace Plunderlust.Core.Interfaces
{
    /// <summary>
    /// Provides real-time efficiency metrics calculated from localized subsystem health.
    /// Implemented by ShipSubsystemController in Phase 2.
    /// </summary>
    public interface IShipSubsystemProvider
    {
        /// <summary>
        /// Efficiency scalar for propulsion derived from Sails and Rigging integrity [0.15 .. 1.0].
        /// </summary>
        float SailsEfficiency { get; }

        /// <summary>
        /// Efficiency scalar for turning torque derived from Rudder and Helm integrity [0.1 .. 1.0].
        /// </summary>
        float RudderEfficiency { get; }

        /// <summary>
        /// Current normalized hull health [0.0 .. 1.0].
        /// </summary>
        float HullIntegrityRatio { get; }

        /// <summary>
        /// Current bilge water flooding ratio [0.0 .. 1.0].
        /// </summary>
        float FloodingRatio { get; }

        /// <summary>
        /// True when rudder health drops below 25%, causing periodic helm pulling.
        /// </summary>
        bool IsRudderCritical { get; }

        /// <summary>
        /// True if sails are damaged below 20%, disallowing Full Canvas.
        /// </summary>
        bool IsSailsCrippled { get; }

        /// <summary>
        /// True when the ship is foundering and actively sinking beneath the waves.
        /// </summary>
        bool IsSinking { get; }
    }

    /// <summary>
    /// Provides manpower efficiency scalars based on deckhand counts.
    /// Implemented by CrewManager in Phase 3.
    /// </summary>
    public interface ICrewEfficiencyProvider
    {
        /// <summary>
        /// Manpower efficiency applied to sail handling and adjustments [0.35 .. 1.0].
        /// </summary>
        float SailHandlingEfficiency { get; }

        /// <summary>
        /// Manpower efficiency applied to rudder responsiveness [0.35 .. 1.0].
        /// </summary>
        float RudderHandlingEfficiency { get; }

        /// <summary>
        /// Manpower efficiency applied to gun battery reloads [0.35 .. 1.0].
        /// </summary>
        float GunnerEfficiency { get; }

        /// <summary>
        /// Manpower efficiency applied to bilge pumping and repairs [0.35 .. 1.0].
        /// </summary>
        float BilgeRepairEfficiency { get; }
    }
}
