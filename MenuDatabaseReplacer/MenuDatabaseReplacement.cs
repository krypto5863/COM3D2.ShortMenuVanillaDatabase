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
using UnityEngine;

namespace ShortMenuVanillaDatabase
{
	public class MenuDatabaseReplacement
	{
		public Dictionary<int, CacheFile.MenuStub> MenusList { get; private set; }

		public bool Done { get; private set; }

		public int Index { get; private set; }

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

				foreach (string s in PathsToLoad)
				{
					var ArcFilesLoaded = Directory.GetFiles(s, "menu*.arc").ToList();
					ArcFilesLoaded.AddRange(Directory.GetFiles(s, "parts*.arc"));

					foreach (string arc in ArcFilesLoaded)
					{
						CurrentlyLoadedAndDatedArcs[arc] = File.GetLastWriteTimeUtc(arc);
					}
				}

				Main.logger.LogDebug($"We found {CurrentlyLoadedAndDatedArcs.Count} that fit our specifications.");

				MultiArcLoader arcFileExplorer = new MultiArcLoader(CurrentlyLoadedAndDatedArcs.Keys.ToArray(), Environment.ProcessorCount, MultiArcLoader.LoadMethod.Single, true, false, MultiArcLoader.Exclude.Voice | MultiArcLoader.Exclude.BG | MultiArcLoader.Exclude.CSV | MultiArcLoader.Exclude.Motion | MultiArcLoader.Exclude.Sound);

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

						throw e;
					}

					Main.logger.LogDebug($"Cache was read, it contains {cachedFile.MenusList.Count()} entries");

					Main.logger.LogDebug($"We extrapolated {cachedFile.CachedLoadedAndDatedArcs.Count} arcs were loaded from cache...");

					List<string> ArcsToReload = new List<string>();

					var ArcsToCheck = CurrentlyLoadedAndDatedArcs
					.Keys
					.Where(k => cachedFile.MenusList.Select(m => m.SourceArc).Contains(k) || !cachedFile.CachedLoadedAndDatedArcs.ContainsKey(k));

					foreach (string Arc in ArcsToCheck)
					{
						if (!cachedFile.CachedLoadedAndDatedArcs.ContainsKey(Arc) || CurrentlyLoadedAndDatedArcs[Arc] != cachedFile.CachedLoadedAndDatedArcs[Arc])
						{
							if (cachedFile.CachedLoadedAndDatedArcs.ContainsKey(Arc) && CurrentlyLoadedAndDatedArcs.ContainsKey(Arc))
							{
								Main.logger.LogDebug($"{Arc} was modified and requires reloading! Entries from this arc will be removed and remade!\nTime In Cache:{cachedFile.CachedLoadedAndDatedArcs[Arc]}\nTime In Folder{CurrentlyLoadedAndDatedArcs[Arc]}");
							}

							ArcsToReload.Add(Arc);

							cachedFile.MenusList.RemoveWhere(menu => menu.SourceArc.Equals(Arc));
						}
					}

					Main.logger.LogDebug($"Done checking over files... Updating held list of menus with succesfully cached files...");

					foreach (CacheFile.MenuStub f in cachedFile.MenusList)
					{
						MenusList[Index++] = f;
					}

					if (ArcsToReload.Count > 0)
					{
						arcFileExplorer = new MultiArcLoader(ArcsToReload.ToArray(), Environment.ProcessorCount, MultiArcLoader.LoadMethod.Single, true, false, MultiArcLoader.Exclude.Voice | MultiArcLoader.Exclude.BG | MultiArcLoader.Exclude.CSV | MultiArcLoader.Exclude.Motion | MultiArcLoader.Exclude.Sound);

						arcFileExplorer.LoadArcs();


						Main.logger.LogDebug($"Done, loading refreshed files...");
					}
					else
					{
						arcFileExplorer = null;

						Main.logger.LogDebug($"No arc files needed updating...");
					}
				}
				else
				{
					arcFileExplorer.LoadArcs();
				}

				if (arcFileExplorer != null && arcFileExplorer.arc.Files.Count > 0)
				{
					Main.logger.LogInfo($"Arcs read in {stopwatch.Elapsed}");

					var filesInArc = new HashSet<CM3D2.Toolkit.Guest4168Branch.Arc.Entry.ArcFileEntry>(arcFileExplorer.arc.Files.Values.Where(val => val.Name.Contains(".menu")));

					foreach (CM3D2.Toolkit.Guest4168Branch.Arc.Entry.ArcFileEntry fileInArc in filesInArc)
					{
						var arcFile = arcFileExplorer.GetContentsArcFilePath(fileInArc);

						var data = fileInArc.Pointer.Decompress();

						ReadInternalMenuFile(fileInArc.Name, arcFile, data.Data);
					}
				}

				Main.logger.LogInfo($"Menu file stubs were done loading at {stopwatch.Elapsed}");

				stopwatch = null;

				Main.logger.LogInfo($"Nulling arc...");

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

		private void ReadInternalMenuFile(string f_strMenuFileName, string sourceArc, byte[] data)
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

				return;
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

				//cacheEntry.Hash = (ulong)data.GetHashCode();

				if (text != "CM3D2_MENU")
				{
					Main.logger.LogError("ProcScriptBin (例外 : ヘッダーファイルが不正です。) The header indicates a file type that is not a menu file!" + text + " @ " + f_strMenuFileName);

					return;
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
						if (stringCom == "name")
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
						else if (stringCom == "setumei")
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
						else if (stringCom == "category")
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
								return;
							}
						}
						else if (stringCom == "color_set")
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

									return;
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
						else if (stringCom == "tex" || stringCom == "テクスチャ変更")
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

									return;
								}
								cacheEntry.MultiColorID = pcMultiColorID;
							}
						}
						else if (stringCom == "icon" || stringCom == "icons")
						{
							if (stringList.Length > 1)
							{
								cacheEntry.Icon = stringList[1];
							}
							else
							{
								Main.logger.LogError("The following menu file has an icon entry but no field set: " + f_strMenuFileName);

								return;
							}
						}
						else if (stringCom == "saveitem")
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
						else if (stringCom == "unsetitem")
						{
							cacheEntry.DelMenu = true;
						}
						else if (stringCom == "priority")
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
						else if (stringCom == "メニューフォルダ")
						{
							if (stringList.Length > 1)
							{
								if (stringList[1].ToLower() == "man")
								{
									cacheEntry.ManMenu = true;
								}
							}
							else
							{
								Main.logger.LogError("A menu with a menu folder setting (メニューフォルダ) has an entry but no field set: " + f_strMenuFileName);

								return;
							}
						}
					}
				}

				var ExistingMenu = MenusList.Where(t => t.Value.FileName == cacheEntry.FileName).ToList();

				if (ExistingMenu.Count() > 0)
				{

					try
					{

						var firstEntry = ExistingMenu.First();

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
							catch { Main.logger.LogWarning("Failed to remove some old menus from the cache! This many cause duplicates... You may want to delete your cache!"); }//Better to just keep going in the case of a failure like this.
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
			}
		}
	}
}
