using System.IO;
using ClientTools.Extractors;
using ClientTools.Misc;
using ClientTools.UpdateFields;
using WCell.Constants;
using WCell.MPQTool;
using WCell.RealmServer;
using WCell.Tools;
using WCell.Tools.Code;
using WCell.Tools.Domi;
using WCell.Tools.Spells;
using WCell.Util.Toolshed;

namespace ClientTools.Versions
{
	/// <summary>
	/// Generic utility class to easily make WCell work with the latest Client version
	/// </summary>
	public static class VersionUpdater
	{
		private static WoWFile _wowFile;
		public static UpdateFieldExtractor _extractor;

		public static WoWFile WoWFile
		{
			get
			{
				if (_wowFile == null)
				{
					_wowFile = new WoWFile(ToolConfig.Instance.WoWFileLocation);
					_extractor = new UpdateFieldExtractor(_wowFile);
				}
				return _wowFile;
			}
		}

		public static string DBCFolder
		{
			get { return Path.Combine(RealmServerConfiguration.ContentDir, "DBC" + WoWFile.Version.BasicString + "/"); }
		}

		public static UpdateFieldExtractor Extractor
		{
			get { return _extractor; }
		}

		public static void SetWowDir(string dir)
		{
			_wowFile = null;
			ToolConfig.Instance.wowDir = dir;
			_extractor = new UpdateFieldExtractor(WoWFile);
		}

		public static void DumpDBCs()
		{
			DBCTool.DumpToDir(DBCFolder);
		}

		/// <summary>
		/// WARNING: This re-generates code-files to comply with the current client-version
		/// </summary>
        public static void DoUpdate(bool dumpDBCs, bool writeVersionFiles = true)
		{
			if (dumpDBCs)
			{
				DumpDBCs();
			}
            DoUpdate(writeVersionFiles);
		}

		/// <summary>
		/// WARNING: This re-generates code-files to comply with the current client-version
		/// </summary>
		public static void DoUpdate(bool writeVersionFiles = true)
		{
			RealmServerConfiguration.DBCFolderName = "dbc" + WoWFile.Version.BasicString;
            if (writeVersionFiles)
            {
                WriteWCellInfo();
                ExtractUpdateFields();
                ExtractSpellFailures();
            }
		    WCellEnumWriter.WriteAllEnums();
			SpellLineWriter.WriteSpellLines();

		    BackupInstancesXmlFile();
			Instances.WriteInstanceStubs();
            Instances.MergeInstancesXML();
		    ClientDBConstantsWriter.WriteClientDBStrings();
		}

        public static void BackupInstancesXmlFile()
        {
            var path = RealmServerConfiguration.GetContentPath("Instances.xml");

            //If the file isnt there we dont need to waste our time
            if (!File.Exists(path)) return;

            var backupPath = RealmServerConfiguration.GetContentPath("Instances.old.xml");

            //Copy the instances.xml into Instances.old.xml
            File.Copy(path, backupPath, true);

            //If the backup was successful remove the original file
            if(File.Exists(backupPath))
            {
                File.Delete(path);
            }
        }

		[Tool]
		public static void ExtractUpdateFields()
		{
			var updateFieldFile = Path.Combine(ToolConfig.WCellConstantsUpdates, "UpdateFieldEnums.cs");
			Extractor.DumpEnums(updateFieldFile);

			var mgr = new UpdateFieldWriter(_extractor.Extract());
			mgr.Write();
		}

		[Tool]
		public static void ExtractSpellFailures()
		{
			var spellFailedReasonFile = Path.Combine(ToolConfig.WCellConstantsRoot, "Spells/SpellFailedReason.cs");
			SpellFailureExtractor.Extract(WoWFile, spellFailedReasonFile);
		}

		public static void WriteWCellInfo()
		{
			using (var writer = new CodeFileWriter(Path.Combine(ToolConfig.WCellConstantsRoot, "WCellInfo.cs"),
												   "WCell.Constants", "WCellInfo", "static class", "", "WCell.Constants.Misc"))
			{
				writer.WriteSummary(@"The official codename of the current WCell build");
				writer.WriteLine("public const string Codename = \"Amethyst\";");
				writer.WriteLine();
				writer.WriteSummary(@"The color of the current WCell codename");
				writer.WriteLine(@"public const ChatColor CodenameColor = ChatColor.Purple;");
				writer.WriteLine();
				writer.WriteSummary("The version of the WoW Client that is currently supported.");
				writer.WriteLine("public static readonly ClientVersion RequiredVersion = new ClientVersion({0}, {1}, {2}, {3});",
				                 WoWFile.Version.Major, WoWFile.Version.Minor, WoWFile.Version.Revision, WoWFile.Version.Build);
				writer.WriteLine();
			}
		}
	}
}