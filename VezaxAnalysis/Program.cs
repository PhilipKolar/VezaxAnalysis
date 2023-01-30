using System.CommandLine;
using RestSharp;
using Newtonsoft.Json.Linq;
using System.IO;

namespace VezaxAnalysis
{
    public class DamageTaken
    {
        public int playerId;
        public int damage;
        public int timestamp;
    }

    public class DebuffPeriod
    {
        public int playerId;
        public int start;
        public int end;
    }

    public class FriendlyFireDealt
    {
        public int playerId;
        public string playerName;
        public int damageDone;
        public int timesReceivedDebuff;
    }

    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var apiKeyOption = new Option<string>(
                name: "--apiKey",
                description: "Your personal warcraftlogs api key");
            var logIdOption = new Option<string>(
                name: "--logId",
                description: "The ID of the log you want to analyse (retrieve it from the URL in a browser)");
            var onlyKillOption = new Option<bool>(
                name: "--onlyKill",
                description: "Only analyse the succesful kill attempt on vezax (if available) instead of including wipes.",
                getDefaultValue: () => false);
            var wipeGracePeriodOption = new Option<int>(
                name: "--wipeGracePeriod",
                description: "The time (in seconds) that in a wipe is excluded from the end of the log, e.g. in case people were stacking to wipe faster",
                getDefaultValue: () => 15);
            var OutputFileOption = new Option<string>(
                name: "--outputFile",
                description: "The file to write the results to, in CSV format. This will overwrite any file with the same name",
                getDefaultValue: () => "");

            var rootCommand = new RootCommand("Sample app for System.CommandLine");
            rootCommand.AddOption(apiKeyOption);
            rootCommand.AddOption(logIdOption);
            rootCommand.AddOption(onlyKillOption);
            rootCommand.AddOption(wipeGracePeriodOption);
            rootCommand.AddOption(OutputFileOption);
            rootCommand.SetHandler((key, logId, onlyKill, wipeGracePeriod, outputFile) =>
                {
                    Task.WaitAll(RunVezaxAnalysis(key, logId, onlyKill, wipeGracePeriod, outputFile));
                },
                apiKeyOption, logIdOption, onlyKillOption, wipeGracePeriodOption, OutputFileOption);

            return await rootCommand.InvokeAsync(args);
        }

        public static async Task RunVezaxAnalysis(string apiKey, string logID, bool onlyKill, int wipeGracePeriod, string outputFile)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Api Key is required");
                return;
            }
            if (string.IsNullOrEmpty(logID))
            {
                Console.WriteLine("Log ID is required");
                return;
            }

            const int markOfTheFacelessDebuffId = 63276;
            const int markOfTheFacelessDamageId = 63278;

            var client = new RestClient("https://www.warcraftlogs.com:443");
            var fightsRequest = new RestRequest($"/v1/report/fights/{logID}?api_key={apiKey}");
            var fightsResponse = await client.GetAsync(fightsRequest, CancellationToken.None);

            var playerDictionary = GetPlayerDictionary(fightsResponse);
            var vezaxAttempts = JObject.Parse(fightsResponse.Content)["fights"].Where(x => x.Value<int>("boss") != 0 && x.Value<string>("name") == "General Vezax").ToList();
            var friendlyFireDealt = new List<FriendlyFireDealt>();
            foreach (var vezaxAttempt in vezaxAttempts)
            {
                var startTime = vezaxAttempt.Value<int>("start_time");
                var endTime = vezaxAttempt.Value<int>("end_time");
                var kill = vezaxAttempt.Value<bool>("kill");
                if (!kill)
                {
                    if (onlyKill)
                    {
                        continue;
                    }
                    endTime -= wipeGracePeriod * 1000; //buffer time (in milliseconds) at the end of any wipes, in case people were stacking to wipe.
                }

                var damageTakenRequest = new RestRequest($"/v1/report/events/damage-taken/{logID}?api_key={apiKey}&start={startTime}&end={endTime}&abilityid={markOfTheFacelessDamageId}");
                var damageTakenResponse = await client.GetAsync(damageTakenRequest, CancellationToken.None);
                var damageTakenList = GetDamageTakenList(damageTakenResponse);

                var debuffRequest = new RestRequest($"/v1/report/events/debuffs/{logID}?api_key={apiKey}&start={startTime}&end={endTime}&abilityid={markOfTheFacelessDebuffId}");
                var debuffResponse = await client.GetAsync(debuffRequest, CancellationToken.None);
                var debuffList = GetDebuffList(debuffResponse);

                var friendlyFireDealtAttempt = GetFriendlyFireDealt(debuffList, damageTakenList);

                MergeLists(friendlyFireDealt, friendlyFireDealtAttempt);
            }

            friendlyFireDealt = friendlyFireDealt.OrderByDescending(x => x.damageDone).ToList();
            EnrichWithPlayerNames(friendlyFireDealt, playerDictionary);

            PrintResults(friendlyFireDealt);

            if (!string.IsNullOrEmpty(outputFile))
            {
                await OutputToCsv(friendlyFireDealt, outputFile);
            }
        }

        private static async Task OutputToCsv(List<FriendlyFireDealt> friendlyFireDealt, string outputFile)
        {
            var outputStrings = new List<string>();
            outputStrings.Add("Player,Number of Debuffs,Friendly Fire Dealt (pre-mitigation),Vezax Healed (pre-reductions)");
            foreach (var ff in friendlyFireDealt)
            {
                outputStrings.Add($"{ff.playerName},{ff.timesReceivedDebuff},{ff.damageDone},{ff.damageDone / 5000 * 100000}");
            }

            try
            {
                await File.WriteAllLinesAsync(outputFile, outputStrings.ToArray());
                Console.WriteLine($"Results have been written to {outputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not write results to file. Error message: {ex.Message}");
            }
        }

        private static void MergeLists(List<FriendlyFireDealt> friendlyFireDealt, List<FriendlyFireDealt> friendlyFireDealtAttempt)
        {
            if (friendlyFireDealt.Count == 0)
            {
                foreach (var ff in friendlyFireDealtAttempt)
                {
                    friendlyFireDealt.Add(ff);
                }
                return;
            }

            foreach (var ff in friendlyFireDealtAttempt)
            {
                var ffTotal = friendlyFireDealt.FirstOrDefault(x => x.playerId == ff.playerId);
                if (ffTotal == null)
                {
                    friendlyFireDealt.Add(ff);
                }
                else
                {
                    ffTotal.damageDone += ff.damageDone;
                    ffTotal.timesReceivedDebuff += ff.timesReceivedDebuff;
                }
            }
        }

        private static void PrintResults(List<FriendlyFireDealt> friendlyFireDealt)
        {
            Console.WriteLine("*** GENERAL VEZAX ***");
            Console.WriteLine("Player      | # Debuffs | Friendly Fire Dealt (pre-mit) | Vezax Healed (pre-reductions)");
            Console.WriteLine("------------+-----------+-------------------------------+------------------------------");
            foreach (var ff in friendlyFireDealt)
            {
                Console.WriteLine(String.Format("{0,-12}| {1,-9} | {2,-29:n0} | {3:n0}", ff.playerName, ff.timesReceivedDebuff, ff.damageDone, ff.damageDone / 5000 * 100000));
            }
        }

        private static void EnrichWithPlayerNames(List<FriendlyFireDealt> friendlyFireDealt, Dictionary<int, string> playerDictionary)
        {
            foreach (var ff in friendlyFireDealt)
            {
                ff.playerName = playerDictionary[ff.playerId];
            }
        }

        private static List<FriendlyFireDealt> GetFriendlyFireDealt(List<DebuffPeriod> debuffPeriods, List<DamageTaken> damageTaken)
        {
            var friendlyFireDealtList = new List<FriendlyFireDealt>();
            foreach (var debuffPeriod in debuffPeriods)
            {
                var totalDamage = damageTaken.Where(x => x.timestamp > debuffPeriod.start && x.timestamp < debuffPeriod.end).Sum(x => x.damage);
                if (friendlyFireDealtList.Any(x => x.playerId == debuffPeriod.playerId))
                {
                    var ff = friendlyFireDealtList.FirstOrDefault(x => x.playerId == debuffPeriod.playerId);
                    ff.damageDone += totalDamage;
                    ff.timesReceivedDebuff++;
                }
                else
                {
                    friendlyFireDealtList.Add(new FriendlyFireDealt
                    {
                        damageDone = totalDamage,
                        playerId = debuffPeriod.playerId,
                        timesReceivedDebuff = 1
                    });
                }
            }
            return friendlyFireDealtList;
        }

        private static List<DebuffPeriod> GetDebuffList(RestResponse debuffResponse)
        {
            var debuffListJson = JObject.Parse(debuffResponse.Content)["events"].ToList();
            var debuffList = new List<DebuffPeriod>();
            foreach (var debuff in debuffListJson)
            {
                var type = debuff.Value<string>("type");
                if (type == "applydebuff")
                {
                    debuffList.Add(new DebuffPeriod
                    {
                        playerId = debuff.Value<int>("targetID"),
                        start = debuff.Value<int>("timestamp"),
                        end = int.MaxValue
                    });
                }
                else if (type == "removedebuff")
                {
                    debuffList.Last().end = debuff.Value<int>("timestamp") + 3000; // Adding a 3s buffer since damage has a travel time, it can happen after the end of the debuff. there is a ~10s gap between marks so this won't crossover
                }
            }
            return debuffList;
        }

        private static List<DamageTaken> GetDamageTakenList(RestResponse damageTakenResponse)
        {
            var damageTakenListJson = JObject.Parse(damageTakenResponse.Content)["events"].ToList();
            var damageTakenList = new List<DamageTaken>();
            foreach (var damageTaken in damageTakenListJson)
            {
                damageTakenList.Add(new DamageTaken
                {
                    damage = damageTaken.Value<int>("unmitigatedAmount"),
                    playerId = damageTaken.Value<int>("targetID"),
                    timestamp = damageTaken.Value<int>("timestamp")
                });
            }
            return damageTakenList;
        }

        private static Dictionary<int, string> GetPlayerDictionary(RestResponse response)
        {
            var playerList = JObject.Parse(response.Content)["friendlies"].ToList();
            var playerDictionary = new Dictionary<int, string>();
            foreach (var player in playerList)
            {
                playerDictionary.Add(player.Value<int>("id"), player.Value<string>("name"));
            }
            return playerDictionary;
        }
    }
}



