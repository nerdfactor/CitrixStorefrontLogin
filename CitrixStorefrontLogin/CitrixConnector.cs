using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace CitrixStorefrontLogin {

	/// <summary>
	/// Connects to a Citrix Storefront through a Netscaler Gateway.
	/// </summary>
	public class CitrixConnector {

		/// <summary>
		/// Check if the CitrixConnector has logged in to the Netscaler Website.
		/// </summary>
		public bool IsLoggedIn { get; private set; }

		/// <summary>
		/// Check if the CitrixConnector has authenticated with the Storefront.
		/// </summary>
		public bool IsAuthenticated { get; private set; }

		/// <summary>
		/// WebClient for requests to the Netscaler.
		/// </summary>
		private EnhancedWebClient client;

		/// <summary>
		/// BaseUrl for all requests to the Netscaler.
		/// </summary>
		private String baseUrl;
		
		/// <summary>
		/// Random DeviceId.
		/// </summary>
		private String deviceId = "";

		/// <summary>
		/// Security token provided by the Netscaler.
		/// </summary>
		private String csrfToken = "";

		public CitrixConnector(EnhancedWebClient client, String baseUrl) {
			this.client = client;
			this.baseUrl = baseUrl;
		}

		/// <summary>
		/// The login to the Netscaler Website is the first step to interact
		/// with the Storefront behind the Netscaler.
		/// </summary>
		/// <param name="username"></param>
		/// <param name="password"></param>
		public void LoginUser(String username, String password) {
			LoginToNetscalerWebsite(username, password);
			SetIcaClient();
			deviceId = GenerateDeviceId();
			LoadStore();
			IsLoggedIn = client.GetAllCookies().ContainsKey("NSC_AAAC");
		}

		/// <summary>
		/// After login to the Netscaler Website it is possible to authenticate
		/// further requests to the Storefront behind it.
		/// </summary>
		public void AuthenticateRequests() {
			if (!IsLoggedIn) { return; }
			csrfToken = GetConfiguration();
			String authMethodsUrl = GetAuthMethodsUrl();
			Dictionary<String, String> methods = GetAuthMethods(authMethodsUrl);
			LoginToStorefront(methods["CitrixAGBasic"]);
			IsAuthenticated = client.GetAllCookies().ContainsKey("CtxsAuthId");
		}

		/// <summary>
		/// Get a list of possible app resources.
		/// </summary>
		/// <returns></returns>
		public Dictionary<String, String> GetResourceList() {
			if (!IsAuthenticated) { return null; }
			return GetAppList();
		}

		/// <summary>
		/// Load a ica file from the possible apps on the resource list.
		/// </summary>
		/// <param name="appName"></param>
		/// <returns></returns>
		public String GetIcaFile(String appName) {
			if (!IsAuthenticated) { return ""; }
			Dictionary<String, String> apps = GetAppList();
			return LoadIcaFile(apps[appName]);
		}

		#region Website Requests

		/// <summary>
		/// Login to the Netscaler website to get access to the Storefront.
		/// behind it.
		/// </summary>
		/// <param name="username"></param>
		/// <param name="password"></param>
		/// <returns></returns>
		private void LoginToNetscalerWebsite(String username, String password) {
			client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
			string result = client.UploadString($"{baseUrl}/cgi/login", $"login={username}&passwd={password}");
		}

		/// <summary>
		/// Set the client at the Netscaler. Not realy sure why this is needed. It does not
		/// return anything that is required for other requests. But it will most likely 
		/// set something on the server side.
		/// </summary>
		/// <returns></returns>
		private void SetIcaClient() {
			// Set headers to indicate, that this should be a secure request that comes
			// directly as a referral from the login page.
			client.Headers[HttpRequestHeader.Accept] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
			client.Headers[HttpRequestHeader.Referer] = $"{baseUrl}/vpn/index.html";
			client.Headers["Upgrade-Insecure-Requests"] = "1";

			string result = client.DownloadString($"{baseUrl}/cgi/setClient?wica");
		}

		/// <summary>
		/// Load the Store Website on the Netscaler that can be used by the user
		/// to manually select apps. Sets this client up to make Api requests.
		/// </summary>
		private void LoadStore() {
			String domain = (new Uri(baseUrl)).Host;

			// Set headers to indicate, that this should be a secure request that comes
			// directly as a referral after setting the Client.
			client.Headers[HttpRequestHeader.Accept] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
			client.Headers[HttpRequestHeader.Referer] = $"{baseUrl}/cgi/setClient?wica";
			client.Headers["Upgrade-Insecure-Requests"] = "1";

			// Make sure to set some cookies that are usually set by Netscaler website.
			// They may not be nessecary, but better safe than sorry.
			client.AddCookie(new Cookie("CtxsClientDetectionDone", "true", "/", domain));
			client.AddCookie(new Cookie("CtxsDesktopAutoLaunchDone", "no", "/", domain));
			client.AddCookie(new Cookie("CtxsHasUpgradeBeenShown", "true", "/", domain));
			client.AddCookie(new Cookie("CtxsPasswordChangeAllowed", "true", "/", domain));
			client.AddCookie(new Cookie("CtxsUserPreferredClient", "Native", "/", domain));
			client.AddCookie(new Cookie("isGatewaySession", "", "/", domain));

			// Very important is to set the Device Id to something.
			client.AddCookie(new Cookie("CtxsDeviceId", deviceId, "/", domain));

			string result = client.DownloadString($"{baseUrl}/Citrix/StoreWeb/");
		}

		#endregion


		#region Api Requests

		/// <summary>
		/// Get the configuration of the Storefront. Contains some usefull settings like
		/// the url to the resource list. But most important is the csrf token that will
		/// be returned. It has to be used for all following requests.
		/// </summary>
		/// <returns></returns>
		private String GetConfiguration() {
			// Set headers to indicate, that this should be a secure request that comes
			// directly as a referral after loading the store.
			client.Headers[HttpRequestHeader.Accept] = "application/xml, text/xml, */*; q=0.01";
			client.Headers[HttpRequestHeader.Referer] = $"{baseUrl}/Citrix/StoreWeb/";
			client.Headers["Upgrade-Insecure-Requests"] = "1";
			client.Headers["X-Citrix-IsUsingHTTPS"] = "Yes";
			client.Headers["X-Requested-With"] = "XMLHttpRequest";

			string result = client.UploadString($"{baseUrl}/Citrix/StoreWeb/Home/Configuration", "");

			// The cookies will contain the CsrfToken that is necessary to be 
			// send as header in all following requests.
			return client.GetAllCookies()["CsrfToken"]?.Value;
		}

		/// <summary>
		/// Get the url to the authentication methods. Just do a request
		/// that does not require authentication (because of 403 response) but
		/// is also not pubic. Something like the resource list.
		/// </summary>
		/// <returns></returns>
		private String GetAuthMethodsUrl() {
			// Set headers to indicate, that this should be a secure request that comes
			// directly as a referral after loading the store.
			client.Headers[HttpRequestHeader.Accept] = @"application/json, text/javascript, */*; q=0.01";
			client.Headers[HttpRequestHeader.Referer] = $"{baseUrl}/Citrix/StoreWeb/";
			client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
			client.Headers["Upgrade-Insecure-Requests"] = "1";
			client.Headers["X-Citrix-IsUsingHTTPS"] = "Yes";
			client.Headers["X-Requested-With"] = "XMLHttpRequest";

			// Send the csrfToken within the header.
			client.Headers["Csrf-Token"] = csrfToken;

			// Make a request to the resource list that should fail.
			string result = client.UploadString($"{baseUrl}/Citrix/StoreWeb/Resources/List", "format=json");

			// Find the authentication url within the response headers.
			if (result.Contains("{\"unauthorized\": true}")) {
				return GetAuthenticationUrlFromHeader(client);
			}
			return "";
		}

		/// <summary>
		/// Get a list of apps that the user can start from the Storefront.
		/// If not already logged in the request will return a unauthorized
		/// error.
		/// </summary>
		/// <returns></returns>
		private Dictionary<String, String> GetAppList() {
			Dictionary<String, String> apps = new Dictionary<string, string>();

			// Set headers to indicate, that this should be a secure request that comes
			// directly as a referral after loading the store.
			client.Headers[HttpRequestHeader.Accept] = @"application/json, text/javascript, */*; q=0.01";
			client.Headers[HttpRequestHeader.Referer] = $"{baseUrl}/Citrix/StoreWeb/";
			client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
			client.Headers["Upgrade-Insecure-Requests"] = "1";
			client.Headers["X-Citrix-IsUsingHTTPS"] = "Yes";
			client.Headers["X-Requested-With"] = "XMLHttpRequest";

			// Send the csrfToken within the header.
			client.Headers["Csrf-Token"] = csrfToken;

			string result = client.UploadString($"{baseUrl}/Citrix/StoreWeb/Resources/List", "format=json&resourceDetails=Default");

			// Find all apps within the result. It could be done by parsing the Json
			// but I didn't want to add additional dependencies. Therefor just a very
			// simple regex to find them all.
			if (!result.Contains("{\"unauthorized\": true}")) {
				Regex r = new Regex("launchurl\":\"([^\"]+)\",\"name\":\"([^\"]+)\"");
				MatchCollection matches = r.Matches(result);
				foreach (Match match in matches) {
					apps.Add(match.Groups[2].Value, match.Groups[1].Value);
				}
			}
			return apps;
		}

		/// <summary>
		/// Get a list of authentication methods that can be used to login to the Storefront.
		/// For some versions of Netscaler/Storefront this needs to be called before one
		/// of the authentication methods is called.
		/// </summary>
		/// <param name="authMethodsUrl"></param>
		/// <returns></returns>
		private Dictionary<String, String> GetAuthMethods(String authMethodsUrl = "Authentication/GetAuthMethods") {
			Dictionary<String, String> methods = new Dictionary<string, string>();

			// Set headers to indicate, that this should be a secure request that comes
			// directly as a referral after loading the store.
			client.Headers[HttpRequestHeader.Accept] = @"application/xml, text/xml, */*; q=0.01";
			client.Headers[HttpRequestHeader.Referer] = $"{baseUrl}/Citrix/StoreWeb/";
			client.Headers["Upgrade-Insecure-Requests"] = "1";
			client.Headers["X-Citrix-IsUsingHTTPS"] = "Yes";
			client.Headers["X-Requested-With"] = "XMLHttpRequest";

			// Send the csrfToken within the header.
			client.Headers["Csrf-Token"] = csrfToken;

			string result = client.UploadString($"{baseUrl}/Citrix/StoreWeb/{authMethodsUrl}", "");

			// Find all the methods within the result. It could be done by parsing the XML
			// but I didn't want to add additional dependencies. Therefor just a very
			// simple regex to find them all.
			if (result.Length != 0) {
				Regex r = new Regex("method name=\"([^\"]+)\" url=\"([^\"]+)");
				MatchCollection matches = r.Matches(result);
				foreach (Match match in matches) {
					methods.Add(match.Groups[1].Value, match.Groups[2].Value);
				}
			}
			return methods;
		}

		/// <summary>
		/// Login to the Storefront to get access to app list.
		/// </summary>
		/// <param name="authUrl"></param>
		private void LoginToStorefront(String authUrl = "GatewayAuth/Login") {
			// Set headers to indicate, that this should be a secure request that comes
			// directly as a referral after loading the store.
			client.Headers[HttpRequestHeader.Accept] = @"application/json, text/javascript, */*; q=0.01";
			client.Headers[HttpRequestHeader.Referer] = $"{baseUrl}/Citrix/StoreWeb/";
			client.Headers["Upgrade-Insecure-Requests"] = "1";
			client.Headers["X-Citrix-IsUsingHTTPS"] = "Yes";
			client.Headers["X-Requested-With"] = "XMLHttpRequest";

			// Send the csrfToken within the header.
			client.Headers["Csrf-Token"] = csrfToken;

			string result = client.UploadString($"{baseUrl}/Citrix/StoreWeb/{authUrl}", "");
		}

		/// <summary>
		/// Loads a ica file from the Storefront and writes it into the temp directory
		/// of the user or to a specified location.
		/// </summary>
		/// <param name="icaUrl"></param>
		/// <param name="icaPath"></param>
		/// <returns></returns>
		private String LoadIcaFile(String icaUrl, String icaPath = "") {
			client.Headers[HttpRequestHeader.Accept] = "application/json, text/javascript, */*; q=0.01";
			client.Headers[HttpRequestHeader.Referer] = $"{baseUrl}/Citrix/StoreWeb/";
			client.Headers["Upgrade-Insecure-Requests"] = "1";
			client.Headers["X-Citrix-IsUsingHTTPS"] = "Yes";
			client.Headers["X-Requested-With"] = "XMLHttpRequest";
			client.Headers["Csrf-Token"] = csrfToken;
			String result = client.UploadString($"{baseUrl}/Citrix/StoreWeb/{icaUrl}", "");
			if (icaPath.Length == 0) {
				icaPath = Path.Combine(Path.GetTempPath(), GetRandomIcaFileName(icaUrl));
			}
			File.WriteAllText(icaPath, result);
			return icaPath;
		}

		#endregion


		#region Helper Methods

		/// <summary>
		/// Creates a random filename for a ica file.
		/// Uses the current time and url of the ica file to generate a hash from it.
		/// </summary>
		/// <param name="icaUrl"></param>
		/// <returns></returns>
		private String GetRandomIcaFileName(String icaUrl) {
			return Math.Abs((icaUrl + DateTime.Now.ToString()).GetHashCode()) + ".ica";
		}

		/// <summary>
		/// Creates a random device id for Citrix. It should start with WR_ and
		/// continue with 17 characters.
		/// </summary>
		/// <returns></returns>
		private String GenerateDeviceId() {
			return "WR_" + (new Guid()).ToString().Replace("-", "").Substring(0, 17);
		}

		/// <summary>
		/// Extracts the authentication method url from response headers of a
		/// web Client. Should be called just after a request that failed.
		/// </summary>
		/// <param name="Client"></param>
		/// <returns></returns>
		private String GetAuthenticationUrlFromHeader(EnhancedWebClient Client) {
			String header = Client.GetResponseHeader("CitrixWebReceiver-Authenticate");
			return GetAuthenticationUrlFromHeader(header);
		}

		/// <summary>
		/// Extracts the authentication method url from a CitrixWebReceiver-Authenticate
		/// response header.
		/// </summary>
		/// <param name="header"></param>
		/// <returns></returns>
		private String GetAuthenticationUrlFromHeader(String header) {
			Regex r = new Regex("location=\"([^\"]+)");
			Match match = r.Match(header);
			if (match.Success) {
				return match.Groups[1].Value;
			}
			return "";
		}

		#endregion
	}
}
