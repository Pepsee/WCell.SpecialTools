using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using WCell.Tools.Ralek;

namespace ClientTools.Extractors
{
	public static class GameObjectTypeExtractor
	{
		//private static long s_TypeOffset = 0x4AAD38;
		//private static int s_TypeCount = 35;
		//private static long s_SoundOffset = 0x4A9C90;
		//private static int s_SoundCount = 100;
		private static long s_TypeOffset = 0x5C7550;
		private static int s_TypeCount = 36;
		private static long s_SoundOffset = 0x5C61C0;
		private static int s_SoundCount = 128;
		internal static int s_VirtualOffset = 0x401200;

		private static List<GameObjectSoundEntry> s_Sounds;
		private static List<GameObjectTypeInfo> s_Types;
		private static List<GameObject> s_GameObjects;

		public static List<GameObjectSoundEntry> Sounds
		{
			get { return s_Sounds; }
		}

		public static void Extract(WoWFile wowFile)
		{
			ExtractSounds(wowFile);
			ExtractTypes(wowFile);
			MakeFinal(wowFile);

			var file = new StreamWriter("gotypes.txt");
			foreach (var go in s_GameObjects)
			{
				go.DumpInfo(file);
			}
			file.Close();
		}

		private static void MakeFinal(WoWFile wowFile)
		{
			List<GameObject> gameObjects = new List<GameObject>();
			for (int i = 0; i < s_Types.Count; i++)
			{
				GameObject go = new GameObject();

				go.InitFromType(wowFile, s_Types[i], i);

				/*long startPos = wowFile.BaseStream.Position;
                // 0x4A9724
                wowFile.BaseStream.Position = s_Types[i].NameOffset - 0x401800; // 0x4988C4
                go.Name = wowFile.ReadCString();
                //wowFile.BaseStream.Position = startPos;

                go.TypeId = s_Types[i].TypeId;
                go.SoundCount = s_Types[i].soundCount;
                go.Sounds = new GameObjectSoundEntry[go.SoundCount];*/

				gameObjects.Add(go);
			}

			s_GameObjects = gameObjects;
		}


		private static void ExtractSounds(WoWFile wowFile)
		{
			List<GameObjectSoundEntry> sounds = new List<GameObjectSoundEntry>();

			wowFile.BaseStream.Position = s_SoundOffset;

			for (int i = 0; i < s_SoundCount; i++)
			{
				sounds.Add(wowFile.ReadStruct<GameObjectSoundEntry>());
			}

			s_Sounds = sounds;
		}

		private static void ExtractTypes(WoWFile wowFile)
		{
			List<GameObjectTypeInfo> types = new List<GameObjectTypeInfo>();

			wowFile.BaseStream.Position = s_TypeOffset;

			for (int i = 0; i < s_TypeCount; i++)
			{
				types.Add(wowFile.ReadStruct<GameObjectTypeInfo>());
			}

			s_Types = types;
		}

		private static GameObjectSoundEntry[] GetSounds(WoWFile wowFile, GameObjectTypeInfo type)
		{
			return null;
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct GameObjectSoundEntry
	{
		public uint Id;
		public uint NameOffset;
		public uint Field_8;
		public uint Field_C;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct GameObjectTypeInfo
	{
		//public int Id;
		public int NameOffset;
		public int SoundCount;
		public int SoundIdListOffset;
		public int SoundListOffset;
	}

	public struct GameObjectSound
	{
		public int Id;
		public string Name;
	}

	public struct GameObject
	{
		public int TypeId;
		public string Name;
		public int SoundCount;
		public GameObjectSound[] Sounds;

		public void InitFromType(WoWFile wowFile, GameObjectTypeInfo type, int id)
		{
			// 0x4A9724
			wowFile.BaseStream.Position = type.NameOffset - GameObjectTypeExtractor.s_VirtualOffset;//0x401800; // 0x4988C4
			Name = wowFile.ReadCString();
			TypeId = id;
			SoundCount = type.SoundCount;
			Sounds = new GameObjectSound[SoundCount];
			ReadSounds(wowFile, type);
			//DumpInfo(RalekProgram.Log);
		}

		public void DumpInfo(TextWriter writer)
		{
			writer.WriteLine("Type: {0}", Name);
			writer.WriteLine(" Id: {0}", TypeId);
			if (SoundCount > 0)
			{
				writer.WriteLine(" SoundCount: {0}", SoundCount);
				writer.WriteLine(" Sounds:");
				for (int i = 0; i < SoundCount; i++)
				{
					writer.WriteLine("  {0}: {1}", Sounds[i].Id, Sounds[i].Name);
				}
			}
		}

		private void ReadSounds(WoWFile wowFile, GameObjectTypeInfo type)
		{
			if (SoundCount < 1)
				return;

			//long start = type.SoundListOffset - 0x401A00;
			long start = type.SoundIdListOffset - 0x401600;// GameObjectTypeExtractor.s_VirtualOffset;

			wowFile.BaseStream.Position = start;




			for (int i = 0; i < SoundCount; i++)
			{
				Sounds[i] = new GameObjectSound();
				Sounds[i].Id = wowFile.ReadInt32();
				long startPos = wowFile.BaseStream.Position;
				if (Sounds[i].Id > GameObjectTypeExtractor.Sounds.Count)
				{
					System.Diagnostics.Debugger.Break();
				}
				wowFile.BaseStream.Position = GameObjectTypeExtractor.Sounds[Sounds[i].Id].NameOffset - GameObjectTypeExtractor.s_VirtualOffset;
				Sounds[i].Name = wowFile.ReadCString();
				wowFile.BaseStream.Position = startPos;
			}
		}
	}
}