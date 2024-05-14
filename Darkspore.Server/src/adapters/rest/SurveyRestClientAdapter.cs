using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using HttpServer;

namespace HttpServer;

public class SurveyRestClientAdapter
{
    [ApiMethod(Name="api.survey.getSurveyList")]
    public byte[] getSurveyList(HttpListenerContext context)
    {
        var parameters = context.Request.QueryString;

        var response = new SurveyResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1,
            Surveys = SurveyService.getSurveyList()
        };

        return XmlUtils.Serialize(response);
    }
}
