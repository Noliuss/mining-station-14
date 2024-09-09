using System.Threading;
using Content.Server.DoAfter;
using Content.Server.Gatherable.Components;
using Content.Shared.Damage;
using Content.Shared.EntityList;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Server.Mech.Equipment.Components;
using Content.Server.Mech.Systems;
using Content.Shared.Mech;
using Content.Shared.Mech.Equipment.Components;

namespace Content.Server.Mech.Equipment.EntitySystems;

/// <summary>
/// Handles <see cref="MechGatheringToolComponent"/> 
/// Probably didn't need a whole other class for this but I'm too lazy to...
/// </summary>
public sealed class MechGatheringToolSystem : EntitySystem
{
    [Dependency] private readonly MechSystem _mech = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechGatheringToolComponent, MechEquipmentRemovedEvent>(OnEquipmentRemoved);
        SubscribeLocalEvent<MechGatheringToolComponent, InteractNoHandEvent>(OnInteract);
        SubscribeLocalEvent<GatheringDoafterCancel>(OnDoafterCancel);
        SubscribeLocalEvent<MechGatheringToolComponent, GatheringDoafterSuccess>(OnDoafterSuccess);
    }

    private void OnEquipmentRemoved(EntityUid uid, MechGatheringToolComponent component, ref MechEquipmentRemovedEvent args)
    {
        if (!TryComp<MechEquipmentComponent>(uid, out var equipmentComponent) ||
            equipmentComponent.EquipmentOwner == null)
            return;
        var mech = equipmentComponent.EquipmentOwner.Value;
    }

    private void OnInteract(EntityUid uid, MechGatheringToolComponent component, InteractNoHandEvent args)
    {
         if (!TryComp<MechGatheringToolComponent>(uid, out var tool) ||
            args.Target is null ||
            tool.GatheringEntities.TryGetValue(args.Target.Value, out var cancelToken))
            return;

        if (!TryComp(args.Target.Value, out GatherableComponent? resource) ||
            resource.ToolWhitelist?.IsValid(uid) == false)
            return;
            
        // Can't gather too many entities at once.
        if (tool.MaxGatheringEntities < tool.GatheringEntities.Count + 1)
            return;

        cancelToken = new CancellationTokenSource();
        tool.GatheringEntities[args.Target.Value] = cancelToken;

        var doAfter = new DoAfterEventArgs(args.User, tool.GatheringTime, cancelToken.Token, uid)
        {
            BreakOnDamage = true,
            BreakOnStun = true,
            BreakOnTargetMove = true,
            BreakOnUserMove = true,
            MovementThreshold = 0.25f,
            BroadcastCancelledEvent = new GatheringDoafterCancel { Tool = uid, Resource = args.Target.Value },
            TargetFinishedEvent = new GatheringDoafterSuccess { Tool = uid, Resource = args.Target.Value, Player = args.User },
        };

        _audio.PlayPvs(tool.GatheringSound, uid);
        _doAfterSystem.DoAfter(doAfter);
    }

    private void OnDoafterSuccess(EntityUid uid, MechGatheringToolComponent tool, GatheringDoafterSuccess ev)
    {
        if (!TryComp(ev.Resource, out GatherableComponent? resource))
            return;

        // Complete the gathering process
        _damageableSystem.TryChangeDamage(ev.Resource, tool.Damage, origin: ev.Player);
        _audio.PlayPvs(tool.GatheringSound, ev.Resource);
        tool.GatheringEntities.Remove(ev.Resource);

        // Spawn the loot!
        if (resource.MappedLoot == null)
            return;

        var playerPos = Transform(ev.Player).MapPosition;

        foreach (var (tag, table) in resource.MappedLoot)
        {
            if (tag != "All")
            {
                if (!_tagSystem.HasTag(tool.Owner, tag))
                    continue;
            }
            var getLoot = _prototypeManager.Index<EntityLootTablePrototype>(table);
            var spawnLoot = getLoot.GetSpawns();
            var spawnPos = playerPos.Offset(_random.NextVector2(0.3f));
            Spawn(spawnLoot[0], spawnPos);
        }
    }

    private void OnDoafterCancel(GatheringDoafterCancel ev)
    {
        if (!TryComp<MechGatheringToolComponent>(ev.Tool, out var tool))
            return;

        tool.GatheringEntities.Remove(ev.Resource);
    }

    private sealed class GatheringDoafterCancel : EntityEventArgs
    {
        public EntityUid Tool;
        public EntityUid Resource;
    }

    private sealed class GatheringDoafterSuccess : EntityEventArgs
    {
        public EntityUid Tool;
        public EntityUid Resource;
        public EntityUid Player;
    }
}
