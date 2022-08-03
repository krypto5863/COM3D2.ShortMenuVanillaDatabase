using CM3D2.Toolkit.Guest4168Branch.MultiArcLoader;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShortMenuVanillaDatabase
{
	public class MenuDatabaseReplacement
	{
		public Dictionary<int, CacheFile.MenuStub> MenusList { get; private set; }

		public bool Done { get; private set; }

		public int Index { get; private set; }

		private ArcCompare compare = new ArcCompare();

		public MenuDatabaseReplacement()
		{
			MenusList = new Dictionary<int, CacheFile.MenuStub>();
		}

		public IEnumerator StartLoading()
		{
			if (Done)
			{
				yield break;
			}

			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			var cachePath = BepInEx.Paths.CachePath + "\\ShortMenuVanillaDatabase.json";
			CacheFile cachedFile = null;
			Dictionary<string, DateTime> CurrentlyLoadedAndDatedArcs = new Dictionary<string, DateTime>();

			//Cancel token is setup in case I need it in the future but the error handling we currently have should be more than enough.
			var cts = new CancellationTokenSource();
			Task LoaderTask = Task.Factory.StartNew(new Action(() =>
			{
				//The following simply decides what is to be loaded.
				List<string> PathsToLoad = new List<string>()
					{
						$"{BepInEx.Paths.GameRootPath}\\GameData"
					};
				if (!String.IsNullOrEmpty(GameMain.Instance.CMSystem.CM3D2Path))
				{
					PathsToLoad.Add(GameMain.Instance.CMSystem.CM3D2Path + "\\GameData");
				}
				if (Directory.Exists($"{BepInEx.Paths.GameRootPath}\\GameData_20"))
				{
					PathsToLoad.Add($"{BepInEx.Paths.GameRootPath}\\GameData_20");
				}

				//This loop handles the listing of arcs within the above directories.
				foreach (string s in PathsToLoad)
				{
					//Gets all arc files in the directories that match our filters..
					var ArcFilesLoaded = Directory.GetFiles(s)
					.Where(t =>
						(
						Path.GetFileName(t).ToLower().StartsWith("menu")
						|| Path.GetFileName(t).ToLower().StartsWith("parts")
						)
						&& t.EndsWith(".arc", StringComparison.OrdinalIgnoreCase)
					)
					.ToList();

					//Gets the write time for each arc file to track any modifications.
					foreach (string arc in ArcFilesLoaded)
					{
						CurrentlyLoadedAndDatedArcs[arc] = File.GetLastWriteTimeUtc(arc);
					}
				}

				Main.logger.LogDebug($"We found {CurrentlyLoadedAndDatedArcs.Count} that fit our specifications.");

				//Loads all our arc files that we've observed are currently available.
				MultiArcLoader arcFileExplorer = new MultiArcLoader(CurrentlyLoadedAndDatedArcs.Keys.ToArray(), Environment.ProcessorCount, MultiArcLoader.LoadMethod.Single, true, true, MultiArcLoader.Exclude.Voice | MultiArcLoader.Exclude.BG | MultiArcLoader.Exclude.CSV | MultiArcLoader.Exclude.Motion | MultiArcLoader.Exclude.Sound);

				//Loads our cache file.
				if (File.Exists(cachePath))
				{
					try
					{
						string mconfig = (File.ReadAllText(cachePath));
						cachedFile = JsonConvert.DeserializeObject<CacheFile>(mconfig);
					}
					catch (Exception e)
					{
						Main.logger.LogError("Ran into a catastrophic error while trying to read the MenuDatabaseCache. We will attempt to delete the cache and rebuild it...");
						Main.logger.LogError($"{e.Message}\n\n{e.StackTrace}");

						File.Delete(cachePath);
						cachedFile = null;

						throw e;
					}

					Main.logger.LogDebug($"Cache was read, it contains {cachedFile.MenusList.Count()} entries");
					Main.logger.LogDebug($"We extrapolated {cachedFile.CachedLoadedAndDatedArcs.Count} arcs were loaded from cache...");

					List<string> ArcsToReload = new List<string>();

					//These are what's currently loaded in this run and compares it to what's not been loaded or what menu files have been loaded from
					var ArcsToCheck = CurrentlyLoadedAndDatedArcs
					.Keys
					.Where(k => cachedFile.MenusList.Select(m => m.SourceArc).Contains(k) || !cachedFile.CachedLoadedAndDatedArcs.ContainsKey(k));

					//This checks each currently loaded arc against what's been loaded in previous runs and checks to see if anything has changed.
					foreach (string Arc in ArcsToCheck)
					{
						//Has this arc not been cached? Has this arc been modified?
						if (!cachedFile.CachedLoadedAndDatedArcs.ContainsKey(Arc) || CurrentlyLoadedAndDatedArcs[Arc] != cachedFile.CachedLoadedAndDatedArcs[Arc])
						{
							//Was arc modified?
							if (cachedFile.CachedLoadedAndDatedArcs.ContainsKey(Arc) && CurrentlyLoadedAndDatedArcs.ContainsKey(Arc))
							{
								//Arc modified!
								Main.logger.LogDebug($"{Arc} was modified and requires reloading! Entries from this arc will be removed and remade!\nTime In Cache:{cachedFile.CachedLoadedAndDatedArcs[Arc]}\nTime In Folder{CurrentlyLoadedAndDatedArcs[Arc]}");
							}

							//Regardless of modified or new, it needs to be re/loaded and re/cached.
							ArcsToReload.Add(Arc);

							//If it was modified, it will just remove any menu cached from it, allowing for a clean recache.
							cachedFile.MenusList.RemoveWhere(menu => menu.SourceArc.Equals(Arc, StringComparison.OrdinalIgnoreCase));
						}
					}

					//This loop checks if the arcs in cache are still valid and cleans the cache if not.
					foreach (var arc in cachedFile.CachedLoadedAndDatedArcs.ToList())
					{
						//Is the cached arc not part of the current paths?
						if ((!arc.Key.Contains(BepInEx.Paths.GameRootPath) && !arc.Key.Contains(GameMain.Instance.CMSystem.CM3D2Path)) || !File.Exists(arc.Key))
						{
							//Remove the arc from our cached list.
							cachedFile.CachedLoadedAndDatedArcs.Remove(arc.Key);
							//Remove all menus loaded from this arc.
							cachedFile.MenusList.RemoveWhere(menu => menu.SourceArc.Equals(arc.Key, StringComparison.OrdinalIgnoreCase));
						}
					}

					Main.logger.LogDebug($"Done checking over files... Updating held list of menus with successfully cached files...");

					foreach (CacheFile.MenuStub f in cachedFile.MenusList)
					{
						MenusList[Index++] = f;
					}

					if (ArcsToReload.Count > 0)
					{
						arcFileExplorer = new MultiArcLoader(ArcsToReload.ToArray(), Environment.ProcessorCount, MultiArcLoader.LoadMethod.Single, true, true, MultiArcLoader.Exclude.Voice | MultiArcLoader.Exclude.BG | MultiArcLoader.Exclude.CSV | MultiArcLoader.Exclude.Motion | MultiArcLoader.Exclude.Sound);

						arcFileExplorer.LoadArcs();

						Main.logger.LogDebug($"Done, loading refreshed files...");
					}
					else
					{
						arcFileExplorer = null;

						Main.logger.LogDebug($"No arc files needed updating...");
					}
				}
				else//No cache file is found. Load all.
				{
					arcFileExplorer.LoadArcs();
				}

				if (arcFileExplorer != null && arcFileExplorer.arc.Files.Count > 0)
				{
					Main.logger.LogInfo($"Arcs read in {stopwatch.Elapsed}");

					var filesInArc = new HashSet<CM3D2.Toolkit.Guest4168Branch.Arc.Entry.ArcFileEntry>(arcFileExplorer.arc.Files.Values.Where(val => val.Name.ToLower().EndsWith(".menu") && !val.Name.ToLower().Contains("_mekure_") && !val.Name.ToLower().Contains("_zurashi_")));

					foreach (CM3D2.Toolkit.Guest4168Branch.Arc.Entry.ArcFileEntry fileInArc in filesInArc.OrderBy(f => arcFileExplorer.GetContentsArcFilePath(f)))
					{
						var arcFile = arcFileExplorer.GetContentsArcFilePath(fileInArc);

						var data = fileInArc.Pointer.Decompress();

						if (!ReadInternalMenuFile(fileInArc.Name, arcFile, data.Data))
						{
							Main.logger.LogError($"Failed to load {fileInArc.Name} from {arcFile}.");
						}
					}
				}

				Main.logger.LogInfo($"Menu file stubs were done loading at {stopwatch.Elapsed}");

				stopwatch = null;

				arcFileExplorer = null;
			}), cts.Token);

			while (!LoaderTask.IsCompleted)
			{
				yield return null;
			}

			if (LoaderTask.IsFaulted)
			{
				Main.logger.LogError(LoaderTask.Exception + "\n\nGonna try running the method again...");

				Main.@this.StartCoroutine(StartLoading());

				yield break;
			}

			Done = true;
			LoaderTask.Dispose();

			CacheFile cache = new CacheFile()
			{
				MenusList = new HashSet<CacheFile.MenuStub>(this.MenusList.Values),
				CachedLoadedAndDatedArcs = CurrentlyLoadedAndDatedArcs
			};

			File.WriteAllText(cachePath, JsonConvert.SerializeObject(cache, Formatting.Indented));
		}

		private bool ReadInternalMenuFile(string f_strMenuFileName, string sourceArc, byte[] data)
		{
			MemoryStream DataStream;

			try
			{
				DataStream = new MemoryStream(data);
			}
			catch (Exception ex)
			{
				Main.logger.LogError(string.Concat(new string[]
				{
						"The following menu file could not be read! (メニューファイルがが読み込めませんでした。): ",
						f_strMenuFileName,
						"\n\n",
						ex.Message,
						"\n",
						ex.StackTrace
				}));

				return false;
			}

			string text6 = string.Empty;
			string text7 = string.Empty;
			string path = "";

			CacheFile.MenuStub cacheEntry = new CacheFile.MenuStub();

			cacheEntry.FileName = f_strMenuFileName;

			try
			{
				BinaryReader binaryReader = new BinaryReader(DataStream, Encoding.UTF8);
				string text = binaryReader.ReadString();

				if (!text.Equals("CM3D2_MENU"))
				{
					Main.logger.LogError("ProcScriptBin (例外 : ヘッダーファイルが不正です。) The header indicates a file type that is not a menu file!" + text + " @ " + f_strMenuFileName);

					return false;
				}

				cacheEntry.Version = binaryReader.ReadInt32();
				path = binaryReader.ReadString();
				cacheEntry.SourceArc = sourceArc;

				cacheEntry.PathInMenu = path;

				binaryReader.ReadString();
				binaryReader.ReadString();
				binaryReader.ReadString();
				binaryReader.ReadInt32();

				while (true)
				{
					int num4 = binaryReader.ReadByte();
					text7 = text6;
					text6 = string.Empty;
					if (num4 == 0)
					{
						break;
					}
					for (int i = 0; i < num4; i++)
					{
						text6 = text6 + "\"" + binaryReader.ReadString() + "\" ";
					}
					if (!(text6 == string.Empty))
					{
						string stringCom = UTY.GetStringCom(text6);
						string[] stringList = UTY.GetStringList(text6);
						if (stringCom.Equals("name"))
						{
							if (stringList.Length > 1)
							{
								string text8 = stringList[1];
								string text9 = string.Empty;
								string arg = string.Empty;
								int j = 0;
								while (j < text8.Length && text8[j] != '\u3000' && text8[j] != ' ')
								{
									text9 += text8[j];
									j++;
								}
								while (j < text8.Length)
								{
									arg += text8[j];
									j++;
								}
								cacheEntry.Name = text9;
							}
							else
							{
								Main.logger.LogWarning("Menu file has no name and an empty name will be used instead." + " @ " + f_strMenuFileName);

								cacheEntry.Name = "";
							}
						}
						else if (stringCom.Equals("setumei"))
						{
							if (stringList.Length > 1)
							{
								cacheEntry.Description = stringList[1];
								cacheEntry.Description = cacheEntry.Description.Replace("《改行》", "\n");
							}
							else
							{
								Main.logger.LogWarning("Menu file has no description (setumei) and an empty description will be used instead." + " @ " + f_strMenuFileName);

								cacheEntry.Description = "";
							}
						}
						else if (stringCom.Equals("category"))
						{
							if (stringList.Length > 1)
							{
								string strCateName = stringList[1].ToLower();
								try
								{
									cacheEntry.Category = (MPN)Enum.Parse(typeof(MPN), strCateName);
								}
								catch
								{
									cacheEntry.Category = MPN.null_mpn;
								}
							}
							else
							{
								Main.logger.LogWarning("The following menu file has a category parent with no category: " + f_strMenuFileName);
								return false;
							}
						}
						else if (stringCom.Equals("color_set"))
						{
							if (stringList.Length > 1)
							{
								try
								{
									cacheEntry.ColorSetMPN = (MPN)Enum.Parse(typeof(MPN), stringList[1].ToLower());
								}
								catch
								{
									Main.logger.LogWarning("There is no category called(カテゴリがありません。): " + stringList[1].ToLower() + " @ " + f_strMenuFileName);

									return false;
								}
								if (stringList.Length >= 3)
								{
									cacheEntry.ColorSetMenu = stringList[2].ToLower();
								}
							}
							else
							{
								Main.logger.LogWarning("A color_set entry exists but is otherwise empty" + " @ " + f_strMenuFileName);
							}
						}
						else if (stringCom.Equals("tex") || stringCom.Equals("テクスチャ変更"))
						{
							MaidParts.PARTS_COLOR pcMultiColorID = MaidParts.PARTS_COLOR.NONE;
							if (stringList.Length == 6)
							{
								string text10 = stringList[5];
								try
								{
									pcMultiColorID = (MaidParts.PARTS_COLOR)Enum.Parse(typeof(MaidParts.PARTS_COLOR), text10.ToUpper());
								}
								catch
								{
									Main.logger.LogError("無限色IDがありません。(The following free color ID does not exist: )" + text10 + " @ " + f_strMenuFileName);

									return false;
								}
								cacheEntry.MultiColorID = pcMultiColorID;
							}
						}
						else if (stringCom.Equals("icon") || stringCom.Equals("icons"))
						{
							if (stringList.Length > 1)
							{
								cacheEntry.Icon = stringList[1];
							}
							else
							{
								Main.logger.LogError("The following menu file has an icon entry but no field set: " + f_strMenuFileName);

								return false;
							}
						}
						else if (stringCom.Equals("saveitem"))
						{
							if (stringList.Length > 1)
							{
								string text11 = stringList[1];
								if (String.IsNullOrEmpty(text11))
								{
									Main.logger.LogWarning("SaveItem is either null or empty." + " @ " + f_strMenuFileName);
								}
							}
							else
							{
								Main.logger.LogWarning("A saveitem entry exists with nothing set in the field @ " + f_strMenuFileName);
							}
						}
						else if (stringCom.Equals("unsetitem"))
						{
							cacheEntry.DelMenu = true;
						}
						else if (stringCom.Equals("priority"))
						{
							if (stringList.Length > 1)
							{
								cacheEntry.Priority = float.Parse(stringList[1]);
							}
							else
							{
								Main.logger.LogError("The following menu file has a priority entry but no field set. A default value of 10000 will be used: " + f_strMenuFileName);

								cacheEntry.Priority = 10000f;
							}
						}
						else if (stringCom.Equals("メニューフォルダ"))
						{
							if (stringList.Length > 1)
							{
								if (stringList[1].ToLower().Equals("man"))
								{
									cacheEntry.ManMenu = true;
								}
							}
							else
							{
								Main.logger.LogError("A menu with a menu folder setting (メニューフォルダ) has an entry but no field set: " + f_strMenuFileName);

								return false;
							}
						}
					}
				}

				var ExistingMenu = MenusList.Where(t => String.Equals(t.Value.FileName, cacheEntry.FileName, StringComparison.OrdinalIgnoreCase)).ToList();

				if (ExistingMenu.Count() > 0)
				{
					try
					{
						var firstEntry = ExistingMenu.First();

						if (compare.Compare(sourceArc, firstEntry.Value.SourceArc) >= 0)
						{
							MenusList[firstEntry.Key] = cacheEntry;

							ExistingMenu.Remove(firstEntry);

							if (ExistingMenu.Count() > 0)
							{
								try
								{
									foreach (var key in ExistingMenu)
									{
										MenusList.Remove(key.Key);
									}
								}
								catch
								{
									//Better to just keep going in the case of a failure like this.
									Main.logger.LogWarning("Failed to remove some old menus from the cache! This many cause duplicates... You may want to delete your cache!");
								}
							}
						}
					}
					catch
					{
						MenusList[Index++] = cacheEntry;
					}
				}
				else
				{
					MenusList[Index++] = cacheEntry;
				}
			}
			catch
			{
				Main.logger.LogError("Encountered some error while reading a vanilla menu file...");

				return false;
			}

			return true;
		}

		public class ArcCompare : IComparer<string>
		{
			public int Compare(string path1, string path2)
			{
				if (path1.Contains(BepInEx.Paths.GameRootPath + "\\GameData\\") ^ path2.Contains(BepInEx.Paths.GameRootPath + "\\GameData\\"))
				{
					if (path1.Contains(BepInEx.Paths.GameRootPath + "\\GameData\\"))
					{
						return 1;
					}
					else
					{
						return -1;
					}
				}
				else if (path1.Contains(BepInEx.Paths.GameRootPath + "\\GameData_20\\") ^ path2.Contains(BepInEx.Paths.GameRootPath + "\\GameData_20\\"))
				{
					if (path1.Contains(BepInEx.Paths.GameRootPath + "\\GameData_20\\"))
					{
						return 1;
					}
					else
					{
						return -1;
					}
				}
				else if (path1.Contains(GameMain.Instance.CMSystem.CM3D2Path + "\\GameData\\") ^ path2.Contains(GameMain.Instance.CMSystem.CM3D2Path + "\\GameData\\"))
				{
					if (path1.Contains(GameMain.Instance.CMSystem.CM3D2Path + "\\GameData\\"))
					{
						return 1;
					}
					else
					{
						return -1;
					}
				}
				else
				{
					return String.Compare(path1, path2);
				}
			}
		}
	}
}