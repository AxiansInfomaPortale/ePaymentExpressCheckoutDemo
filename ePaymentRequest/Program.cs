using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace ePaymentRequest
{
    [DataContract]
    public class AvailablePaymentProvider
    {
        [DataMember(Name = "accountid")]
        public int AccountID;

        [DataMember(Name = "minvalue")]
        public decimal MinValue;

        [DataMember(Name = "minvaluewithoutsignature")]
        public decimal MinValueWithoutSignature;

        [DataMember(Name = "maxvalue")]
        public decimal MaxValue;

        [DataMember(Name = "provider")]
        public string Description;
    }

    public class Program
    {
        private static readonly string postSetExpressCheckout = "METHOD=SetExpressCheckout&APPCODE={0}&VERSION={1}&CURRENCYCODE=EUR&RETURNURL={2}&CANCELURL={3}&ACTION=Sale&LOCALCODE=DE&DESC={4}&COMMUNE={5}&NOTIFYURL={6}&ERRORURL={7}";
        private static readonly string postGetProviders = "APPCODE={0}&COMMUNE={1}";
        private static readonly string appCode = "yourappcodehere";
        private static readonly string password = "yourpasswordhere";
        private static readonly string ePaymentUrl = "theurloftheepaymentportalhere";
        private static readonly int communeId = 0; //your commune id here

        static void Main(string[] args)
        {
            List<AvailablePaymentProvider> availablePaymentProviders = GetAvailableProviderRequest();

            if (SendCheckoutRequest(out string location, out string errorMessage, out string token))
            {
                //redirect user to location
            }
        }


        internal static bool SendCheckoutRequest(out string location, out string errorMessage, out string token)
        {
            errorMessage = string.Empty;
            token = string.Empty;
            location = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(appCode) || string.IsNullOrEmpty(password))
                    return false;

                //The baseurl of your application to handle repsonses from the payment portal
                string baseUrl = "";

                string version = "1.0";
                //The url to be notified on successful payments
                string returnUrl = string.Format("{0}/Checkout/PaymentCheckoutOK", baseUrl);
                //The url to be notified on user cancel
                string cancelUrl = string.Format("{0}/Checkout/PaymentCheckoutCancel", baseUrl);
                //The url to be notified on payments
                string notifyUrl = string.Format("{0}/api/Config/NotifyPayment", baseUrl); ;
                //The url to be notified on errors
                string errorUrl = string.Format("{0}/api/Config/ErrorPayment", baseUrl); ;
                string description = string.Empty;

                string url = string.Format("{0}api/Config/SetExpressCheckout", ePaymentUrl);

                description = HttpUtility.UrlEncode("Ich bin eine Testzahlung");

                if (string.IsNullOrEmpty(description))
                    return false;

                string postData = string.Format(postSetExpressCheckout, appCode,version, returnUrl, cancelUrl, description, communeId, notifyUrl, errorUrl);

                decimal totalAmount = 0;

                totalAmount += Convert.ToDecimal("1,000.21", CultureInfo.GetCultureInfo("en-US"));
                string lineText = "&L_PAYMENTREQUEST_NAME_{0}={1}&L_PAYMENTREQUEST_AMT_{0}={2}";
                postData += string.Format(lineText, 0, HttpUtility.UrlEncode("Ich bin die Zeile"), totalAmount.ToString(CultureInfo.GetCultureInfo("en-US")));

                if (totalAmount <= 0)
                    return false;

                postData += string.Format("&AMT={0}", totalAmount.ToString(CultureInfo.GetCultureInfo("en-US")));
                postData += string.Format("&A_PAYMENTREQUEST_ACCOUNT_0=1&A_PAYMENTREQUEST_ACCOUNT_1=3&MYUSERID={0}", "someData");

                NameValueCollection nvc = HttpUtility.ParseQueryString(postData);

                string value = string.Empty;
                foreach(string key in nvc.AllKeys)
                    value += nvc.Get(key);

                postData += string.Format("&HASH={0}", CreateHash(value));

                RequestManager requestManager = new RequestManager();
                HttpWebRequest webRequest = requestManager.CreateWebRequest(url, postData);
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                requestManager.WriteData(webRequest, postData);
                string responseData = requestManager.ReadData(webRequest);

                Dictionary<string, string> parameters = GetURLParameters(responseData);

                bool success = false;
                if (parameters.ContainsKey("ACK"))
                {
                    if (parameters["ACK"].ToLower().Equals("success"))
                        success = true;
                }

                if (success)
                {
                    if (parameters.ContainsKey("TOKEN"))
                    {
                        token = parameters["TOKEN"];
                    }

                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        internal static List<AvailablePaymentProvider> GetAvailableProviderRequest()
        {
            try
            {
                if (string.IsNullOrEmpty(appCode) || string.IsNullOrEmpty(password))
                    throw new Exception("Invalid authentification data");

                string url = string.Format("{0}api/Config/GetAvailablePaymentProviders", ePaymentUrl);

                string postData = string.Format(postGetProviders, appCode, communeId);
                NameValueCollection nvc = System.Web.HttpUtility.ParseQueryString(postData);

                string value = string.Empty;
                foreach (string key in nvc.AllKeys)
                    value += nvc.Get(key);

                postData += string.Format("&HASH={0}", CreateHash(value));

                RequestManager requestManager = new RequestManager();
                HttpWebRequest webRequest = requestManager.CreateWebRequest(url, postData);
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                requestManager.WriteData(webRequest, postData);
                string responseData = requestManager.ReadData(webRequest);
                var obj = JObject.Parse(responseData);
                if (obj["error"] != null)
                    throw new Exception(obj["error"].ToString());

                JArray providers = (JArray)obj["providers"];
                return JsonConvert.DeserializeObject<List<AvailablePaymentProvider>>(providers.ToString());
            }
            catch (Exception e)
            {

            }
            return null;
        }

        private static string CreateHash(string value)
        {
            HMACMD5 md5 = new HMACMD5((Encoding.UTF8.GetBytes(password)));
            byte[] computedHash = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
            return BitConverter.ToString(computedHash).Replace("-", string.Empty);
        }

        internal static Dictionary<string, string> GetURLParameters(string urlData)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            if (urlData == null)
                return result;

            string[] list = urlData.Split('&');

            foreach (string s in list)
            {
                string[] sublist = s.Split('=');
                if (sublist.Length == 2)
                {
                    string key = WebUtility.UrlEncode(sublist[0].ToUpper());
                    string value = WebUtility.UrlDecode(sublist[1]);

                    result.Add(key, value);
                }
            }
            return result;
        }
    }
}

