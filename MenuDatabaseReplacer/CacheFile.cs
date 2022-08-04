using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace ShortMenuVanillaDatabase
{
	public class CacheFile
	{
		public Dictionary<string, DateTime> CachedLoadedAndDatedArcs { get; set; }
		public List<CacheFile.MenuStub> MenusList { get; set; }

		public CacheFile()
		{
			CachedLoadedAndDatedArcs = new Dictionary<string, DateTime>();
			MenusList = new List<MenuStub>();
		}

		public class MenuStub
		{
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
			public MPN ColorSetMPN { get; set; }

			public string ColorSetMenu { get; set; }

			[JsonConverter(typeof(StringEnumConverter))]
			public MaidParts.PARTS_COLOR MultiColorID { get; set; }

			public bool DelMenu { get; set; }
			public bool ManMenu { get; set; }
			public float Priority { get; set; }
			public bool LegacyMenu { get; set; }
			public string SourceArc { get; set; }
		}
	}
}