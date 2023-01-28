using BepInEx;
using System;
using System.Collections.Generic;

namespace ShortMenuVanillaDatabase
{
	public class ArcCompare : IComparer<string>
	{
		public static ArcCompare Instance = new ArcCompare();

		public int Compare(string path1, string path2)
		{
			if (path1 is null)
			{
				throw new ArgumentNullException(nameof(path1));
			}
			if (path2 is null)
			{
				throw new ArgumentNullException(nameof(path2));
			}

			if (path1.Contains(Paths.GameRootPath + "\\GameData\\") ^ path2.Contains(Paths.GameRootPath + "\\GameData\\"))
			{
				if (path1.Contains(Paths.GameRootPath + "\\GameData\\"))
				{
					return 1;
				}

				return -1;
			}

			if (path1.Contains(Paths.GameRootPath + "\\GameData_20\\") ^ path2.Contains(Paths.GameRootPath + "\\GameData_20\\"))
			{
				if (path1.Contains(Paths.GameRootPath + "\\GameData_20\\"))
				{
					return 1;
				}

				return -1;
			}

			if (path1.Contains(GameMain.Instance.CMSystem.CM3D2Path + "\\GameData\\") ^ path2.Contains(GameMain.Instance.CMSystem.CM3D2Path + "\\GameData\\"))
			{
				if (path1.Contains(GameMain.Instance.CMSystem.CM3D2Path + "\\GameData\\"))
				{
					return 1;
				}

				return -1;
			}

			return string.CompareOrdinal(path1, path2);
		}
	}
}