using HttpServer;

namespace HttpServer;

public class ConfigService
{
    public static ConfigContract getGameConfig(string darksporeVersion) {
        string host = ServerConfig.GetHost();

        return new ConfigContract{
            BlazeServiceName = "darkspore", // Directly linked to BlazeServiceName
            BlazeSecure = "N", // Directly linked to BlazeSecure
            BlazeEnv = "prod", // Directly linked to BlazeEnvironment, can be { prod, beta, cert, test, dev }
            SporenetCdnHost = host,
            SporenetDbHost = host,
            SporenetDbName = "darkspore",
            SporenetHost = host,
            HttpSecure = "N",
            LiferayHost = host,
            LauncherAction = 2,
            LauncherUrl = $"http://{host}/bootstrap/launcher/?version={darksporeVersion}"
        };
    }
}