using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ClientTools.Extractors;
using WCell.Tools.Commands;

namespace ClientTools.Commands
{
	public class GODumpCommand : ToolCommand
	{
		protected override void Initialize()
		{
			Init("GODump");
			EnglishDescription = "Dumps the gameobject entry structures to gotypes.txt";
		}

		public override void Process(WCell.Util.Commands.CmdTrigger<ToolCmdArgs> trigger)
		{
			using (var wowFile = new WoWFile(trigger.Text.NextWord()))
			{
				GameObjectTypeExtractor.Extract(wowFile);
			}
			//base.Process(trigger);
		}
	}
}