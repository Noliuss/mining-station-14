using Content.Server.Power.Components;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Random;

using Content.Shared.Atmos;

using System.Linq;

// actually used
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Temperature.Components;
using Content.Shared.Materials;

namespace Content.Server.Mining;

[RegisterComponent]
public class FurnaceComponent : Component
{
    /// <summary>
    /// Current temperature inside furnace.
    /// </summary>
    [DataField("temperature")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float Temperature = Atmospherics.T20C;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool DoorOpen = false;

    [ViewVariables(VVAccess.ReadWrite)]
    public readonly Dictionary<string, int> Materials = new();

    [DataField("baseSpecHeat")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float BaseSpecHeat = 1000f;
}

public class FurnaceSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _sharedAudioSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FurnaceComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
        // on pour button pressed, pour
        // on door open, open
        // on door close, close
    }

    public override void Update(float dt)
    {
        foreach (var comp in EntityManager.EntityQuery<FurnaceComponent>())
        {
            UpdateTemp(comp.Owner, comp, dt);
            MeltOres(comp.Owner, comp);
            OreReactions(comp.Owner);
        }
    }

    private void OnAtmosUpdate(EntityUid uid, FurnaceComponent comp, AtmosDeviceUpdateEvent args)
    {
        float dt = 1/_atmosphereSystem.AtmosTickRate; // FIXME
        var environment = _atmosphereSystem.GetContainingMixture(uid, true, true);
        if (environment is null)
            return;

        float coeff = 100f; // TODO: higher if door is open
        float dQ = coeff * (comp.Temperature - environment.Temperature)* dt;
        _atmosphereSystem.AddHeat(environment, dQ);
        float dT = -dQ / SpecHeat(comp);
        comp.Temperature = MathF.Max(comp.Temperature + dT, environment.Temperature); // can't fall below ambient
    }

    private float SpecHeat(FurnaceComponent comp)
    {
        // TODO
        return comp.BaseSpecHeat;
    }

    private void UpdateTemp(EntityUid uid, FurnaceComponent comp, float dt)
    {
        // if powered, add temp
        if (TryComp<ApcPowerReceiverComponent>(uid, out var power) && power.Powered)
        {
            float energy = power.Load * (1 - power.DumpHeat) * dt;
            comp.Temperature += energy/SpecHeat(comp);
        }

        // peg temperature component to our internal number
        if (TryComp<TemperatureComponent>(uid, out var temp))
        {
            temp.CurrentTemperature = comp.Temperature;
        }
    }

    private void MeltOres(EntityUid uid, FurnaceComponent comp)
    {
        if (!TryComp<ServerStorageComponent>(uid, out var storage))
            return;

        foreach (var ore in storage.StoredEntities)
        {
            if (TryComp<MaterialComponent>(ore, out var material))
            {
                if (temp.CurrentTemperature > MeltingTemperature(material))
                {
                    Melt(ore, comp, material);
                }
            }
        }
    }

    private float MeltingTemperature(MaterialComponent material)
    {
        float max = 0;
        foreach ((string k, int v) in material.Materials)
        {
            if (_prototype.TryIndex<MaterialPrototype>(k, out var mat))
            {
                max = MathF.Max(mat.MeltingTemperature, max);
            }
        }
        return max;
    }

    private void Melt(EntityUid uid, FurnaceComponent furnace, MaterialComponent ore)
    {
        // TODO: Add my materials to the furnace
        QueueDel(ore.Owner);
    }

    private void OreReactions(EntityUid uid)
    {
        // 2C + O2 -> 2CO
        // iron oxide + CO -> Iron
    }

    private void Pour(EntityUid uid, FurnaceComponent furnace)
    {
        int total = furnace.Materials.Sum(x => x.Value);
        int numSheets = total/100;
        // pick prototype
        string proto = "SheetSteel";
        var result = Spawn(proto, Transform(uid).Coordinates);
        furnace.Materials.Clear();
    }
}
