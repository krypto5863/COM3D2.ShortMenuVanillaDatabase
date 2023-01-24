using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using CM3D2.Toolkit.Guest4168Branch.Arc.Entry;
using CM3D2.Toolkit.Guest4168Branch.Logging;
using CM3D2.Toolkit.Guest4168Branch.MultiArcLoader;
using Newtonsoft.Json;

namespace ShortMenuVanillaDatabase
{
	public class MenuDatabaseReplacement
	{
		public CacheFile.MenuStub[] MenusList { get; private set; }

		public bool Done { get; private set; }

		private readonly ArcCompare _compare = new ArcCompare();

		private class LogToolkit : ILogger
		{
			public string Name => throw new NotImplementedException();

			public void Debug(string message, params object[] args)
			{
				//Main.PLogger.LogDebug(message);
			}

			public void Error(string message, params object[] args)
			{
				//Main.PLogger.LogError(message);
			}

			public void Fatal(string message, params object[] args)
			{
				//Main.PLogger.LogFatal(message);
			}

			public void Info(string message, params object[] args)
			{
				//Main.PLogger.LogInfo(message);
			}

			public void Trace(string message, params object[] args)
			{
				//Main.PLogger.LogError(message);
			}

			public void Warn(string message, params object[] args)
			{
				//Main.PLogger.LogWarning(message);
			}
		}


		public IEnumerator StartLoading()
		{
			if (Done)
			{
				yield break;
			}

			var stopwatch = new Stopwatch();
			stopwatch.Start();

			var tempList = new HashSet<CacheFile.MenuStub>();

			var cachePath = Paths.CachePath + "\\ShortMenuVanillaDatabase.json";
			CacheFile cachedFile;
			var currentlyLoadedAndDatedArcs = new Dictionary<string, DateTime>();

			//Cancel token is setup in case I need it in the future but the error handling we currently have should be more than enough.
			var cts = new CancellationTokenSource();
			var loaderTask = Task.Factory.StartNew(() =>
			{
				//The following simply decides what is to be loaded.
				var pathsToLoad = new List<string>
				{
					$"{Paths.GameRootPath}\\GameData"
				};

				if (!string.IsNullOrEmpty(GameMain.Instance.CMSystem.CM3D2Path))
				{
					pathsToLoad.Add(GameMain.Instance.CMSystem.CM3D2Path + "\\GameData");
				}
				if (Directory.Exists($"{Paths.GameRootPath}\\GameData_20"))
				{
					pathsToLoad.Add($"{Paths.GameRootPath}\\GameData_20");
				}

				//This loop handles the listing of arcs within the above directories.
				foreach (var s in pathsToLoad)
				{
					//Gets all arc files in the directories that match our filters..
					var arcFilesLoaded = Directory.GetFiles(s)
						.Where(t =>
							(
								Path.GetFileName(t).StartsWith("menu", StringComparison.OrdinalIgnoreCase)
								|| Path.GetFileName(t).StartsWith("parts", StringComparison.OrdinalIgnoreCase)
							)
							&& t.EndsWith(".arc", StringComparison.OrdinalIgnoreCase)
						)
						.ToArray();

					//Gets the write time for each arc file to track any modifications.
					foreach (var arc in arcFilesLoaded)
					{
						currentlyLoadedAndDatedArcs[arc] = File.GetLastWriteTimeUtc(arc);
					}
				}

				Main.PLogger.LogDebug($"We found {currentlyLoadedAndDatedArcs.Count} that fit our specifications @ {stopwatch.Elapsed}");

				//PLogger for logging.
				var logger = new LogToolkit();

				//Loads all our arc files that we've observed are currently available.
				var arcFileExplorer = new MultiArcLoader(currentlyLoadedAndDatedArcs.Keys.ToArray(), Environment.ProcessorCount, MultiArcLoader.LoadMethod.Single, true, true, MultiArcLoader.Exclude.Voice | MultiArcLoader.Exclude.BG | MultiArcLoader.Exclude.CSV | MultiArcLoader.Exclude.Motion | MultiArcLoader.Exclude.Sound, logger);

				//Loads our cache file.
				if (File.Exists(cachePath))
				{
					try
					{
						var mConfig = (File.ReadAllText(cachePath));
						cachedFile = JsonConvert.DeserializeObject<CacheFile>(mConfig) ?? new CacheFile();

						Main.PLogger.LogDebug($"Cache loaded @ {stopwatch.Elapsed}"
						                      + $"\nIt contains {cachedFile.MenusList.Count} entries."
						                      + $"\nWe extrapolated {cachedFile.CachedLoadedAndDatedArcs.Count} arcs were loaded from cache...");
					}
					catch (Exception e)
					{
						Main.PLogger.LogError("Ran into a catastrophic error while trying to read the MenuDatabaseCache. We will attempt to delete the cache and rebuild it...");
						Main.PLogger.LogError($"{e.Message}\n\n{e.StackTrace}");

						File.Delete(cachePath);
						cachedFile = null;

						throw;
					}

					var arcsToReload = new List<string>();

					//These are what's currently loaded in this run and compares it to what's not been loaded or what menu files have been loaded from
					var arcsToCheck = currentlyLoadedAndDatedArcs
						.Keys
						.Where(k => cachedFile.MenusList.Select(m => m.SourceArc).Contains(k) || !cachedFile.CachedLoadedAndDatedArcs.ContainsKey(k));

					//This checks each currently loaded arc against what's been loaded in previous runs and checks to see if anything has changed.
					foreach (var arc in arcsToCheck)
					{
						//Has this arc not been cached? Has this arc been modified?
						if (cachedFile.CachedLoadedAndDatedArcs.ContainsKey(arc) && currentlyLoadedAndDatedArcs[arc] == cachedFile.CachedLoadedAndDatedArcs[arc])
						{
							continue;
						}

						//Was arc modified?
						if (cachedFile.CachedLoadedAndDatedArcs.ContainsKey(arc) && currentlyLoadedAndDatedArcs.ContainsKey(arc))
						{
							//Arc modified!
							Main.PLogger.LogDebug($"{arc} was modified and requires reloading! Entries from this arc will be removed and remade!\nTime In Cache:{cachedFile.CachedLoadedAndDatedArcs[arc]}\nTime In Folder{currentlyLoadedAndDatedArcs[arc]}");
						}

						//Regardless of modified or new, it needs to be re/loaded and re/cached.
						arcsToReload.Add(arc);

						//If it was modified, it will just remove any menu cached from it, allowing for a clean recache.
						cachedFile.MenusList.RemoveAll(menu => menu.SourceArc.Equals(arc, StringComparison.OrdinalIgnoreCase));
					}

					//This loop checks if the arcs in cache are still valid and cleans the cache if not.
					foreach (var arc in cachedFile.CachedLoadedAndDatedArcs.ToList())
					{
						//Is the cached arc not part of the current paths?
						if ((arc.Key.Contains(Paths.GameRootPath) || arc.Key.Contains(GameMain.Instance.CMSystem.CM3D2Path)) && File.Exists(arc.Key))
						{
							continue;
						}

						//Remove the arc from our cached list.
						cachedFile.CachedLoadedAndDatedArcs.Remove(arc.Key);
						//Remove all menus loaded from this arc.
						cachedFile.MenusList.RemoveAll(menu => menu.SourceArc.Equals(arc.Key, StringComparison.OrdinalIgnoreCase));
					}

					Main.PLogger.LogDebug($"Done cleaning cache and amending @ {stopwatch.Elapsed}...");

					foreach (var menu in cachedFile.MenusList)
					{
						tempList.Add(menu);
					}

					if (arcsToReload.Count > 0)
					{

						Main.PLogger.LogDebug("Building Arc Loader.");

						arcFileExplorer = new MultiArcLoader(arcsToReload.ToArray(), Environment.ProcessorCount, MultiArcLoader.LoadMethod.Single, true, true, MultiArcLoader.Exclude.Voice | MultiArcLoader.Exclude.BG | MultiArcLoader.Exclude.CSV | MultiArcLoader.Exclude.Motion | MultiArcLoader.Exclude.Sound, logger);

						Main.PLogger.LogDebug("Loading Arcs...");

						arcFileExplorer.LoadArcs();

						Main.PLogger.LogDebug("Done, loading refreshed files...");
					}
					else
					{
						arcFileExplorer = null;

						Main.PLogger.LogDebug("No arc files needed updating...");
					}
				}
				else//No cache file is found. Load all.
				{
					arcFileExplorer.LoadArcs();
				}

				if (arcFileExplorer != null && arcFileExplorer.arc.Files.Count > 0)
				{
					Main.PLogger.LogInfo($"Arcs read @ {stopwatch.Elapsed}");

					var filesInArc = new HashSet<ArcFileEntry>(arcFileExplorer.arc.Files.Values.Where(val =>
						val.Name.ToLower()
							.EndsWith(".menu") &&
						!val.Name.ToLower()
							.Contains("_mekure_") &&
						!val.Name.ToLower()
							.Contains("_zurashi_")));

					foreach (var fileInArc in filesInArc.OrderBy(f => arcFileExplorer.GetContentsArcFilePath(f)))
					{
						var arcFile = arcFileExplorer.GetContentsArcFilePath(fileInArc);
						var data = fileInArc.Pointer.Decompress();

						if (!ReadInternalMenuFile(fileInArc.Name, arcFile, data.Data, ref tempList))
						{
							Main.PLogger.LogError($"Failed to load {fileInArc.Name} from {arcFile}.");
						}
					}

					Main.PLogger.LogInfo($"Processed all menus @ {stopwatch.Elapsed}");
				}

				//Completely done.
				Main.PLogger.LogInfo($"SMVD thread is done @ {stopwatch.Elapsed}");

				stopwatch = null;

				arcFileExplorer = null;
			}, cts.Token);

			while (!loaderTask.IsCompleted)
			{
				yield return null;
			}

			if (loaderTask.IsFaulted)
			{
				Main.PLogger.LogError(loaderTask.Exception?.InnerException + "\n\nGonna try running the method again...");

				Main.This.StartCoroutine(StartLoading());

				yield break;
			}

			MenusList = tempList.ToArray();

			Done = true;
			loaderTask.Dispose();

			var cache = new CacheFile
			{
				MenusList = tempList.ToList(),
				CachedLoadedAndDatedArcs = currentlyLoadedAndDatedArcs
			};

			File.WriteAllText(cachePath, JsonConvert.SerializeObject(cache, Formatting.Indented));
		}

		private bool ReadInternalMenuFile(string fStrMenuFileName, string sourceArc, byte[] data, ref HashSet<CacheFile.MenuStub> currentCollection)
		{
			MemoryStream dataStream;

			try
			{
				dataStream = new MemoryStream(data);
			}
			catch (Exception ex)
			{
				Main.PLogger.LogError(string.Concat("The following menu file could not be read! (メニューファイルがが読み込めませんでした。): ", fStrMenuFileName, "\n\n", ex.Message, "\n", ex.StackTrace));

				return false;
			}

			var cacheEntry = new CacheFile.MenuStub(fStrMenuFileName);

			try
			{
				var binaryReader = new BinaryReader(dataStream, Encoding.UTF8);
				var text = binaryReader.ReadString();

				if (!text.Equals("CM3D2_MENU"))
				{
					Main.PLogger.LogError("ProcScriptBin (例外 : ヘッダーファイルが不正です。) The header indicates a file type that is not a menu file!" + text + " @ " + fStrMenuFileName);

					return false;
				}

				cacheEntry.Version = binaryReader.ReadInt32();
				var path = binaryReader.ReadString();
				cacheEntry.SourceArc = sourceArc;

				cacheEntry.PathInMenu = path;

				binaryReader.ReadString();
				binaryReader.ReadString();
				binaryReader.ReadString();
				binaryReader.ReadInt32();

				while (true)
				{
					int num4 = binaryReader.ReadByte();
					var text6 = string.Empty;
					if (num4 == 0)
					{
						break;
					}
					for (var i = 0; i < num4; i++)
					{
						text6 = text6 + "\"" + binaryReader.ReadString() + "\" ";
					}
					if (text6 != string.Empty)
					{
						var stringCom = UTY.GetStringCom(text6);
						var stringList = UTY.GetStringList(text6);
						switch (stringCom)
						{
							case "name" when stringList.Length > 1:
							{
								var text8 = stringList[1];
								var text9 = string.Empty;
								var j = 0;
								while (j < text8.Length && text8[j] != '\u3000' && text8[j] != ' ')
								{
									text9 += text8[j];
									j++;
								}
								while (j < text8.Length)
								{
									j++;
								}
								cacheEntry.Name = text9;
								break;
							}
							case "name":
								Main.PLogger.LogWarning("Menu file has no name and an empty name will be used instead." + " @ " + fStrMenuFileName);

								cacheEntry.Name = "";
								break;
							case "setumei" when stringList.Length > 1:
								cacheEntry.Description = stringList[1];
								cacheEntry.Description = cacheEntry.Description.Replace("《改行》", "\n");
								break;
							case "setumei":
								Main.PLogger.LogWarning("Menu file has no description (setumei) and an empty description will be used instead." + " @ " + fStrMenuFileName);

								cacheEntry.Description = "";
								break;
							case "category" when stringList.Length > 1:
							{
								var strCatName = stringList[1].ToLower();
								try
								{
									cacheEntry.Category = (MPN)Enum.Parse(typeof(MPN), strCatName);
								}
								catch
								{
									cacheEntry.Category = MPN.null_mpn;
								}

								break;
							}
							case "category":
								Main.PLogger.LogWarning("The following menu file has a category parent with no category: " + fStrMenuFileName);
								return false;
							case "color_set" when stringList.Length > 1:
							{
								try
								{
									cacheEntry.ColorSetMpn = (MPN)Enum.Parse(typeof(MPN), stringList[1].ToLower());
								}
								catch
								{
									Main.PLogger.LogWarning("There is no category called(カテゴリがありません。): " + stringList[1].ToLower() + " @ " + fStrMenuFileName);

									return false;
								}
								if (stringList.Length >= 3)
								{
									cacheEntry.ColorSetMenu = stringList[2].ToLower();
								}

								break;
							}
							case "color_set":
								Main.PLogger.LogWarning("A color_set entry exists but is otherwise empty" + " @ " + fStrMenuFileName);
								break;
							case "tex":
							case "テクスチャ変更":
							{
								if (stringList.Length == 6)
								{
									var text10 = stringList[5];
									MaidParts.PARTS_COLOR pcMultiColorId;
									try
									{
										pcMultiColorId = (MaidParts.PARTS_COLOR)Enum.Parse(typeof(MaidParts.PARTS_COLOR), text10.ToUpper());
									}
									catch
									{
										Main.PLogger.LogError("無限色IDがありません。(The following free color ID does not exist: )" + text10 + " @ " + fStrMenuFileName);

										return false;
									}
									cacheEntry.MultiColorId = pcMultiColorId;
								}

								break;
							}
							case "icon":
							case "icons":
							{
								if (stringList.Length > 1)
								{
									cacheEntry.Icon = stringList[1];
								}
								else
								{
									Main.PLogger.LogError("The following menu file has an icon entry but no field set: " + fStrMenuFileName);

									return false;
								}

								break;
							}
							case "saveitem" when stringList.Length > 1:
							{
								var text11 = stringList[1];
								if (string.IsNullOrEmpty(text11))
								{
									Main.PLogger.LogWarning("SaveItem is either null or empty." + " @ " + fStrMenuFileName);
								}

								break;
							}
							case "saveitem":
								Main.PLogger.LogWarning("A saveitem entry exists with nothing set in the field @ " + fStrMenuFileName);
								break;
							case "unsetitem":
								cacheEntry.DelMenu = true;
								break;
							case "priority" when stringList.Length > 1:
								cacheEntry.Priority = float.Parse(stringList[1]);
								break;
							case "priority":
								Main.PLogger.LogError("The following menu file has a priority entry but no field set. A default value of 10000 will be used: " + fStrMenuFileName);

								cacheEntry.Priority = 10000f;
								break;
							case "メニューフォルダ" when stringList.Length > 1:
							{
								if (stringList[1].ToLower().Equals("man"))
								{
									cacheEntry.ManMenu = true;
								}

								break;
							}
							case "メニューフォルダ":
								Main.PLogger.LogError("A menu with a menu folder setting (メニューフォルダ) has an entry but no field set: " + fStrMenuFileName);

								return false;
						}
					}
				}

				var curEnumerable = currentCollection.ToArray();

				var leftoverCount = 0;

				foreach (var curElement in curEnumerable)
				{
					if (!string.Equals(curElement.FileName, cacheEntry.FileName, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					if (_compare.Compare(sourceArc, curElement.SourceArc) != -1)
					{
						currentCollection.Remove(curElement);
					}
					else
					{
						leftoverCount++;
					}
				}

				if (leftoverCount <= 0)
				{
					currentCollection.Add(cacheEntry);
				}
			}
			catch
			{
				Main.PLogger.LogError("Encountered some error while reading a vanilla menu file...");

				return false;
			}

			return true;
		}

		public class ArcCompare : IComparer<string>
		{
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
}