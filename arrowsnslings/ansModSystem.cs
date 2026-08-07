using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace arrowsnslings;

public class arrowsnslingsMod : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterItemClass("ItemAnSSling", typeof(ItemAnSSling));
        api.RegisterEntity("EntityThrownSlingBullet", typeof(EntityThrownSlingBullet));
        
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
