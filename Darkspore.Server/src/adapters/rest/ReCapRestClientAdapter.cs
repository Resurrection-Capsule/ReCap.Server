using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using HttpServer;

namespace HttpServer;

public class ReCapRestClientAdapter
{
    private AccountService accountService;

    public ReCapRestClientAdapter(SqliteConfig newSqliteConfig) {
        accountService = new AccountService(newSqliteConfig);
    }

    private string getBodyFromRequest(HttpListenerContext context)
    {
        var request = context.Request;
        if (!request.HasEntityBody)
        {
            return "";
        }
        System.IO.Stream body = request.InputStream;
        System.Text.Encoding encoding = request.ContentEncoding;
        System.IO.StreamReader reader = new System.IO.StreamReader(body, encoding);
        string s = reader.ReadToEnd();
        body.Close();
        reader.Close();
        return s;
    }

    [ApiMethod(Name="api.game.log")]
    public byte[] log(HttpListenerContext context)
    {
        string message = getBodyFromRequest(context);
        Console.WriteLine(message);
        return new byte[]{};
    }

    [ApiMethod(Name="api.game.registration")]
    public byte[] registerUser(HttpListenerContext context)
    {
        var request = context.Request;
        var parameters = request.QueryString;
        string email = parameters.Get("email");
        string name = parameters.Get("name");
        string password = parameters.Get("pass");
        int avatar = Int32.Parse(parameters.Get("avatar"));

        // TODO: Unlocking everything from start to test; make that configurable through parameters
        var account = accountService.createAccount(email, name, password, avatar, true);

        // auto actualPartsSize = Repository::CreatureParts::ListAll().size();
		// auto parts = Repository::Parts::ListAll();
		// uint64_t index = 1;
		// for (auto& part : parts) {
		// 	Repository::CreatureParts::Add(std::make_shared<Game::CreaturePart>(actualPartsSize + index++, part->rigblock_asset_id, user->get_account().id));
		// }
		// Repository::CreatureParts::Save();

		// // TODO: Unlocking all creatures from start to test; remove that in the future
		// std::vector<Repository::CreatureTemplatePtr> templates = Repository::CreatureTemplates::ListAll();
		// user->get_account().creatureRewards = templates.size();
		// for (auto& templateCreature : templates) {
		// 	user->UnlockCreature(templateCreature->id);
		// }

		// for (uint16_t squadSlot = 1; squadSlot <= 3; squadSlot++) {
		// 	uint16_t templateId = squadSlot - 1;
		// 	Squad squad1;
		// 	squad1.id = squadSlot;
		// 	squad1.slot = squadSlot;
		// 	squad1.name = "Slot " + std::to_string(squadSlot);
		// 	squad1.locked = false;
		// 	squad1.creatures.Add(templates[templateId]->id);
		// 	user->get_squads().data().push_back(squad1);
		// }

		// Repository::Users::SaveUser(user);

        var response = new ResponseContract{
            Stat = "ok",
            Version = ServerConfig.GetDarksporeVersion(),
            Timestamp = 1,
            ExecTime = 1
        };

        return XmlUtils.Serialize(response);
    }
}
