using System;
using System.Web;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;

using Vinco.Utils;


namespace Vinco.Handlers
{
    public abstract class RangeRequestHandler : IHttpHandler
    {
        private const byte MAX_MULTIPART_RANGES = 5;
        private const byte CHECK_INTERVAL = 30;
        private const string RANGE_BOUNDARY = "<q1w2e3r4t5y6u7i8o9p0zaxscdvfbgnhmjklkl>";
        private const string MULTIPART_CONTENT_TYPE = "multipart/byteranges; boundary=" + RANGE_BOUNDARY;

        #region IHttpHandler

        protected void ProcessRequest(HttpContext context)
        {
            HttpContextBase httpContext = new HttpContextWrapper(context);
            ProcessRequest(httpContext);
        }

        public virtual void ProcessRequest(HttpContextBase httpContext)
        {
            HttpRequestBase httpRequest = httpContext.Request;
            HttpResponseBase httpResponse = httpContext.Response;

            FindFileInfo(httpContext);

            byte calls = 0;
            Func<bool> OnContinue = () =>
            {
                if (calls == CHECK_INTERVAL)
                {
                    calls = 0;
                    if (!httpResponse.IsClientConnected)
                    {
                        httpResponse.Clear();
                        httpResponse.End();
                        return false;
                    }
                }
                calls++;
                return true;
            };

            bool valid = Validate(httpContext);
            if (valid)
            {
                long length = 0L;
                httpResponse.Clear();
                httpResponse.Buffer = false;
                Stream outputStream = httpResponse.OutputStream;

                Queue<ByteRange> rangeQueue = null;
                if (AcceptRanges && HttpHeaderHelper.IsRangeRequest(httpRequest))
                {
                    if (EnsureParseRanges(httpContext, out rangeQueue))
                    {
                        ByteRange range;
                        string contentType = MimeMapping.GetMimeMapping(RequestFileInfo.Name);

                        if (rangeQueue.Count > 1)
                        {
                            RangeResponseHeaders(httpContext);
                            httpResponse.ContentType = MULTIPART_CONTENT_TYPE;

                            if (HttpHeaderHelper.IsHttpGetMethod(httpRequest))
                            {
                                do
                                {
                                    range = rangeQueue.Dequeue();

                                    string headers = WriteMultipartDetails(range, contentType, RequestFileInfo.Length);
                                    httpResponse.Output.Write(headers);

                                    long rangeLength = (range.Length - range.Offset) + 1;
                                    length = Write(outputStream, range.Offset, rangeLength, OnContinue);

                                    httpResponse.Output.WriteLine();

                                } while (rangeQueue.Any());

                                httpResponse.Write("--" + RANGE_BOUNDARY + "--");
                            }
                        }
                        else
                        {
                            range = rangeQueue.Dequeue();
                            RangeResponseHeaders(httpContext);
                            httpResponse.ContentType = contentType;
                            long rangeLength = (range.Length - range.Offset) + 1;
                            httpResponse.AppendHeader("Content-Length", rangeLength.ToString());
                            httpResponse.AppendHeader("Content-Range", string.Format(CultureInfo.InvariantCulture, "bytes {0}-{1}/{2}", range.Offset, range.Length, RequestFileInfo.Length));

                            if (HttpHeaderHelper.IsHttpGetMethod(httpRequest))
                            {
                                length = Write(outputStream, range.Offset, rangeLength, OnContinue);
                            }
                        }
                    }
                    else
                    {
                        RequestedRangeNotSatisfiable(httpContext, RequestFileInfo.Length);
                    }
                }
                else
                {
                    NoRangeResponseHeaders(httpContext);
                    if (HttpHeaderHelper.IsHttpGetMethod(httpRequest))
                    {
                        length = Write(outputStream, 0, 0, OnContinue);
                    }
                }
                if (DownloadCompleted != null)
                {
                    DownloadCompleted();
                }
            }
        }

        public bool IsReusable
        {
            get { return false; }
        }

        void IHttpHandler.ProcessRequest(HttpContext context)
        {
            ProcessRequest(context);
        }

        #endregion

        protected abstract void FindFileInfo(HttpContextBase httpContext);

        protected abstract long Write(Stream stream, long offset, long length, Func<bool> continueAction);

        protected virtual string GetEtag()
        {
            return HttpHeaderHelper.GenerateETag(RequestFileInfo.LastModified, DateTime.Now);
        }

        #region Headers

        protected virtual void NoRangeResponseHeaders(HttpContextBase httpContext)
        {
            HttpResponseBase response = httpContext.Response;

            response.StatusCode = 200;
            response.ContentType = MimeMapping.GetMimeMapping(RequestFileInfo.Name);
            response.AddHeader("Accept-Ranges", this.AcceptRanges ? "bytes" : "none");
            response.AddHeader("Content-Length", RequestFileInfo.Length.ToString());

            response.Cache.SetLastModified(RequestFileInfo.LastModified);
            response.Cache.SetETag(GetEtag());
            response.Cache.SetExpires(DateTime.Now.AddDays(1.0));
            response.Cache.SetCacheability(HttpCacheability.Public);

            ContentDisposition disposition = new ContentDisposition()
            {
                FileName = RequestFileInfo.Name
            };
            string str = disposition.ToString();
            response.AddHeader("Content-Disposition", disposition.ToString());
        }

        protected virtual void RangeResponseHeaders(HttpContextBase httpContext)
        {
            HttpResponseBase response = httpContext.Response;

            response.StatusCode = 206;
            response.AppendHeader("Last-Modified", FormatHttpDateTime(RequestFileInfo.LastModified));
            response.AppendHeader("Accept-Ranges", this.AcceptRanges ? "bytes" : "none");
            response.AppendHeader("Cache-Control", "public");
            response.AppendHeader("ETag", GetEtag());
        }

        #endregion

        #region Validation

        protected virtual bool Validate(HttpContextBase httpContext)
        {
            Func<HttpContextBase, bool>[] validatorList =
            {
                EnsureValidMethods,
                EnsureFileExists,
                EnsureIfModifiedSinceHeader,
                EnsureIfUnmodifiedSinceHeader,
                EnsureIfMatchHeader,
                EnsureIfNoneMatchHeader,
                EnsureValidIfRange
            };
            foreach (var v in validatorList)
            {
                if (!v(httpContext))
                {
                    return false;
                }
            }
            return true;
        }

        protected virtual bool EnsureValidMethods(HttpContextBase httpContext)
        {
            HttpRequestBase request = httpContext.Request;
            HttpResponseBase response = httpContext.Response;

            if (!HttpHeaderHelper.IsHttpMethodAllowed(request))
            {
                response.StatusCode = 501;
                return false;
            }
            return true;
        }

        protected virtual bool EnsureFileExists(HttpContextBase httpContext)
        {
            HttpResponseBase response = httpContext.Response;
            if (RequestFileInfo == null)
            {
                response.StatusCode = 404;
                return false;
            }
            return true;
        }

        protected virtual bool EnsureIfModifiedSinceHeader(HttpContextBase httpContext)
        {
            HttpRequestBase request = httpContext.Request;
            HttpResponseBase response = httpContext.Response;

            if (!HttpHeaderHelper.IfModifiedSince(request, RequestFileInfo.LastModified))
            {
                response.StatusCode = 304; // Not Modified response

                return false;

            }
            return true;
        }

        protected virtual bool EnsureIfUnmodifiedSinceHeader(HttpContextBase httpContext)
        {
            HttpRequestBase request = httpContext.Request;
            HttpResponseBase response = httpContext.Response;

            if (!HttpHeaderHelper.IfUnmodifiedSince(request, RequestFileInfo.LastModified))
            {
                response.StatusCode = 412; // Precondition Failed response

                return false;
            }
            return true;
        }

        protected virtual bool EnsureIfMatchHeader(HttpContextBase httpContext)
        {
            HttpRequestBase request = httpContext.Request;
            HttpResponseBase response = httpContext.Response;

            if (!HttpHeaderHelper.IfMach(request, GetEtag()))
            {
                response.StatusCode = 412; // Precondition Failed response

                return false;
            }
            return true;
        }

        protected virtual bool EnsureIfNoneMatchHeader(HttpContextBase httpContext)
        {
            HttpRequestBase request = httpContext.Request;
            HttpResponseBase response = httpContext.Response;

            string value = HttpHeaderHelper.GetHeaderValue(request, "If-None-Match");
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
            if (value == "*")
            {
                response.StatusCode = 412;  // Precondition Failed

                return false;
            }
            string etag = GetEtag();
            IEnumerable<string> values = HttpHeaderHelper.GetHeaderValues(request, "If-None-Match");
            if (values.All(x => x != etag))
            {
                response.AppendHeader("Etag", etag);

                response.StatusCode = 304; // Not Modified

                return false;
            }
            return true;
        }

        protected virtual bool EnsureValidIfRange(HttpContextBase httpContext)
        {
            HttpRequestBase request = httpContext.Request;

            if (!HttpHeaderHelper.IsRangeRequest(request))
            {
                return true;
            }
            return HttpHeaderHelper.IfRange(request, GetEtag(), RequestFileInfo.LastModified);
        }

        #endregion

        #region Range methods

        protected virtual bool EnsureParseRanges(HttpContextBase httpContext, out Queue<ByteRange> rangeQueue)
        {
            HttpRequestBase request = httpContext.Request;

            if (!HttpHeaderHelper.IsRangeRequest(request))
            {
                throw new InvalidOperationException();
            }

            rangeQueue = null;
            const string RANGE = "bytes=";
            string rangeHeader = httpContext.Request.Headers["Range"];

            int index = rangeHeader.IndexOf(RANGE);
            string str = rangeHeader.Substring(index + RANGE.Length);
            string[] rangePairs = str.Split(",".ToCharArray());

            const byte OFFSET = 0;
            const byte LENGTH = 1;
            foreach (string item in rangePairs)
            {
                string[] ranges = item.Split('-');
                if (ranges.Length != 2)
                {
                    return false;
                }
                long length;
                string rangeString = ranges[LENGTH];
                if (string.IsNullOrWhiteSpace(rangeString))
                {
                    length = RequestFileInfo.Length - 1;
                }
                else
                {
                    if (!GetLongFromString(rangeString, out length))
                    {
                        return false;
                    }
                }

                long offset;
                rangeString = ranges[OFFSET];
                if (string.IsNullOrWhiteSpace(rangeString))
                {
                    offset = RequestFileInfo.Length - length;
                    length = RequestFileInfo.Length - 1;
                }
                else
                {
                    if (!GetLongFromString(rangeString, out offset))
                    {
                        return false;
                    }
                }
                if (rangeQueue == null)
                {
                    rangeQueue = new Queue<ByteRange>(5);
                }
                rangeQueue.Enqueue(new ByteRange { Offset = offset, Length = length });
            }
            bool valid = false;
            if (rangeQueue.Any())
            {
                valid = EnsureRangesValid(rangeQueue);
            }
            return valid;
        }

        protected virtual bool EnsureRangesValid(Queue<ByteRange> rangeQueue)
        {
            foreach (var v in rangeQueue)
            {
                if (!IsSatisfiableRange(v.Offset, RequestFileInfo.Length - 1) | !IsSatisfiableRange(v.Length, RequestFileInfo.Length - 1))
                {
                    return false;
                }
                if (v.Offset < 0 | v.Length < 0)
                {
                    return false;
                }
                if (v.Length < v.Offset)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region Private methods

        private static string WriteMultipartDetails(ByteRange range, string contentType, long fileLength)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Format("--{0}", RANGE_BOUNDARY));
            sb.AppendLine(string.Format("Content-Type: {0}", contentType));
            sb.AppendLine(string.Format("Content-Range: {0}", string.Format(CultureInfo.InvariantCulture, "bytes {0}-{1}/{2}", range.Offset, range.Length, fileLength)));
            sb.AppendLine();
            return sb.ToString();
        }

        private static bool IsSatisfiableRange(long range, long fileLength)
        {
            return range <= fileLength && fileLength > 0;
        }

        private static bool GetLongFromString(string value, out long result)
        {
            result = 0L;
            if (long.TryParse(value, out result))
            {
                return true;
            }
            return false;
        }

        private static void RequestedRangeNotSatisfiable(HttpContextBase httpContext, long length)
        {
            HttpResponseBase response = httpContext.Response;

            response.StatusCode = 416;
            response.ContentType = null;
            response.AppendHeader("Content-Range", string.Format("bytes */{0}", length.ToString(NumberFormatInfo.InvariantInfo)));
        }

        private static string FormatHttpDateTime(DateTime date)
        {
            date = date.ToUniversalTime();
            return date.ToString("R", DateTimeFormatInfo.InvariantInfo);
        }

        #endregion

        public IFileInfo RequestFileInfo { get; set; }

        public virtual bool AcceptRanges
        {
            get { return true; }
        }       
        
        public Action DownloadCompleted;
    }
}
