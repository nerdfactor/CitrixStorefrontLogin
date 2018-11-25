using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace CitrixStorefrontLogin {
	/// <summary>
	/// Enhanced WebClient that stores cookies from previous requests
	/// and allows simple use of a client certificate to authenticate
	/// requests.
	/// </summary>
	public class EnhancedWebClient : WebClient {

		/// <summary>
		/// A client certificate used to authenticate the requests.
		/// </summary>
		public X509Certificate2 Certificate { get; set; }

		/// <summary>
		/// A container containing all the cookies from previous
		/// requests.
		/// </summary>
		public CookieContainer CookieContainer { get; set; }

		public EnhancedWebClient() : this(new CookieContainer()) { }
		public EnhancedWebClient(String certPath, String certPw) : this(new CookieContainer(), certPath, certPw){}

		public EnhancedWebClient(CookieContainer container, String certPath = "", String certPw = "") {
			CookieContainer = container;
			LoadClientCertificate(certPath, certPw);

			// Will make sure that TLS 1.2 is used.
			// This is normaly not the default on Windows and newer version
			// of Netscaler will most likely require TLS 1.2. So disable it
			// if it causes problems with your setup.
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
		}

		public void LoadClientCertificate(String certPath, String certPw) {
			if(certPath.Length == 0 || certPw.Length == 0 || !File.Exists(certPath)) { return; }
			Certificate = new X509Certificate2(certPath, certPw);
		}

		/// <summary>
		/// Override the web request of the client, to set some important settings
		/// that each request needs:
		/// - Use a client certificate if the server needs it for authentification
		/// - Set the redirect following option because it will mess with Citrix cookies
		/// - Use the cookies from previous requests to make Citrix happy
		/// - Set the user agent to somethin usefull that is not suspicious to Citrix
		/// </summary>
		/// <param name="address"></param>
		/// <returns></returns>
		protected override WebRequest GetWebRequest(Uri address) {
			HttpWebRequest request = base.GetWebRequest(address) as HttpWebRequest;
			request.ClientCertificates.Add(Certificate);
			request.AllowAutoRedirect = false;
			request.CookieContainer = CookieContainer;
			request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/535.2 (KHTML, like Gecko) Chrome/15.0.874.121 Safari/535.2";
			return request;
		}

		/// <summary>
		/// Make sure, that the cookies from the response get stored for the following
		/// requests.
		/// </summary>
		/// <param name="request"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result) {
			return GetWebResponse(request);
		}

		/// <summary>
		/// Make sure, that the cookies from the response get stored for the following
		/// requests.
		/// </summary>
		/// <param name="request"></param>
		/// <returns></returns>
		protected override WebResponse GetWebResponse(WebRequest request) {
			WebResponse response = base.GetWebResponse(request);
			ReadCookies(response as HttpWebResponse);
			return response;
		}

		/// <summary>
		/// Read the cookies inside a WebResponse and add them to the container
		/// in order to save them for future requests.
		/// </summary>
		/// <param name="response"></param>
		private void ReadCookies(HttpWebResponse response) {
			if(response != null) {
				CookieCollection cookies = response.Cookies;
				CookieContainer?.Add(cookies);
			}		
		}

		/// <summary>
		/// Manually add a cookie to the container
		/// </summary>
		/// <param name="c"></param>
		public void AddCookie(Cookie c) {
			CookieContainer?.Add(c);
		}

		/// <summary>
		/// Get all cookies in the container. They can be for different Uri.
		/// </summary>
		/// <returns></returns>
		public Dictionary<String, Cookie> GetAllCookies() {
			Dictionary<String, Cookie> cookies = new Dictionary<String, Cookie>();
			if(CookieContainer == null) { return cookies; }

			// Get the domains in the cookie container
			var domainTable = CookieContainer
				.GetType()
				.GetRuntimeFields()
				.FirstOrDefault(x => x.Name == "m_domainTable");
			var domains = domainTable.GetValue(CookieContainer) as IDictionary;

			// And than load all the cookie collections for those domains.
			foreach (var domain in domains.Values) {
				var type = domain
					.GetType()
					.GetRuntimeFields()
					.First(x => x.Name == "m_list");
				var values = (IDictionary)type.GetValue(domain);
				foreach (CookieCollection col in values.Values) {
					foreach(Cookie co in col) {
						cookies.Add(co.Name, co);
					}
				}
			}

			return cookies;
		}

		/// <summary>
		/// Get the value for a response header.
		/// </summary>
		/// <param name="header"></param>
		/// <returns></returns>
		public String GetResponseHeader(String header) {
			var heeader = ResponseHeaders;
			for (int i = 0; i < ResponseHeaders.Count; i++) {
				if(ResponseHeaders.GetKey(i) == header) { return ResponseHeaders.Get(i); }
			}
			return "";
		}
	}
}
