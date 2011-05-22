using System;
using System.Web;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;


namespace Vinco.Utils
{
    internal class HttpHeaderHelper
    {
        public static bool IfMach(HttpRequestBase request, string etag)
        {
            var values = GetHeaderValues(request, "If-Match");
            if (!values.Any())
            {
                return true;
            }
            if (values.Contains("*"))
            {
                return true;
            }
            return values.Any(x => x == etag);
        }

        public static bool IfModifiedSince(HttpRequestBase request, DateTime lastModified)
        {
            var value = GetHeaderValue(request, "If-Modified-Since");
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
            DateTime ifModifiedSince = DateTime.MinValue;
            if (!DateTime.TryParse(value, out ifModifiedSince))
            {
                return true;
            }
            DateTime info = lastModified;
            info = new DateTime(info.Year, info.Month, info.Day, info.Hour, info.Minute, info.Second, 0);
            ifModifiedSince = ifModifiedSince.ToUniversalTime();
            if (info <= ifModifiedSince)
            {
                return false;
            }
            return true;
        }

        public static bool IfUnmodifiedSince(HttpRequestBase request, DateTime lastModified)
        {
            var value = GetHeaderValue(request, "If-Unmodified-Since");
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
            DateTime ifUnModifiedSince = DateTime.MinValue;
            if (!DateTime.TryParse(value, out ifUnModifiedSince))
            {
                return true;
            }
            DateTime date = lastModified;
            date = new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, 0);
            ifUnModifiedSince = ifUnModifiedSince.ToUniversalTime();
            if (date <= ifUnModifiedSince)
            {
                return false;
            }
            return true;
        }

        public static bool IfRange(HttpRequestBase request, string etag, DateTime lastModified)
        {
            bool flag = false;
            string ifRangeHeader = GetHeaderValue(request, "If-Range");
            if (string.IsNullOrWhiteSpace(ifRangeHeader))
            {
                // TODO: Small hack

                ifRangeHeader = etag;
            }
            if ((ifRangeHeader != null) && (ifRangeHeader.Length > 1))
            {
                if (ifRangeHeader[0] == '"')
                {
                    if (ifRangeHeader == etag)
                    {
                        flag = true;
                    }
                }
                else
                {
                    if ((ifRangeHeader[0] == 'W') && (ifRangeHeader[1] == '/'))
                    {
                        flag = false;
                    }
                    if (!IsOutDated(ifRangeHeader, lastModified))
                    {
                        flag = true;
                    }
                }
            }
            return flag;
        }

        public static bool IsHttpGetMethod(HttpRequestBase httpRequest)
        {
            return string.Equals("GET", httpRequest.HttpMethod, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsHttpMethodAllowed(HttpRequestBase httpRequest)
        {
            return string.Equals("GET", httpRequest.HttpMethod, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals("HEAD", httpRequest.HttpMethod, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRangeRequest(HttpRequestBase request)
        {
            string rangeHeader = request.Headers["Range"];
            if (rangeHeader != null)
            {
                return rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        #region ETag helpers

        public static string GenerateETag(DateTime lastModified, DateTime now)
        {
            long lastModifiledFileTime = lastModified.ToFileTime();
            long dateNow = now.ToFileTime();
            string str = lastModifiledFileTime.ToString("X8", CultureInfo.InvariantCulture);
            if ((dateNow - lastModifiledFileTime) <= TimeSpan.TicksPerSecond) // There are 10 million ticks in one second.
            {
                return ("W/\"" + str + "\"");
            }
            return ("\"" + str + "\"");
        }

        public static string GenerateETag(params string[] items)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string s in items)
            {
                if ((sb.Length > 0) && (sb[sb.Length - 1] != '|'))
                {
                    sb.Append("|");
                }
                sb.Append(s);
            }
            ASCIIEncoding ascii = new ASCIIEncoding();
            byte[] bytes = ascii.GetBytes(sb.ToString());
            return Convert.ToBase64String(new MD5CryptoServiceProvider().ComputeHash(bytes));
        }

        public static string ToHttpEtag(string etagString, bool weak)
        {
            if (string.IsNullOrWhiteSpace(etagString))
            {
                throw new ArgumentNullException("etagString");
            }
            if (weak)
            {
                return ("W/\"" + etagString + "\"");
            }
            return ("\"" + etagString + "\"");
        } 

        #endregion

        #region Private methods

        public static string GetHeaderValue(HttpRequestBase request, string name)
        {
            return request.Headers[name];
        }

        public static IEnumerable<string> GetHeaderValues(HttpRequestBase request, string name)
        {
            string value = request.Headers[name];
            if (string.IsNullOrWhiteSpace(value))
            {
                return new string[0];
            }
            return value.Split(',');
        }

        private static bool IsOutDated(string ifRangeHeader, DateTime lastModified)
        {
            DateTime ifRangeDate;
            DateTime lastModifiedUni = lastModified.ToUniversalTime();

            DateTime.TryParse(ifRangeHeader, out ifRangeDate);
            if (ifRangeDate == DateTime.MinValue)
            {
                return true;
            }
            return ifRangeDate < lastModified;
        }

        #endregion
    }
}
