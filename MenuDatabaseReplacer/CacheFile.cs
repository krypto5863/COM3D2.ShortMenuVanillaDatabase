using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace ShortMenuVanillaDatabase
{
	public class CacheFile
	{
		public Dictionary<string, DateTime> CachedLoadedAndDatedArcs;
		public List<MenuStub> MenusList;

		public CacheFile()
		{
			CachedLoadedAndDatedArcs = new Dictionary<string, DateTime>();
			MenusList = new List<MenuStub>();
		}

		public bool RemoveAllTracesOfArc(string arc)
		{
			arc = arc.ToLower();
			var result1 = CachedLoadedAndDatedArcs.Remove(arc);
			var result2 = MenusList.RemoveAll(menu => menu.SourceArc.ToLower().Equals(arc));

			return result1 || result2 > 0;
		}

		public bool ShouldAddMenuFile(string filename, string sourceArc)
		{
			foreach (var curElement in MenusList)
			{
				if (!string.Equals(curElement.FileName, filename, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (ArcCompare.Instance.Compare(sourceArc, curElement.SourceArc) == -1)
				{
					return false;
				}
			}

			return true;
		}

		public bool TryAddMenuFile(MenuStub cacheEntry, string sourceArc)
		{
			var leftoverCount = 0;

			foreach (var curElement in MenusList.ToArray())
			{
				if (!string.Equals(curElement.FileName, cacheEntry.FileName, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (ArcCompare.Instance.Compare(sourceArc, curElement.SourceArc) != -1)
				{
					MenusList.Remove(curElement);
				}
				else
				{
					leftoverCount++;
				}
			}

			if (leftoverCount <= 0)
			{
				MenusList.Add(cacheEntry);
				return true;
			}

			return false;
		}

		public class MenuStub
		{
			public MenuStub(string fileName)
			{
				FileName = fileName;
			}

			public string FileName { get; set; }
			public string PathInMenu { get; set; }
			public ulong Hash { get; set; }
			public string Name { get; set; }
			public int Version { get; set; }
			public string Icon { get; set; }
			public string Description { get; set; }

			[JsonConverter(typeof(StringEnumConverter))]
			public MPN Category { get; set; }

			[JsonConverter(typeof(StringEnumConverter))]
			public MPN ColorSetMpn { get; set; }

			public string ColorSetMenu { get; set; }

			[JsonConverter(typeof(StringEnumConverter))]
			public MaidParts.PARTS_COLOR MultiColorId { get; set; }

			public bool DelMenu { get; set; }
			public bool ManMenu { get; set; }
			public float Priority { get; set; }
			public bool LegacyMenu { get; set; }
			public string SourceArc { get; set; }
		}
	}
}