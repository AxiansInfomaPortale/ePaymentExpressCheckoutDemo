using System.IO;
using System.Net;
using System.Text;

namespace ePaymentRequest
{
    internal class RequestManager
    {
        internal void WriteData(HttpWebRequest webRequest, string postData)
        {
            using (Stream requestWriter = webRequest.GetRequestStream())
            {
                byte[] requestBytes = new UTF8Encoding().GetBytes(postData);
                requestWriter.Write(requestBytes, 0, requestBytes.Length);
            }
        }

        internal HttpWebRequest CreateWebRequest(string url, string postData)
        {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            return webRequest;
        }

        internal string ReadData(HttpWebRequest webRequest)
        {
            string responseData = string.Empty;
            HttpWebResponse resp = (HttpWebResponse)webRequest.GetResponse();

            using (Stream responseReader = resp.GetResponseStream())
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    responseReader.CopyTo(ms);
                    ms.Position = 0;
                    responseData = new UTF8Encoding().GetString(ms.ToArray());
                }
            }
            return responseData;
        }
    }
}