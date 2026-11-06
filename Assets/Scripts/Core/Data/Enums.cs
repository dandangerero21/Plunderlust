namespace Plunderlust.Core.Data
{
    /// <summary>
    /// Represents the four sailing states for ship propulsion and maneuverability.
    /// </summary>
    public enum SailState
    {
        Anchored = 0,
        Rowing = 1,
        BattleSails = 2,
        FullCanvas = 3
    }

    /// <summary>
    /// The four localized critical ship subsystems subject to damage, degradation, and repair.
    /// </summary>
    public enum ShipSubsystemType
    {
        Hull = 0,
        SailsRigging = 1,
        RudderHelm = 2,
        GunDeck = 3
    }

    /// <summary>
    /// Naval factions governing open-world reputation and hostile encounters.
    /// </summary>
    public enum FactionType
    {
        ImperialNavy = 0,
        MerchantGuild = 1,
        CorsairBrethren = 2,
        FreeportTraders = 3
    }

    /// <summary>
    /// The five specialized officer roles hireable in port taverns.
    /// </summary>
    public enum OfficerSpecialtyType
    {
        Carpenter = 0,
        Boatswain = 1,
        MasterGunner = 2,
        Sailmaster = 3,
        Quartermaster = 4
    }

    /// <summary>
    /// Specialist tier influencing passive bonuses and hire costs.
    /// </summary>
    public enum SpecialistTier
    {
        Common = 0,
        Veteran = 1,
        Legendary = 2
    }

    /// <summary>
    /// Commodity and resource types carried in cargo holds or salvaged at sea.
    /// </summary>
    public enum CargoItemType
    {
        Gold = 0,
        Timber = 1,
        Canvas = 2,
        Cannonballs = 3,
        Sugar = 4,
        Rum = 5,
        Spices = 6,
        TreasureMap = 7,
        Silk = 8
    }

    /// <summary>
    /// Hull tiers corresponding to Kenney Pirate Kit vessels.
    /// </summary>
    public enum ShipTier
    {
        Tier1_LeakyRowboat = 1,
        Tier2_CoastalCutter = 2,
        Tier3_PirateBrigantine = 3,
        Tier4_RoyalGalleon = 4,
        Tier5_GhostGalleon = 5
    }

    /// <summary>
    /// Diplomatic alignment tiers based on faction reputation (-100 to +100).
    /// </summary>
    public enum FactionStandingTier
    {
        Hunted = 0,         // -100 to -60: Naval hunter squadrons dispatched
        Hostile = 1,        // -59 to -20: Attacked on sight
        Neutral = 2,        // -19 to +19: Standard trade & entry
        Friendly = 3,       // +20 to +59: Discounts at port taverns & markets
        PrivateerAlly = 4   // +60 to +100: Allied in combat, maximum privileges
    }

    /// <summary>
    /// Tactical states for naval AI navigation and combat behavior.
    /// </summary>
    public enum AICombatState
    {
        PatrollingTradeLane = 0,
        ClosingDistance = 1,
        BroadsideOrbit = 2,
        Fleeing = 3,
        Surrendered = 4
    }
}
