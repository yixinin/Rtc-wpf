using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Web.Http;

namespace Rtc
{
    public class Http
    {
        const string BaseURL = "http://localhost:8080/";

        public async static Task<string> PostAsnyc(object m, string url)
        {
            var httpClient = new HttpClient();
            var text = JsonConvert.SerializeObject(m);
            var content = new HttpStringContent(text);
            content.Headers.ContentType = new Windows.Web.Http.Headers.HttpMediaTypeHeaderValue("application/json");

            try
            {
                var resp = await httpClient.PostAsync(new Uri(BaseURL + url), content);
                if (resp.StatusCode != HttpStatusCode.Ok)
                {
                    Debug.WriteLine("http post code:" + resp.StatusCode);
                    return "";
                }
                var x = await resp.Content.ReadAsStringAsync();
                return x;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("http post ex:" + ex);
            }

            return "";
        }
    }
}
