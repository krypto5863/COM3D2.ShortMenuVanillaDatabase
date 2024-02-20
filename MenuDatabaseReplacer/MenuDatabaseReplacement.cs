using BepInEx;
using CM3D2.Toolkit.Guest4168Branch.MultiArcLoader;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ShortMenuVanillaDatabase
{
	public class MenuDatabaseReplacement
	{
		public List<CacheFile.MenuStub> MenusList => _cachedFile.MenusList;

		public bool Done { get; private set; }

		private readonly string _cachePath = Paths.CachePath + "\\ShortMenuVanillaDatabase.json";
		private static CacheFile _cachedFile;

		public IEnumerator StartLoading()
		{
			if (Done)
			{
				yield break;
			}

			ShortMenuVanillaDatabase.PLogger.LogInfo("Starting Analysis of arc files...");

			var stopwatch = new Stopwatch();
			stopwatch.Start();

			/*
            // var getArcsToLoadTask = new Task<Dictionary<string, DateTime>>(GetAllArcsToLoad);
            //getArcsToLoadTask.RunSynchronously();

            var arcsToLoad = GetAllArcsToLoad();

            var processArcsToLoadWithCache = ContinuationFunction(arcsToLoad);

			ContinuationFunction2(processArcsToLoadWithCache, stopwatch);

            Done = true;

			_cachedFile.CachedLoadedAndDatedArcs = processArcsToLoadWithCache;
			File.WriteAllText(_cachePath, JsonConvert.SerializeObject(_cachedFile, Formatting.Indented))
			*/
			var getArcsToLoadTask = Task.Factory.StartNew(GetAllArcsToLoad);
			var processArcsToLoadWithCache = getArcsToLoadTask.ContinueWith(loadArcs =>
			{
				var loadedArcs = loadArcs.Result;

				//There's no cache file to load. Just load every file then.
				if (File.Exists(_cachePath) == false)
				{
					_cachedFile = new CacheFile();
					return loadedArcs;
				}

				var sCachedFile = File.ReadAllText(_cachePath);
				_cachedFile = JsonConvert.DeserializeObject<CacheFile>(sCachedFile) ?? new CacheFile();
				var arcsToReload = new Dictionary<string, DateTime>();

				if (_cachedFile.CachedLoadedAndDatedArcs is null || _cachedFile.MenusList is null)
				{
					return loadedArcs;
				}

				foreach (var pair in _cachedFile.CachedLoadedAndDatedArcs.ToArray())
				{
					//This arc was removed!
					if (loadedArcs.ContainsKey(pair.Key) == false)
					{
						ShortMenuVanillaDatabase.PLogger.LogDebug($"Removing {pair.Key} as it's missing!");
						_cachedFile.RemoveAllTracesOfArc(pair.Key);
						continue;
					}

					//Arc hasn't changed!
					if (loadedArcs[pair.Key] == pair.Value)
					{
						continue;
					}

					_cachedFile.RemoveAllTracesOfArc(pair.Key);
					arcsToReload[pair.Key] = pair.Value;
				}

				var newArcsToReload =
					loadedArcs.Where(arc => _cachedFile.CachedLoadedAndDatedArcs.Keys.Contains(arc.Key) == false);

				return arcsToReload.Concat(newArcsToReload).ToDictionary(r => r.Key, t => t.Value);
			}, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.AttachedToParent);

			var loadAndAmend = processArcsToLoadWithCache.ContinueWith(arcsProcessTask =>
			{
				var arcsToLoad = arcsProcessTask.Result.Keys.ToArray();

				if (arcsToLoad.Length <= 0)
				{
					return;
				}

				ShortMenuVanillaDatabase.PLogger.LogInfo($"Reloading {arcsToLoad.Length} arc files!");

				var cmArcs = arcsToLoad
					.Where(r => r.ToLower().StartsWith(GameMain.Instance.CMSystem.CM3D2Path,
						StringComparison.OrdinalIgnoreCase)).ToArray();
				var com20Arcs = arcsToLoad
					.Where(r => r.ToLower().StartsWith($"{Paths.GameRootPath}\\GameData_20",
						StringComparison.OrdinalIgnoreCase)).ToArray();
				var comArcs = arcsToLoad.Except(cmArcs).Except(com20Arcs).ToArray();

				var arcFileExplorer = new MultiArcLoader(cmArcs, com20Arcs, comArcs, new string[0],
					Math.Max(Environment.ProcessorCount, 1), MultiArcLoader.LoadMethod.Single, true, false,
					MultiArcLoader.Exclude.Voice | MultiArcLoader.Exclude.BG | MultiArcLoader.Exclude.CSV |
					MultiArcLoader.Exclude.Motion | MultiArcLoader.Exclude.Sound);

				arcFileExplorer.LoadArcs();

				if (arcFileExplorer.arc.Files.Count <= 0)
				{
					return;
				}

				var filesInArc =
					arcFileExplorer.arc.Files.Values.Where(val =>
							val.Name.ToLower()
								.EndsWith(".menu") &&
							!val.Name.ToLower()
								.Contains("_mekure_") &&
							!val.Name.ToLower()
								.Contains("_zurashi_")
						)
						.ToArray();

				var filesToLoad = filesInArc.OrderBy(f => arcFileExplorer.GetContentsArcFilePath(f)).ToArray();

				foreach (var fileInArc in filesToLoad)
				{
					var arcFile = arcFileExplorer.GetContentsArcFilePath(fileInArc);

					if (_cachedFile.ShouldAddMenuFile(fileInArc.Name, arcFile) == false)
					{
						//Main.PLogger.LogDebug($"Skipping {fileInArc.Name} @ {arcFile}");
						continue;
					}

					var data = fileInArc.Pointer.Decompress();

					if (ReadInternalMenuFile(fileInArc.Name, data.Data, out var menuStub) == false)
					{
						ShortMenuVanillaDatabase.PLogger.LogError($"Failed to load {fileInArc.Name} from {arcFile}.");
						continue;
					}

					menuStub.SourceArc = arcFile;

					if (_cachedFile.TryAddMenuFile(menuStub, arcFile) == false)
					{
						//Main.PLogger.LogDebug($"Won't add {menuStub.FileName} from {arcFile} because it surmised it's lower in priority.");
					}
				}

				arcFileExplorer.Dispose();

				ShortMenuVanillaDatabase.PLogger.LogInfo($"Done processing {filesToLoad.Length} menu files @ {stopwatch.Elapsed}");
			}, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.AttachedToParent);

			yield return new WaitWhile(() => loadAndAmend.IsCompleted == false);

			if (loadAndAmend.IsFaulted)
			{
				if (loadAndAmend.Exception?.InnerException != null)
				{
					throw loadAndAmend.Exception.InnerException;
				}
			}

			Done = true;

			_cachedFile.CachedLoadedAndDatedArcs = getArcsToLoadTask.Result;
			File.WriteAllText(_cachePath, JsonConvert.SerializeObject(_cachedFile, Formatting.Indented));

			stopwatch.Stop();
			ShortMenuVanillaDatabase.PLogger.LogInfo($"Completely done @ {stopwatch.Elapsed}");
		}

		private static void ContinuationFunction2(Dictionary<string, DateTime> arcsProcessTask, Stopwatch stopwatch)
		{
			var arcsToLoad = arcsProcessTask.Keys.ToArray();

			if (arcsToLoad.Length <= 0)
			{
				return;
			}

			ShortMenuVanillaDatabase.PLogger.LogInfo($"Reloading {arcsToLoad.Length} arc files!");

			var cmArcs = arcsToLoad
				.Where(r => r.ToLower().StartsWith(GameMain.Instance.CMSystem.CM3D2Path,
					StringComparison.OrdinalIgnoreCase)).ToArray();
			var com20Arcs = arcsToLoad
				.Where(r => r.ToLower().StartsWith($"{Paths.GameRootPath}\\GameData_20",
					StringComparison.OrdinalIgnoreCase)).ToArray();
			var comArcs = arcsToLoad.Except(cmArcs).Except(com20Arcs).ToArray();

			var arcFileExplorer = new MultiArcLoader(cmArcs, com20Arcs, comArcs, new string[0],
				2, MultiArcLoader.LoadMethod.Single, true, false,
				MultiArcLoader.Exclude.Voice | MultiArcLoader.Exclude.BG | MultiArcLoader.Exclude.CSV |
				MultiArcLoader.Exclude.Motion | MultiArcLoader.Exclude.Sound);

			arcFileExplorer.LoadArcs();

			if (arcFileExplorer.arc.Files.Count <= 0)
			{
				return;
			}

			var filesInArc =
				arcFileExplorer.arc.Files.Values.Where(val =>
						val.Name.ToLower()
							.EndsWith(".menu") &&
						!val.Name.ToLower()
							.Contains("_mekure_") &&
						!val.Name.ToLower()
							.Contains("_zurashi_")
					)
					.ToArray();

			var filesToLoad = filesInArc.OrderBy(f => arcFileExplorer.GetContentsArcFilePath(f)).ToArray();

			foreach (var fileInArc in filesToLoad)
			{
				var arcFile = arcFileExplorer.GetContentsArcFilePath(fileInArc);

				if (_cachedFile.ShouldAddMenuFile(fileInArc.Name, arcFile) == false)
				{
					//Main.PLogger.LogDebug($"Skipping {fileInArc.Name} @ {arcFile}");
					continue;
				}

				var data = fileInArc.Pointer.Decompress();

				if (ReadInternalMenuFile(fileInArc.Name, data.Data, out var menuStub) == false)
				{
					ShortMenuVanillaDatabase.PLogger.LogError($"Failed to load {fileInArc.Name} from {arcFile}.");
					continue;
				}

				menuStub.SourceArc = arcFile;

				if (_cachedFile.TryAddMenuFile(menuStub, arcFile) == false)
				{
					//Main.PLogger.LogDebug($"Won't add {menuStub.FileName} from {arcFile} because it surmised it's lower in priority.");
				}
			}

			arcFileExplorer.Dispose();

			ShortMenuVanillaDatabase.PLogger.LogInfo($"Done processing {filesToLoad.Length} menu files @ {stopwatch.Elapsed}");
		}

		private Dictionary<string, DateTime> ContinuationFunction(Dictionary<string, DateTime> loadArcs)
		{
			var loadedArcs = loadArcs;

			//There's no cache file to load. Just load every file then.
			if (File.Exists(_cachePath) == false)
			{
				_cachedFile = new CacheFile();
				return loadedArcs;
			}

			var sCachedFile = File.ReadAllText(_cachePath);
			_cachedFile = JsonConvert.DeserializeObject<CacheFile>(sCachedFile) ?? new CacheFile();
			var arcsToReload = new Dictionary<string, DateTime>();

			if (_cachedFile.CachedLoadedAndDatedArcs is null || _cachedFile.MenusList is null)
			{
				return loadedArcs;
			}

			foreach (var pair in _cachedFile.CachedLoadedAndDatedArcs.ToArray())
			{
				//This arc was removed!
				if (loadedArcs.ContainsKey(pair.Key) == false)
				{
					ShortMenuVanillaDatabase.PLogger.LogDebug($"Removing {pair.Key} as it's missing!");
					_cachedFile.RemoveAllTracesOfArc(pair.Key);
					continue;
				}

				//Arc hasn't changed!
				if (loadedArcs[pair.Key] == pair.Value)
				{
					continue;
				}

				_cachedFile.RemoveAllTracesOfArc(pair.Key);
				arcsToReload[pair.Key] = pair.Value;
			}

			var newArcsToReload = loadedArcs.Where(arc => _cachedFile.CachedLoadedAndDatedArcs.Keys.Contains(arc.Key) == false);

			return arcsToReload.Concat(newArcsToReload).ToDictionary(r => r.Key, t => t.Value);
		}

		private static Dictionary<string, DateTime> GetAllArcsToLoad()
		{
			var currentlyLoadedAndDatedArcs = new Dictionary<string, DateTime>(3);

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

			return currentlyLoadedAndDatedArcs;
		}

		private static bool ReadInternalMenuFile(string fStrMenuFileName, byte[] data, out CacheFile.MenuStub menuStub)
		{
			if (fStrMenuFileName == null) throw new ArgumentNullException(nameof(fStrMenuFileName));
			menuStub = null;

			MemoryStream dataStream;

			try
			{
				dataStream = new MemoryStream(data);
			}
			catch (Exception ex)
			{
				ShortMenuVanillaDatabase.PLogger.LogError(string.Concat("The following menu file could not be read! (メニューファイルがが読み込めませんでした。): ", fStrMenuFileName, "\n\n", ex.Message, "\n", ex.StackTrace));

				return false;
			}

			var cacheEntry = new CacheFile.MenuStub(fStrMenuFileName);

			try
			{
				var binaryReader = new BinaryReader(dataStream, Encoding.UTF8);
				var text = binaryReader.ReadString();

				if (!text.Equals("CM3D2_MENU"))
				{
					ShortMenuVanillaDatabase.PLogger.LogError("ProcScriptBin (例外 : ヘッダーファイルが不正です。) The header indicates a file type that is not a menu file!" + text + " @ " + fStrMenuFileName);

					return false;
				}

				cacheEntry.Version = binaryReader.ReadInt32();
				var path = binaryReader.ReadString();

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
								ShortMenuVanillaDatabase.PLogger.LogWarning("Menu file has no name and an empty name will be used instead." + " @ " + fStrMenuFileName);

								cacheEntry.Name = "";
								break;

							case "setumei" when stringList.Length > 1:
								cacheEntry.Description = stringList[1];
								cacheEntry.Description = cacheEntry.Description.Replace("《改行》", "\n");
								break;

							case "setumei":
								ShortMenuVanillaDatabase.PLogger.LogWarning("Menu file has no description (setumei) and an empty description will be used instead." + " @ " + fStrMenuFileName);

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
								ShortMenuVanillaDatabase.PLogger.LogWarning("The following menu file has a category parent with no category: " + fStrMenuFileName);
								return false;

							case "color_set" when stringList.Length > 1:
								{
									try
									{
										cacheEntry.ColorSetMpn = (MPN)Enum.Parse(typeof(MPN), stringList[1].ToLower());
									}
									catch
									{
										ShortMenuVanillaDatabase.PLogger.LogWarning("There is no category called(カテゴリがありません。): " + stringList[1].ToLower() + " @ " + fStrMenuFileName);

										return false;
									}
									if (stringList.Length >= 3)
									{
										cacheEntry.ColorSetMenu = stringList[2].ToLower();
									}

									break;
								}
							case "color_set":
								ShortMenuVanillaDatabase.PLogger.LogWarning("A color_set entry exists but is otherwise empty" + " @ " + fStrMenuFileName);
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
											ShortMenuVanillaDatabase.PLogger.LogError("無限色IDがありません。(The following free color ID does not exist: )" + text10 + " @ " + fStrMenuFileName);

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
										ShortMenuVanillaDatabase.PLogger.LogError("The following menu file has an icon entry but no field set: " + fStrMenuFileName);

										return false;
									}

									break;
								}
							case "saveitem" when stringList.Length > 1:
								{
									var text11 = stringList[1];
									if (string.IsNullOrEmpty(text11))
									{
										ShortMenuVanillaDatabase.PLogger.LogWarning("SaveItem is either null or empty." + " @ " + fStrMenuFileName);
									}

									break;
								}
							case "saveitem":
								ShortMenuVanillaDatabase.PLogger.LogWarning("A saveitem entry exists with nothing set in the field @ " + fStrMenuFileName);
								break;

							case "unsetitem":
								cacheEntry.DelMenu = true;
								break;

							case "priority" when stringList.Length > 1:
								cacheEntry.Priority = float.Parse(stringList[1]);
								break;

							case "priority":
								ShortMenuVanillaDatabase.PLogger.LogError("The following menu file has a priority entry but no field set. A default value of 10000 will be used: " + fStrMenuFileName);

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
								ShortMenuVanillaDatabase.PLogger.LogError("A menu with a menu folder setting (メニューフォルダ) has an entry but no field set: " + fStrMenuFileName);

								menuStub = null;
								return false;
						}
					}
				}
			}
			catch
			{
				ShortMenuVanillaDatabase.PLogger.LogError("Encountered some error while reading a vanilla menu file...");
				return false;
			}

			menuStub = cacheEntry;
			return true;
		}
	}
}