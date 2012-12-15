using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Text;

namespace Cadenza.Net
{
	public class WebDavMethodBuilder {

		Uri server;
		public Uri Server {
			get {return server;}
			set {
				if (!value.AbsolutePath.EndsWith ("/")) {
					var b = new UriBuilder (value);
					b.Path += "/";
					value = b.Uri;
				}
				server = value;
			}
		}
		public NetworkCredential Credentials {get; set;}
		public IDictionary<string, string> RequestHeaders {get; set;}
		public TextWriter Log {get; set;}
		
		public Task<WebDavPropertyFindMethod> CreateFileStatusMethodAsync (string path = null, int? depth = null, XElement request = null)
		{
			request = request ?? new XElement (WebDavNames.Propfind,
					new XElement (WebDavNames.Prop,
			              new XElement (WebDavNames.CreationDate),
			              new XElement (WebDavNames.ResourceType),
			              new XElement (WebDavNames.GetContentLength)));
			return CreatePropertyFindMethodAsync (path, depth, request);
		}

		public Task<WebDavPropertyFindMethod> CreatePropertyFindMethodAsync (string path = null, int? depth = null, XElement request = null)
		{
			depth = depth ?? 1;
			request = request ?? new XElement (WebDavNames.Propfind,
					new XElement (WebDavNames.Propname));
			var uri = CreateUri (path);
			return CreateMethodAsync (new WebDavPropertyFindMethod (uri, ToStream (request)), uri, WebDavMethods.PropertyFind,
					GetRequestHeaders ("Depth", depth == -1 ? "infinity" : depth.ToString ()));
		}

		Uri CreateUri (string path)
		{
			path = path ?? "";
			var p = new Uri (path, UriKind.Relative);
			return new Uri (Server, p);
		}

		static Stream ToStream (XElement e)
		{
			return new MemoryStream (Encoding.UTF8.GetBytes (e.ToString ()));
		}

		static IDictionary<string, string> GetRequestHeaders (params string[] extra)
		{
			if (extra == null || extra.Length == 0)
				return null;
			var n = new Dictionary<string, string> ();
			for (int i = 0; i < extra.Length; i += 2)
				n [extra [i]] = extra [i + 1];
			return n;
		}

		Task<TResult> CreateMethodAsync<TResult> (TResult result, Uri uri, string requestMethod, IDictionary<string, string> headers)
			where TResult : WebDavMethod
		{
			var request = (HttpWebRequest) HttpWebRequest.Create (uri);

			if (Credentials != null) {
				request.Credentials      = Credentials;
				request.PreAuthenticate  = true;
			}

			request.Method = requestMethod;

			AddHeaders (request.Headers, RequestHeaders);
			AddHeaders (request.Headers, headers);

			/*
             * The following line fixes an authentication problem explained here:
             * http://www.devnewsgroups.net/dotnetframework/t9525-http-protocol-violation-long.aspx
             */
			System.Net.ServicePointManager.Expect100Continue = false;

			result.Request = request;
			result.Builder = this;

			if (Log != null) {
				Log.WriteLine ("{0} {1}", request.Method, uri.AbsolutePath);
			}

			return Task<TResult>.Factory.StartNew (() => {
				result.UploadContentAsync ().Wait ();
				result.GetResponseAsync ().Wait ();
				return result;
			});
		}

		static void AddHeaders (WebHeaderCollection headers, IDictionary<string, string> add)
		{
			if (add == null)
				return;
			foreach (var e in add) {
				headers [e.Key] = e.Value;
			}
		}
	}
}

