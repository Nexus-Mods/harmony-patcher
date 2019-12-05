using System;

using Newtonsoft.Json;

using VortexHarmonyExec;
namespace VortexHarmoyExec
{
    public class JSONResponse
    {
        public string Message { get; private set; }
        public int ErrorCode { get; private set; }
        public Exception RaisedException { get; private set; }

        internal static string CreateSerializedResponse(string message, Enums.EErrorCode code, Exception exc = null)
        {
            JSONResponse response = new JSONResponse();
            response.Message = message;
            response.ErrorCode = (int)(code);
            response.RaisedException = exc;

            return JsonConvert.SerializeObject(response);
        }
    }
}
