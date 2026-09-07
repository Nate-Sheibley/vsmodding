using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace spearsnjavlins;

public class ItemAnJSpear : Item
{
}

public class CollectibleBehaviorHeldMovementSpeed : CollectibleBehavior
{
    private float multiplier = 1f;

    public CollectibleBehaviorHeldMovementSpeed(CollectibleObject collObj)
        : base(collObj)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);

        multiplier = properties["multiplier"].AsFloat(1f);
    }
}
}