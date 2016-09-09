using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using Microsoft.IdentityModel.Protocols.WSTrust;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;

namespace SPSync.Core.Adfs
{
    /// <summary>
    /// This Helper class will be called in the ExecutingWebRequest event of the ClientContext object to attach an ADFS cookie to the context
    /// to allow authenticated calls to succeed.
    /// <example>
    /// ClientContext ctx = new ClientContext(webUrl);
    /// ctx.ExecutingWebRequest += new EventHandler<WebRequestEventArgs>(ctx_ExecutingWebRequest);
    /// void ctx_ExecutingWebRequest(object sender, WebRequestEventArgs e)
    ///    {
    ///        try
    ///        {
    ///            e.WebRequestExecutor.WebRequest.CookieContainer = Helper.AttachCookie(txtWctx.Text, txtWtrealm.Text, txtWreply.Text, txtcorpStsUrl.Text, txtUserId.Text,
    ///                  txtPassword.Text); 
    ///        ...
    /// </example>
    /// <author>Shailen Sukul</author>
    /// <date>Jul 29 2010</date>
    /// </summary>
    public class AdfsHelper
    {
        /* Store the cookie in a static container */
        private static CookieContainer cc = null;
        /* Store the current wtrealm so that the cookie can be invalidated when the realm changes */
        private static string _wtrealm = string.Empty;

        public static void InValidateCookie()
        {
            cc = null;
        }

        public static string GetCachedFedAuthCookieString(string webUrl)
        {
            var cookies = cc.GetCookies(new Uri(webUrl));
            if (cookies == null)
                return null;

            var c = cookies["FedAuth"];
            if (c == null || c.Expired)
                return null;

            return c.ToString();
        }

        /// <summary>
        /// This will locate the FedAuth cookie, add it to the cookie container and return it.
        /// </summary>
        /// <param name="wctx"></param>
        /// <param name="wtrealm"></param>
        /// <param name="wreply"></param>
        /// <param name="corpStsUrl"></param>
        /// <param name="userid"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static CookieContainer AttachCookie(string webUrl, string wctx, string wtrealm, string wreply, string corpStsUrl, string userid, string password)
        {
            if (cc == null || wtrealm != _wtrealm || cc.GetCookies(new Uri(webUrl))["FedAuth"] == null || cc.GetCookies(new Uri(webUrl))["FedAuth"].Expired)
            {
                try
                {
                    _wtrealm = wtrealm;
                    cc = new CookieContainer();
                    Cookie samlAuth = new Cookie("FedAuth", AdfsHelper.GetFedAuthCookie(wctx, wtrealm, wreply, corpStsUrl, userid, password));
                    samlAuth.Expires = DateTime.Now.AddHours(1);
                    samlAuth.Path = "/";
                    samlAuth.Secure = true;
                    samlAuth.HttpOnly = true;
                    Uri samlUri = new Uri(webUrl);
                    samlAuth.Domain = samlUri.Host;
                    cc.Add(samlAuth);
                }
                catch
                {
                    /* Invalidate Cookie */
                    InValidateCookie();
                    throw;
                }
            }

            return cc;
        }

        /// <summary>
        /// Make an ADFS call to get the FedAuth cookie
        /// </summary>
        /// <param name="wctx"></param>
        /// <param name="wtrealm"></param>
        /// <param name="wreply"></param>
        /// <param name="corpStsUrl"></param>
        /// <param name="userid"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private static string GetFedAuthCookie(string wctx, string wtrealm, string wreply, string corpStsUrl, string userid, string password)
        {
            var sharepointSite = new
            {
                Wctx = wctx,
                Wtrealm = wtrealm,
                Wreply = wreply
            };
            var credentials = new { Username = userid, Password = password };

            //
            // Get token from STS
            // 
            string stsResponse = AdfsHelper.GetResponse(
                corpStsUrl,
                sharepointSite.Wtrealm,
                credentials.Username,
                credentials.Password);

            //
            // Generate response to Sharepoint
            //
            string stringData = String.Format("wa=wsignin1.0&wctx={0}&wresult={1}",
                Uri.EscapeDataString(sharepointSite.Wctx),
                Uri.EscapeDataString(stsResponse));
            HttpWebRequest sharepointRequest = HttpWebRequest.Create(sharepointSite.Wreply) as HttpWebRequest;
            sharepointRequest.Method = "POST";
            sharepointRequest.ContentType = "application/x-www-form-urlencoded";
            sharepointRequest.CookieContainer = new CookieContainer();
            sharepointRequest.AllowAutoRedirect = false; // This is important
            Stream newStream = sharepointRequest.GetRequestStream();

            byte[] data = Encoding.UTF8.GetBytes(stringData);
            newStream.Write(data, 0, data.Length);
            newStream.Close();
            HttpWebResponse webResponse = sharepointRequest.GetResponse() as HttpWebResponse;
            string fedAuth = webResponse.Cookies["FedAuth"].Value;
            webResponse.Close();
            //todo: large cookie may be chunked: FedAuth, FedAuth1, FedAuth2, etc
            // Need to get all chunks and send back.

            return fedAuth;
        }

        /// <summary>
        /// Helper Method
        /// </summary>
        /// <param name="stsUrl"></param>
        /// <param name="realm"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private static string GetResponse(string stsUrl, string realm, string username, string password)
        {
            RequestSecurityToken rst = new RequestSecurityToken();
            rst.RequestType = WSTrustFeb2005Constants.RequestTypes.Issue;

            // 
            // Bearer token, no encryption
            // 
            rst.AppliesTo = new EndpointAddress(realm);
            rst.KeyType = WSTrustFeb2005Constants.KeyTypes.Bearer;

            WSTrustFeb2005RequestSerializer trustSerializer = new WSTrustFeb2005RequestSerializer();
            WSHttpBinding binding = new WSHttpBinding();
            binding.Security.Mode = SecurityMode.TransportWithMessageCredential;
            binding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            binding.Security.Message.EstablishSecurityContext = false;
            binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.None;
            EndpointAddress address = new EndpointAddress(stsUrl);

            WSTrustFeb2005ContractClient trustClient = new WSTrustFeb2005ContractClient(binding, address);
            trustClient.ClientCredentials.UserName.UserName = username;
            trustClient.ClientCredentials.UserName.Password = password;
            Message response = trustClient.EndIssue(trustClient.BeginIssue(Message.CreateMessage(MessageVersion.Default, WSTrustFeb2005Constants.Actions.Issue, new RequestBodyWriter(trustSerializer, rst)), null, null));
            trustClient.Close();

            XmlDictionaryReader reader = response.GetReaderAtBodyContents();
            return reader.ReadOuterXml();
        }
    }
}
