using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CitrixStorefrontLogin {
	/// <summary>
	/// Some native Win32 Api Methods.
	/// </summary>
	public class Win32 {


		/// <summary>
		/// The Shlwapi.dll provides some nice Wrapper functions for the Windows shell. 
		/// One of them is AssocQueryString to get the program associated with a file
		/// extension. No need to manually check within the registry.
		/// Thanks to Ohad Schneider and reduckted: http://stackoverflow.com/a/17773402/5801927
		/// </summary>
		/// <param name="flags"></param>
		/// <param name="str"></param>
		/// <param name="pszAssoc"></param>
		/// <param name="pszExtra"></param>
		/// <param name="pszOut"></param>
		/// <param name="pcchOut"></param>
		/// <returns></returns>
		[DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
		public static extern uint AssocQueryString(AssocF flags, AssocStr str,
		   string pszAssoc, string pszExtra, [Out] StringBuilder pszOut, ref uint
		   pcchOut);

		public enum AssocF {
			NONE = 0x00000000,
			INIT_NOREMAPCLSID = 0x00000001,
			INIT_BYEXENAME = 0x00000002,
			OPEN_BYEXENAME = 0x00000002,
			INIT_DEFAULTTOSTAR = 0x00000004,
			INIT_DEFAULTTOFOLDER = 0x00000008,
			NOUSERSETTINGS = 0x00000010,
			NOTRUNCATE = 0x00000020,
			VERIFY = 0x00000040,
			REMAPRUNDLL = 0x00000080,
			NOFIXUPS = 0x00000100,
			IGNOREBASECLASS = 0x00000200,
			INIT_IGNOREUNKNOWN = 0x00000400,
			INIT_FIXED_PROGID = 0x00000800,
			IS_PROTOCOL = 0x00001000,
			INIT_FOR_FILE = 0x00002000
		}

		public enum AssocStr {
			COMMAND = 1,
			EXECUTABLE,
			FRIENDLYDOCNAME,
			FRIENDLYAPPNAME,
			NOOPEN,
			SHELLNEWVALUE,
			DDECOMMAND,
			DDEIFEXEC,
			DDEAPPLICATION,
			DDETOPIC,
			INFOTIP,
			QUICKTIP,
			TILEINFO,
			CONTENTTYPE,
			DEFAULTICON,
			SHELLEXTENSION,
			DROPTARGET,
			DELEGATEEXECUTE,
			SUPPORTED_URI_PROTOCOLS,
			PROGID,
			APPID,
			APPPUBLISHER,
			APPICONREFERENCE,
			MAX
		}

		/// <summary>
		/// Get the associated element for an extension.
		/// </summary>
		/// <param name="association"></param>
		/// <param name="extension"></param>
		/// <returns></returns>
		public static string GetAssocString(AssocStr association, string extension) {
			const int S_OK = 0;
			const int S_FALSE = 1;

			uint length = 0;
			uint ret = AssocQueryString(AssocF.NONE, association, extension, null, null, ref length);
			if (ret != S_FALSE) {
				throw new InvalidOperationException("Could not determine associated string");
			}

			var sb = new StringBuilder((int)length); // (length-1) will probably work too as the marshaller adds null termination
			ret = AssocQueryString(AssocF.NONE, association, extension, null, sb, ref length);
			if (ret != S_OK) {
				throw new InvalidOperationException("Could not determine associated string");
			}

			return sb.ToString();
		}
	}
}
