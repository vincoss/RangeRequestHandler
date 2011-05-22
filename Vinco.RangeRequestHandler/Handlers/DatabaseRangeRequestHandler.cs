using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using Vinco.Data;


namespace Vinco.Handlers
{
    public class DatabaseRangeRequestHandler : RangeRequestHandler
    {
        private int _fileId;
        
        protected override void FindFileInfo(HttpContextBase httpContext)
        {
            HttpRequestBase request = httpContext.Request;
            string physicalPath = request.PhysicalPath;
            string fileName = Path.GetFileName(physicalPath);

            // TODO: Throws if file does not exists

            dynamic databaseFileInfo  = SqlHelper.FindFile(fileName).RecordToExpando();
            _fileId = databaseFileInfo.Id;

            RequestFileInfo = new DatabaseFileInfo(databaseFileInfo.Name, databaseFileInfo.Length, databaseFileInfo.DateModified);
        }

        protected override long Write(Stream stream, long offset, long length, Func<bool> continueAction)
        {
           return SqlHelper.Read(_fileId, stream, offset, length, continueAction);
        }
    }
}