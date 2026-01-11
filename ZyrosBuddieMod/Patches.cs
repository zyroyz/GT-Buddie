using HarmonyLib;
using BuddieMod.PluginInfo;

namespace Patches
{
    public class patches
    {
        public static void Patchall()
        {
            var harmoney2 = new Harmony(PluginInfo.GUID);
            harmoney2.PatchAll();
        }
    }

}
