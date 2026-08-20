using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace RapidGuard;

public class RapidGuardModSystem : ModSystem
{
    private ICoreServerAPI? api;

    public override void StartServerSide(ICoreServerAPI api)
    {
        this.api = api;

        GenRivuletsPatch.Initialize(api);
    }
}
