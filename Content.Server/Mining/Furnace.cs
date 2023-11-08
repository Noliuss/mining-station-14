using Content.Server.Power.Components;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Random;

using Content.Shared.Atmos;

using System.Linq;

// actually used
using Robust.Shared.Prototypes;

using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Stack;
using Content.Server.Temperature.Components;
using Content.Shared.Materials;

namespace Content.Server.Mining;

[RegisterComponent]
public class FurnaceComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public bool DoorOpen = false;

    [ViewVariables(VVAccess.ReadWrite)]
    public readonly Dictionary<string, int> Materials = new();

    [DataField("baseSpecHeat")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float BaseSpecHeat = 1000f;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool ForcePour = false; // set to true to force pour using VV, for debugging
}

public class FurnaceSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _sharedAudioSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly StackSystem _stack = default!;

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
            if (!TryComp<TemperatureComponent>(comp.Owner, out var temp))
                continue;

            UpdateTemp(comp.Owner, comp, temp, dt);
            MeltOres(comp.Owner, comp, temp);
            OreReactions(comp.Owner, comp, temp);

            if (comp.ForcePour)
            {
                Pour(comp.Owner, comp);
                comp.ForcePour = false;
            }
        }
    }

    private void OnAtmosUpdate(EntityUid uid, FurnaceComponent comp, AtmosDeviceUpdateEvent args)
    {
        if (TryComp<TemperatureComponent>(uid, out var temp))
        {
            temp.AtmosTemperatureTransferEfficiency = 0.05f; // TODO: higher if door open
        }
    }

    private float SpecHeat(FurnaceComponent comp)
    {
        // TODO
        return comp.BaseSpecHeat;
    }

    private void UpdateTemp(EntityUid uid, FurnaceComponent comp, TemperatureComponent temp, float dt)
    {
        if (TryComp<ApcPowerReceiverComponent>(uid, out var power) && power.Powered)
        {
            float energy = power.Load * (1 - power.DumpHeat) * dt;
            temp.CurrentTemperature += energy/SpecHeat(comp);
        }
    }

    private void MeltOres(EntityUid uid, FurnaceComponent comp, TemperatureComponent temp)
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
        foreach ((var k, var v) in ore.Materials)
        {
            if (!furnace.Materials.ContainsKey(k))
                furnace.Materials[k] = 0;
            furnace.Materials[k] += v;
        }
        QueueDel(ore.Owner);
    }

    private void OreReactions(EntityUid uid, FurnaceComponent furnace, TemperatureComponent temp)
    {
        // 2C + O2 -> 2CO
        // iron oxide + CO -> Iron
    }

    private void Pour(EntityUid uid, FurnaceComponent furnace)
    {
        int total = furnace.Materials.Sum(x => x.Value);
        int numSheets = total/100;
        // TODO: pick prototype
        string proto = "SheetSteel1";
        var result = Spawn(proto, Transform(uid).Coordinates);
        if (TryComp<MaterialComponent>(result, out var mat))
        {
            mat.Materials.Clear();
            foreach ((var k, var v) in furnace.Materials)
            {
                mat.Materials.Add(k, v/numSheets);
            }
        }
        _stack.SetCount(result, numSheets);
        furnace.Materials.Clear();
    }
}
