using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace arrowsnslings;

public class ItemAnSBullet : Item
{
    public override void GetHeldItemInfo(
        ItemSlot inSlot,
        StringBuilder dsc,
        IWorldAccessor world,
        bool withDebugInfo)
    {
        // Keep the normal Vintage Story tooltip
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

        if (inSlot?.Itemstack == null)
        {
            return;
        }

        var attributes = inSlot.Itemstack.Collectible.Attributes;

        if (attributes == null)
        {
            return;
        }

        // -------------------------
        // DAMAGE
        // -------------------------

        float damage = attributes["damage"].AsFloat(0f);

        if (damage >= 0f)
        {
            dsc.AppendLine(
                Lang.Get("arrow-piercingdamage-add", "+" + damage)
            );
        }
        else
        {
            dsc.AppendLine(
                Lang.Get("arrow-piercingdamage-remove", damage)
            );
        }

        // -------------------------
        // DAMAGE TIER
        // -------------------------

        int damageTier = attributes["damageTier"].AsInt(0);

        dsc.AppendLine(
            "Damage tier: " + damageTier
        );

        // -------------------------
        // ACCURACY
        // -------------------------

        float accModifier = attributes["accModifier"].AsFloat(0f);

        string accuracyText;

        if (accModifier > 0f)
        {
            accuracyText = "+" + (accModifier * 100f).ToString("0") + "%";
        }
        else
        {
            accuracyText = (accModifier * 100f).ToString("0") + "%";
        }

        dsc.AppendLine(
            "Accuracy: " + accuracyText
        );

        // -------------------------
        // RANGE
        // -------------------------

        float rangeMult = attributes["rangeMult"].AsFloat(1f);

        float rangeModifier = (rangeMult - 1f) * 100f;

        string rangeText;

        if (rangeModifier > 0f)
        {
            rangeText = "+" + rangeModifier.ToString("0") + "%";
        }
        else
        {
            rangeText = rangeModifier.ToString("0") + "%";
        }

        dsc.AppendLine(
            "Range: " + rangeText
        );
    }
}