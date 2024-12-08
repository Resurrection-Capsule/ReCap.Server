namespace ReCap.Server.Adapters.Rest.Api;

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using ReCap.Server.Adapters.Rest.Api;
using ReCap.Server.Adapters.Rest.Contracts.Survey.SurveyResponse;
using ReCap.Server.Config.Server;
using ReCap.Server.Config.Sqlite;
using ReCap.Server.Service.Survey;
using ReCap.Server.Utils.Xml;

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

        return XmlUtils.Serialize(response);
    }
}
