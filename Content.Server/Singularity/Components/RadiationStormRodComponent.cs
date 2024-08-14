using Content.Server.Singularity.EntitySystems;

namespace Content.Server.Singularity.Components
{
    [RegisterComponent]
    public sealed class RadiationStormRodComponent : Component
    {
        [DataField("range")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float Range = 50f;
    }
}
