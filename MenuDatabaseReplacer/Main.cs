using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Text;

//These two lines tell your plugin to not give a flying fuck about accessing private variables/classes whatever. It requires a publicized stubb of the library with those private objects though.
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace ShortMenuVanillaDatabase
{
	//This is the metadata set for your plugin.
	[BepInPlugin("ShortMenuVanillaDatabase", "ShortMenuVanillaDatabase", "1.0.3")]
	public class Main : BaseUnityPlugin
	{
		public static Main @this;
		//Static var for the logger so you can log from other classes.
		public static ManualLogSource logger;
		//This is where the magic happens.
		public static MenuDatabaseReplacement Database;
		//I'd rather not use an index tbh but whatever, Kiss's implementation is literally retarded.
		private static Dictionary<MenuDataBase, int> IndexToRead = new Dictionary<MenuDataBase, int>();

		//Config entry variable. You set your configs to this.
		//internal static ConfigEntry<bool> ExampleConfig;

		static Harmony harmony;

		private void Awake()
		{
			//Useful for engaging coroutines or accessing variables non-static variables. Completely optional though.
			@this = this;

			//pushes the logger to a public static var so you can use the bepinex logger from other classes.
			logger = Logger;

			//Binds the configuration. In other words it sets your ConfigEntry var to your config setup.
			//ExampleConfig = Config.Bind("Section", "Name", false, "Description");

			//Installs the patches in the Main class.

#if !OnlyCompare
			harmony = Harmony.CreateAndPatchAll(typeof(Main));
#else
			//Harmony.CreateAndPatchAll(typeof(JustLogging));
#endif
		}

		//Basic harmony patch format. You specify the class to be patched and the method within that class to be patched. This patcher prefixes the method, meaning it runs before the patched method does. You can also postfix, run after the method patches and do lots of things like change parameters and results with harmony patching. Very powerful.		
		[HarmonyPatch(typeof(MenuDataBase), MethodType.Constructor, new Type[] { typeof(IntPtr), typeof(EnumData), typeof(EnumData) })]
		[HarmonyPrefix]
		private static void BuildingDatabase()
		{
			logger.LogDebug("Created the database...");

			Database = new MenuDatabaseReplacement();
		}

		[HarmonyPatch(typeof(MenuDataBase), "JobFinished")]
		[HarmonyPrefix]
		private static bool IsFinished(ref bool __result)
		{
			__result = Database.Done;

#if !OnlyCompare
			return false;
#else
			return Database.Done;
#endif
		}

		[HarmonyPatch(typeof(MenuDataBase), "StartAnalysis")]
		[HarmonyPrefix]
		private static bool StartLoad()
		{
			logger.LogInfo("Starting Vanilla Menu Files Analysis...");

			@this.StartCoroutine(Database.StartLoading());
#if !OnlyCompare
			return false;
#else
			return true;
#endif
		}
		[HarmonyPatch(typeof(MenuDataBase), "Dispose", new Type[] { typeof(bool) })]
		[HarmonyPrefix]
		private static bool DisposeVar(MenuDataBase __instance)
		{
			Database = null;
			__instance.is_disposed_ = true;
#if !OnlyCompare
			return false;
#else
			return true;
#endif
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetDataSize")]
		[HarmonyPrefix]
		private static bool GiveDataSize(ref int __result)
		{
#if !OnlyCompare
			__result = Database.MenusList.Count;

			return false;
#else
			return true;
#endif
		}

		[HarmonyPatch(typeof(MenuDataBase), "SetIndex")]
		[HarmonyPrefix]
		private static bool SetIndexToRead(ref int __0, ref MenuDataBase __instance)
		{
			IndexToRead[__instance] = __0;
#if !OnlyCompare
			return false;
#else

			DLLMenuDataBase.SetIndex(ref __instance.data_, __0);

			ulong nativeHash = __instance.GetNativeHash();
			IntPtr fileNameToNativeHash = DLLMenuDataBase.GetFileNameToNativeHash(ref __instance.data_, nativeHash);
			if (fileNameToNativeHash == IntPtr.Zero)
			{
				return true;
			}
			var filename = Marshal.PtrToStringAnsi(fileNameToNativeHash);

			Database.IndexToRead = Database.MenusList.FirstOrDefault(kv => kv.Value.FileName.Equals(filename)).Key;

			return true;
#endif
		}

		[HarmonyPatch(typeof(MenuDataBase), "IsHeaderCheck")]
		[HarmonyPrefix]
		private static bool HeaderCheck(ref bool __result)
		{
#if !OnlyCompare
			__result = true;

			return false;
#else
			return true;
#endif
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetVersion")]
		[HarmonyPrefix]
		private static bool GetVersion(ref int __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = Database.MenusList[IndexToRead[__instance]].Version;

			return false;
#else
			return true;
#endif
		}
		//Unimplemented... It's mostly only used within MenuDataBase for some functions. We don't need it.
		[HarmonyPatch(typeof(MenuDataBase), "GetNativeHash")]
		[HarmonyPrefix]
		private static bool HashGet(ref ulong __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = 0;

			return false;
#else
			return true;
#endif
		}
		[HarmonyPatch(typeof(MenuDataBase), "GetMenuFileName")]
		[HarmonyPrefix]
		private static bool MenuName(ref string __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = Database.MenusList[IndexToRead[__instance]].FileName;

			return false;
#else
			return true;
#endif
		}
		//Not Implemented... Never really called though. What's even the point?
		[HarmonyPatch(typeof(MenuDataBase), "GetParentMenuFileName")]
		[HarmonyPrefix]
		private static bool ParentMenuName(ref string __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = "";

			return false;
#else
			return true;
#endif
		}
		[HarmonyPatch(typeof(MenuDataBase), "GetSrcFileName")]
		[HarmonyPrefix]
		private static bool GetSrcFileName(ref string __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = Database.MenusList[IndexToRead[__instance]].PathInMenu;

			return false;
#else
			return true;
#endif
		}
		[HarmonyPatch(typeof(MenuDataBase), "GetItemName")]
		[HarmonyPrefix]
		private static bool GetName(ref string __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = Database.MenusList[IndexToRead[__instance]].Name;

			return false;
#else
			return true;
#endif
		}
		[HarmonyPatch(typeof(MenuDataBase), "GetCategoryName")]
		[HarmonyPrefix]
		private static bool GetCategory(ref string __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = Database.MenusList[IndexToRead[__instance]].Category.ToString();

			return false;
#else
			return true;
#endif
		}
		[HarmonyPatch(typeof(MenuDataBase), "GetInfoText")]
		[HarmonyPrefix]
		private static bool GetInfoText(ref string __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = Database.MenusList[IndexToRead[__instance]].Description;

			return false;
#else
			return true;
#endif
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetMenuName")]
		[HarmonyPrefix]
		private static bool GetMenuName(ref string __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = Database.MenusList[IndexToRead[__instance]].Name;

			return false;
#else
			return true;
#endif
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetItemInfoText")]
		[HarmonyPrefix]
		private static bool GetItemInfoText(ref string __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = Database.MenusList[IndexToRead[__instance]].Description;

			return false;
#else
			return true;
#endif
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetCategoryMpnText")]
		[HarmonyPrefix]
		private static bool GetCategoryMpnText(ref string __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = Database.MenusList[IndexToRead[__instance]].Category.ToString();

			return false;
#else
			return true;
#endif
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetCategoryMpn")]
		[HarmonyPrefix]
		private static bool GetCategoryMpn(ref int __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = (int)Database.MenusList[IndexToRead[__instance]].Category;

			return false;
#else
			return true;
#endif
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetColorSetMpn")]
		[HarmonyPrefix]
		private static bool GetColorSetMpn(ref int __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = (int)Database.MenusList[IndexToRead[__instance]].ColorSetMPN;

			return false;
#else
			return true;
#endif
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetMenuNameInColorSet")]
		[HarmonyPrefix]
		private static bool GetMenuNameInColorSet(ref string __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = Database.MenusList[IndexToRead[__instance]].ColorSetMenu;

			return false;
#else
			return true;
#endif
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetMultiColorId")]
		[HarmonyPrefix]
		private static bool GetMultiColorId(ref int __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = (int)Database.MenusList[IndexToRead[__instance]].MultiColorID;

			return false;
#else
			return true;
#endif
		}

		[HarmonyPatch(typeof(MenuDataBase), "GetIconS")]
		[HarmonyPrefix]
		private static bool GetIconS(ref string __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = Database.MenusList[IndexToRead[__instance]].Icon;

			return false;
#else
			return true;
#endif
		}
		//Not Implemented... Same deal, and I've yet to see it do anything. Will eventually implement.
		[HarmonyPatch(typeof(MenuDataBase), "GetSaveItem")]
		[HarmonyPrefix]
		private static bool GetSaveItem(ref string __result)
		{
#if !OnlyCompare
			__result = "";

			return false;
#else
			return true;
#endif
		}
		[HarmonyPatch(typeof(MenuDataBase), "GetBoDelOnly")]
		[HarmonyPrefix]
		private static bool GetBoDelOnly(ref bool __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = Database.MenusList[IndexToRead[__instance]].DelMenu;

			return false;
#else
			return true;
#endif
		}
		[HarmonyPatch(typeof(MenuDataBase), "GetPriority")]
		[HarmonyPrefix]
		private static bool GetPriority(ref float __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = Database.MenusList[IndexToRead[__instance]].Priority;

			return false;
#else
			return true;
#endif
		}
		[HarmonyPatch(typeof(MenuDataBase), "GetIsMan")]
		[HarmonyPrefix]
		private static bool GetIsMan(ref bool __result, ref MenuDataBase __instance)
		{
#if !OnlyCompare
			__result = Database.MenusList[IndexToRead[__instance]].ManMenu;

			return false;
#else
			return true;
#endif
		}
		//Not Implemented... I don't even think this does anything but return false though...
		[HarmonyPatch(typeof(MenuDataBase), "GetIsCollabo")]
		[HarmonyPrefix]
		private static bool GetIsCollabo(ref bool __result)
		{
#if !OnlyCompare
			__result = false;

			return false;
#else
			return true;
#endif
		}
	}
}
