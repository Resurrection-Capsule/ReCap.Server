using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using HttpServer;

namespace HttpServer;

[RestController(Value="/survey/api", ContentType="text/xml")]
public class SurveyRestClientAdapter
{
    public SurveyRestClientAdapter(SqliteConfig newSqliteConfig) {}

    [RequestMapping(Name="api.survey.getSurveyList")]
    public byte[] getSurveyList(HttpListenerContext context, Dictionary<string,string> parameters)
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
