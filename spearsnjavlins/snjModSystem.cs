using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace spearsnjavlins;

public class spearsnjavlinsMod : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterItemClass("ItemSnJAtlatl", typeof(ItemSnJAtlatl));
        api.RegisterBehavior("CollectibleBehaviorHeldMovementSpeed", typeof(CollectibleBehaviorHeldMovementSpeed));
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
    }

    public override void Dispose()
    {
    }
}
