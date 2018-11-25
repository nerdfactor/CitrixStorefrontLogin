using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace CitrixStorefrontLogin {

	/// <summary>
	/// Example Program that shows how to connect to a Citrix Storefront
	/// over a Netscaler Gateway, download a ica file and start Citrix session
	/// with it.
	/// </summary>
	public class Program {

		/// <summary>
		/// The base url to Netscaler website.
		/// </summary>
		public static String Url { get; set; } = "";

		/// <summary>
		/// The username for the login.
		/// </summary>
		public static String Username { get; set; } = "";

		/// <summary>
		/// The password for the login.
		/// </summary>
		public static String Password { get; set; } = "";

		/// <summary>
		/// The appname that should be started.
		/// If empty, will show a list of possible apps.
		/// </summary>
		public static String AppName { get; set; } = "";

		/// <summary>
		/// The path to a PFX file containing a user certificate
		/// and key used to authenticate the client for the requests.
		/// If empty, won't use a certificate to authenticate.
		/// </summary>
		public static String Cert { get; set; } = "";

		/// <summary>
		/// The password for the PFX file.
		/// Is necessary if a PFX file is provided.
		/// </summary>
		public static String CertPw { get; set; } = "";

		static void Main(string[] args) {
			ParseArguments(args);
			if (Url.Length == 0 || Username.Length == 0 || Password.Length == 0) { return; }

			// Create a WebClient that keeps cookies stored and uses a
			// client certificate to authenticate the requests.
			EnhancedWebClient client = new EnhancedWebClient(Cert, CertPw);

			// Create a CitrixConnector that can login and authenticate
			CitrixConnector connector = new CitrixConnector(client, Url);
			connector.LoginUser(Username, Password);
			connector.AuthenticateRequests();
			if(AppName.Length != 0) {
				// load the ica file
				String icaPath = connector.GetIcaFile(AppName);

				// and launch the IcaClient with that file.
				LaunchIcaClient(icaPath);
			}
			
		}

		/// <summary>
		/// This is a very crude way to deal with the Arguments.
		/// But I did not want to add extra code to that is not
		/// needed to showcase this example.
		/// </summary>
		/// <param name="args"></param>
		private static void ParseArguments(string[] args) {
			int length = args.Length;
			if (length >= 1) { Url = args[0]; }
			if (length >= 2) { Username = args[1]; }
			if (length >= 3) { Password = args[2]; }
			if (length >= 4) { AppName = args[3]; }
			if (length >= 5) { Cert = args[4]; }
			if (length >= 6) { CertPw = args[5]; }
		}

		/// <summary>
		/// Launches the Citrix Ica Client and passes the ica file to it.
		/// This should start a new Citrix Session.
		/// </summary>
		/// <param name="icaPath"></param>
		private static void LaunchIcaClient(String icaPath, String icaClientPath = "") {
			if (!File.Exists(icaPath)) { return; }

			// Check that the path to the Citrix Ica Client exists.
			// Otherwise find the proper path to to it by checking the executable
			// associated to the .ica file extension.
			if(icaClientPath.Length == 0 || !File.Exists(icaClientPath)) {
				icaClientPath = Win32.GetAssocString(Win32.AssocStr.EXECUTABLE, ".ica");
				if (!File.Exists(icaClientPath)) { return; }
			}

			// Start a new Citrix Ica Client process with the ica file path as argument.
			Process process = new Process();
			process.StartInfo.FileName = icaClientPath;
			process.StartInfo.Arguments = icaPath;
			process.StartInfo.WindowStyle = ProcessWindowStyle.Maximized;
			process.Start();

			// Clean up after the session has started. Is not nessecary
			// but if don't like leaving trash in temp path.
			// The Ica Client will spawn new processes and finish. But the
			// ica file will still be needed at that time. Therefor it is
			// not possible to just wait until it has finished, but wait
			// some proper seconds.
			Thread.Sleep(1000);
			File.Delete(icaPath);
		}
	}
}