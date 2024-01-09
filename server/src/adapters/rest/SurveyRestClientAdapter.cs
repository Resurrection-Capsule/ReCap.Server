using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using HttpServer;

namespace HttpServer;

class SurveyRestClientAdapter
{
    [ApiMethod(Name="api.survey.getSurveyList")]
    public static byte[] getSurveyList(NameValueCollection parameters)
    {
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
