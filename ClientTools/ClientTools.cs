using System;
using System.IO;
using WCell.Tools;

namespace ClientTools
{
	public static class ClientTools
	{
		public static void Main(string[] args)
		{
			var path = Path.GetFullPath(@"../../../../../WCell/Run/Debug/");
			if (!File.Exists(path))
			{
				throw new Exception("Cannot start ClientTools - Please make sure the path is correct (By default: Inside of a folder that is on the same level as the WCell/ folder)");
			}
			Environment.CurrentDirectory = path;
			if (!Tools.Init("", typeof(ClientTools).Assembly))
			{
				return;
			}

			Tools.StartCommandLine();

			Console.WriteLine("Press ANY key to exit...");
			Console.ReadKey();
		}
	}
}
