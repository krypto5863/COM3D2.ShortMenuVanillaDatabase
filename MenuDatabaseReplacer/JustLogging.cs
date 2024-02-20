using HarmonyLib;

namespace ShortMenuVanillaDatabase
{
	internal class JustLogging
	{
		[HarmonyPatch(typeof(MenuDataBase), "JobFinished")]
		[HarmonyPostfix]
		private static void IsFinished(ref bool __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"A call to check if job was finished was made, returned {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetDataSize")]
		[HarmonyPostfix]
		private static void GiveDataSize(ref int __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning size of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "IsHeaderCheck")]
		[HarmonyPostfix]
		private static void Header(ref bool __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning headercheck of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetVersion")]
		[HarmonyPostfix]
		private static void GetVer(ref int __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning version of {__result}");
		}

		//Unimplemented...
		[HarmonyPatch(typeof(MenuDataBase), "GetNativeHash")]
		[HarmonyPostfix]
		private static void givehash(ref ulong __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning hash of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetMenuFileName")]
		[HarmonyPostfix]
		private static void menufilename(ref string __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning menufilename of {__result}");
		}

		//Not Implemented...
		[HarmonyPatch(typeof(MenuDataBase), "GetParentMenuFileName")]
		[HarmonyPostfix]
		private static void parentmenufile(ref string __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning parentmenufile of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetSrcFileName")]
		[HarmonyPostfix]
		private static void getsrcfilename(ref string __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning srcfilename of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetItemName")]
		[HarmonyPostfix]
		private static void getitemanme(ref string __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning returning item name of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetCategoryName")]
		[HarmonyPostfix]
		private static void categoryname(ref string __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning category of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetInfoText")]
		[HarmonyPostfix]
		private static void returninginfotext(ref string __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning infotext of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetMenuName")]
		[HarmonyPostfix]
		private static void menuname(ref string __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning menuname of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetItemInfoText")]
		[HarmonyPostfix]
		private static void iteminfotext(ref string __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning iteminfotext of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetCategoryMpnText")]
		[HarmonyPostfix]
		private static void mpntext(ref string __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning mpntext of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetCategoryMpn")]
		[HarmonyPostfix]
		private static void categorympn(ref int __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning category mpn of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetColorSetMpn")]
		[HarmonyPostfix]
		private static void colorsetmpn(ref int __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning colorsetmpn of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetMenuNameInColorSet")]
		[HarmonyPostfix]
		private static void getmenucolor(ref string __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning menu color of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetMultiColorId")]
		[HarmonyPostfix]
		private static void multicolorid(ref int __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning multicolorid of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetIconS")]
		[HarmonyPostfix]
		private static void geticonstring(ref string __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning icon of {__result}");
		}

		//Not Implemented...
		[HarmonyPatch(typeof(MenuDataBase), "GetSaveItem")]
		[HarmonyPostfix]
		private static void getsaveitem(ref string __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning save item of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetBoDelOnly")]
		[HarmonyPostfix]
		private static void bodel(ref bool __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning delmenu of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetPriority")]
		[HarmonyPostfix]
		private static void priority(ref float __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning priority of {__result}");
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetIsMan")]
		[HarmonyPostfix]
		private static void getisman(ref bool __result)
		{
			ShortMenuVanillaDatabase.PLogger.LogDebug($"returning is man menu of {__result}");
		}

		//Not Implemented...
		[HarmonyPatch(typeof(MenuDataBase), "GetIsCollabo")]
		[HarmonyPostfix]
		private static void iscoll(ref bool __result, MenuDataBase __instance)
		{
			if (__result)
			{
				ShortMenuVanillaDatabase.PLogger.LogDebug($"returning is collabo of {__result} for {__instance.GetMenuFileName()}");
			}
		}
	}
}