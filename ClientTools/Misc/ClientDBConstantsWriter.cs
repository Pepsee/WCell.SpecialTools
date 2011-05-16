using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ClientTools.Versions;
using WCell.Tools;
using WCell.Tools.Code;
using WCell.Util.Toolshed;

namespace ClientTools.Misc
{
    public class ClientDBConstantsWriter
    {
        private static CodeFileWriter _writer;
        private static HashSet<string> _db2FileNames = new HashSet<string>();
        private static HashSet<string> _dbcFileNames = new HashSet<string>();

        [Tool]
        public static void WriteClientDBStrings()
        {
            GrabFileNames();
            WriteClientDBStrings(ToolConfig.WCellCoreRoot + "ClientDB/ClientDBConstants.cs");
        }

        public static void WriteClientDBStrings(string outputFileName)
        {
            using (_writer = new CodeFileWriter(outputFileName, "WCell.Core.ClientDB", "ClientDBConstants", "static class", ""))
            {
                var sb = new StringBuilder();

                _writer.WriteMap("DB2 Files");
                foreach (var fileName in _db2FileNames)
                {
                    WriteField(sb, fileName);
                }
                _writer.WriteEndMap();

                _writer.WriteLine();

                _writer.WriteMap("DBC Files");
                foreach (var fileName in _dbcFileNames)
                {
                    WriteField(sb, fileName);
                }
                _writer.WriteEndMap();
            }
        }

        private static void WriteField(StringBuilder sb, string fileName)
        {
            sb.Clear();
            sb.Append("public const string ");
            var len = sb.Length;
            FormatField(fileName, ref sb);
            if (sb.Length != len)
            {
                sb.Append(" = \"");
                sb.Append(fileName);
                sb.Append("\";");
                _writer.WriteSummary(fileName);
                _writer.WriteLine(sb.ToString());
                _writer.WriteLine();
            }
        }

        public static void GrabFileNames()
        {
            foreach (var file in Directory.GetFiles(VersionUpdater.DBCFolder))
            {
                var name = Path.GetFileName(file);
                if(String.IsNullOrEmpty(name))
                    continue;

                char[] delimiters = { '.' };
                var splitFilename = name.ToUpper().Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
                if (splitFilename.Length != 2)
                    continue;
                if (!splitFilename[1].Contains("DB2"))
                    continue;

                _db2FileNames.Add(name);
            }

            foreach (var file in Directory.GetFiles(VersionUpdater.DBCFolder))
            {
                var name = Path.GetFileName(file);
                if (String.IsNullOrEmpty(name))
                    continue;

                char[] delimiters = { '.' };
                var splitFilename = name.ToUpper().Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
                if (splitFilename.Length != 2)
                    continue;
                if (!splitFilename[1].Contains("DBC"))
                    continue;

                _dbcFileNames.Add(name);
            }
        }

        public static void FormatField(string input, ref StringBuilder sb)
        {
            char[] delimiters = {'.'};
            var splitFilename = input.ToUpper().Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            sb.Append(splitFilename[1]);
            sb.Append("_");
            splitFilename[0] = splitFilename[0].Replace('-', '_');
            sb.Append(splitFilename[0]);
        }
    }
}
