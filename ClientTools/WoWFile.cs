/*************************************************************************
 *
 *   file		: WoWFile.cs
 *   copyright		: (C) The WCell Team
 *   email		: info@wcell.org
 *   last changed	: $LastChangedDate: 2009-04-30 01:12:32 +0800 (Thu, 30 Apr 2009) $
 *   last author	: $LastChangedBy: ralekdev $
 *   revision		: $Rev: 881 $
 *
 *   This program is free software; you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation; either version 2 of the License, or
 *   (at your option) any later version.
 *
 *************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using NLog;
using WCell.Constants;
using WCell.Tools.Ralek;
using WCell.Util.NLog;

namespace ClientTools
{
	public class WoWFile : BinaryReader
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		private ClientVersion m_version;
		private string m_content, m_filename;

		public ClientVersion Version
		{
			get { return m_version; }
		}

		public string FileString
		{
			get { return m_content; }
		}

		public string FileName
		{
			get { return m_filename; }
		}

		public WoWFile(string wowExeName)
			: base(new FileStream(wowExeName, FileMode.Open))
		{
			m_filename = wowExeName;
			var sr = new StreamReader(BaseStream, Encoding.ASCII);

			m_content = sr.ReadToEnd();

			try
			{
				FindVersionInfo();
			}
			catch (Exception e)
			{
				LogUtil.ErrorException(e, "Could not determine Client-Version");
			}

			BaseStream.Position = 0;
		}

		public int CurrentChar
		{
			get { return PeekChar(); }
		}

		private void FindVersionInfo()
		{
			long startOffset = FileString.IndexOf("Version %s (%s) %s");

			if (startOffset > 0)
			{
				BaseStream.Position = startOffset;
				this.ReadCString();

				while (PeekChar() == 0)
				{
					BaseStream.Position++;
				}
				var version = this.ReadCString();

				while (PeekChar() == 0)
				{
					BaseStream.Position++;
				}
				var build = this.ReadCString();

				try
				{
					m_version = new ClientVersion(version + "." + build);
				}
				catch
				{
					// something went wrong:
					FindVersionInfo2();
				}
				/*
				 * 6898....2.1.3...RELEASE_BUILD
				 * */
				//BaseStream.Position = startOffset - 16;

				//var build = this.ReadCString();
				//BaseStream.Position += 3;
				//m_version = new ClientVersion(this.ReadCString() + "." + build);
			}
			else
			{
				//throw new InvalidDataException("Could not locate version information");
				FindVersionInfo2();
			}
		}

		/// <summary>
		/// Things changed in 3.2.0
		/// </summary>
		private void FindVersionInfo2()
		{
			long startOffset = FileString.IndexOf("RELEASE_BUILD");

			if (startOffset > 0)
			{
				BaseStream.Position = startOffset;

				do
				{
					BaseStream.Position--;
				} while (PeekChar() == 0);

				while (PeekChar() != 0)
				{
					BaseStream.Position--;
				}

				BaseStream.Position++;
				var version = this.ReadCString();
				BaseStream.Position -= 2;

				while (PeekChar() != 0)
				{
					BaseStream.Position--;
				}

				do
				{
					BaseStream.Position--;
				} while (PeekChar() == 0);

				while (PeekChar() != 0)
				{
					BaseStream.Position--;
				}

				BaseStream.Position++;
				var build = this.ReadCString();

				m_version = new ClientVersion(version + "." + build);
				/*
				 * Aug 17 2009.10314....3.2.0..RELEASE_BUILD
				 * */
				//BaseStream.Position = startOffset - 16;

				//var build = this.ReadCString();
				//BaseStream.Position += 3;
				//m_version = new ClientVersion(this.ReadCString() + "." + build);
			}
			else
			{
				throw new InvalidDataException("Could not locate version information");
			}
		}

		public override string ToString()
		{
			return FileName + string.Format(" (v. {0})", Version);
		}
	}
}