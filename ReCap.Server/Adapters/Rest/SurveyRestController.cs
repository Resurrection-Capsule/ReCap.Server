using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using ReCap.Server.Adapters.Rest.Contracts.Survey;
using ReCap.Server.Config;
using ReCap.Server.Services;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.Rest.Api;

[RestController(Value="/survey/api", ContentType="text/xml")]
public class SurveyRestController
{
    public SurveyRestController(SqliteConfig newSqliteConfig) {}

    [RequestMapping(Name="api.survey.getSurveyList")]
    public byte[] getSurveyList(HttpListenerContext context, Dictionary<string,string> parameters)
    {
        var response = new SurveyResponseContract{
            Stat = "ok",
            Version = ServerConfig.GameVersionStr,
            Timestamp = 1,
            ExecTime = 1,
            Surveys = SurveyService.getSurveyList()
        };

        return XmlHelper.Serialize(response);
    }
}
