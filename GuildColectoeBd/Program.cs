using CoreRCON;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System.Net;

namespace GuildColectoeBd
{
    class Program
    {
        static string rconIp;
        static int rconPort;
        static bool FiltrarGuildaSemNome;
        static string rconPassword;
        static string networkPath;
        public static string ConnectionString;

        static async Task Main(string[] args)
        {

            InitializeEnv();

            try
            {
                var endPoint = new IPEndPoint(IPAddress.Parse(rconIp), rconPort);
                var rcon = new RCON(endPoint, rconPassword);
                await rcon.ConnectAsync();
                Console.WriteLine("Connected to RCON server.");

                // Enviar comando RCON e aguardar resposta
                try
                {
                    string response = await rcon.SendCommandAsync("exportguilds");
                    //Console.WriteLine("Command sent. Response: " + response);
                }
                catch (Exception ex)
                {
                    //Console.WriteLine("Error sending command: " + ex.Message);
                }

                // Verificar a criação do arquivo e processar
                string filePath = await CheckForFileAsync(networkPath);
                if (!string.IsNullOrEmpty(filePath))
                {
                    var guilds = await CollectGuildsAndMembersAsync(filePath);
                    if (guilds != null)
                    {
                        await InsertOrUpdateMembersAsync(guilds);
                        DeleteFile(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        public static void InitializeEnv()
        {
            rconIp = Environment.GetEnvironmentVariable("RCON_IP") ?? "192.168.100.73";
            rconPort = int.TryParse(Environment.GetEnvironmentVariable("RCON_PORT"), out var port) ? port : 25575;
            FiltrarGuildaSemNome = bool.TryParse(Environment.GetEnvironmentVariable("FILTRAR_GUILDA_SEM_NOME"), out var ativo) ? ativo : false;
            rconPassword = Environment.GetEnvironmentVariable("RCON_PASSWORD") ?? "unreal";
            networkPath = Environment.GetEnvironmentVariable("NETWORK_PATH") ?? @"\\OPTSUKE01\palguard";
            ConnectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING") ?? "Server=192.168.100.84;Database=db-palworld-pvp-insiderhub;Uid=PalAdm;Pwd=sukelord;SslMode=none;";
        }

        static async Task<string> CheckForFileAsync(string path)
        {
            string filePath = Path.Combine(path, "guildexport.json");

            while (!File.Exists(filePath))
            {
                Console.WriteLine("Waiting for the file to be created...");
                await Task.Delay(5000); // Espera 5 segundos antes de verificar novamente
            }

            Console.WriteLine("File found: " + filePath);
            return filePath;
        }

        static async Task<Dictionary<string, Guild>> CollectGuildsAndMembersAsync(string filePath)
        {
            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                var guilds = JsonConvert.DeserializeObject<Dictionary<string, Guild>>(json);

                foreach (var guildEntry in guilds)
                {
                    string guildId = guildEntry.Key;
                    Guild guild = guildEntry.Value;

                    //Console.WriteLine($"Guild ID: {guildId}");
                    //Console.WriteLine($"Name: {guild.Name}");
                    //Console.WriteLine($"Admin UID: {guild.AdminUID}");
                    //Console.WriteLine($"CampNum: {guild.CampNum}");
                    //Console.WriteLine($"Level: {guild.Level}");
                    //Console.WriteLine("Members:");

                    foreach (var memberEntry in guild.Members)
                    {
                        string memberId = memberEntry.Key;
                        Member member = memberEntry.Value;

                        //Console.WriteLine($"  Member ID: {memberId}");
                        //Console.WriteLine($"    NickName: {member.NickName}");
                        //Console.WriteLine($"    Exp: {member.Exp}");
                        //Console.WriteLine($"    Level: {member.Level}");
                        //Console.WriteLine($"    Position: {member.Pos}");
                    }

                    //Console.WriteLine();
                }

                return guilds;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing the JSON file: " + ex.Message);
                return null;
            }
        }

        public static async Task InsertOrUpdateMembersAsync(Dictionary<string, Guild> guilds)
        {
            try
            {


                using var connection = new MySqlConnection(ConnectionString);
                await connection.OpenAsync();

                foreach (var guildEntry in guilds)
                {
                    string guildId = guildEntry.Key;
                    Guild guild = guildEntry.Value;

                    foreach (var memberEntry in guild.Members)
                    {
                        string memberId = memberEntry.Key;
                        Member member = memberEntry.Value;

                        var commandText = @"
                    INSERT INTO clan_temp (
                        id, player_name, internal_player_id, player_exp, player_lvl, player_pos,
                        clan_id, clan_name, clan_admin, clan_camp_num, clan_camps, clan_lvl
                    )
                    VALUES (
                        @id, @player_name, @internal_player_id, @player_exp, @player_lvl, @player_pos,
                        @clan_id, @clan_name, @clan_admin, @clan_camp_num, @clan_camps, @clan_lvl
                    )
                    ON DUPLICATE KEY UPDATE
                        player_name = VALUES(player_name),
                        player_exp = VALUES(player_exp),
                        player_lvl = VALUES(player_lvl),
                        player_pos = VALUES(player_pos),
                        clan_name = VALUES(clan_name),
                        clan_admin = VALUES(clan_admin),
                        clan_camp_num = VALUES(clan_camp_num),
                        clan_camps = VALUES(clan_camps),
                        clan_lvl = VALUES(clan_lvl);
                ";
                        if (FiltrarGuildaSemNome)
                        {
                            if (guild.Name != "Guilda sem nome" && guild.Name != "" && guild.Name != null)
                            {
                                using var command = new MySqlCommand(commandText, connection);
                                command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                                command.Parameters.AddWithValue("@player_name", member.NickName);
                                command.Parameters.AddWithValue("@internal_player_id", memberId);
                                command.Parameters.AddWithValue("@player_exp", member.Exp.ToString());
                                command.Parameters.AddWithValue("@player_lvl", member.Level);
                                command.Parameters.AddWithValue("@player_pos", member.Pos);
                                command.Parameters.AddWithValue("@clan_id", guildId);
                                command.Parameters.AddWithValue("@clan_name", guild.Name);
                                command.Parameters.AddWithValue("@clan_admin", guild.AdminUID == memberId ? 1 : 0);
                                command.Parameters.AddWithValue("@clan_camp_num", guild.CampNum);
                                command.Parameters.AddWithValue("@clan_camps", string.Join(",", guild.Camps.Keys));
                                command.Parameters.AddWithValue("@clan_lvl", guild.Level);

                                await command.ExecuteNonQueryAsync();
                                Console.WriteLine($"Inserting/Updating: {memberId}  {member.NickName}  {guild.Name}");
                            }

                        }
                        else
                        {
                            using var command = new MySqlCommand(commandText, connection);
                            command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                            command.Parameters.AddWithValue("@player_name", member.NickName);
                            command.Parameters.AddWithValue("@internal_player_id", memberId);
                            command.Parameters.AddWithValue("@player_exp", member.Exp.ToString());
                            command.Parameters.AddWithValue("@player_lvl", member.Level);
                            command.Parameters.AddWithValue("@player_pos", member.Pos);
                            command.Parameters.AddWithValue("@clan_id", guildId);
                            command.Parameters.AddWithValue("@clan_name", guild.Name);
                            command.Parameters.AddWithValue("@clan_admin", guild.AdminUID == memberId ? 1 : 0);
                            command.Parameters.AddWithValue("@clan_camp_num", guild.CampNum);
                            command.Parameters.AddWithValue("@clan_camps", string.Join(",", guild.Camps.Keys));
                            command.Parameters.AddWithValue("@clan_lvl", guild.Level);

                            await command.ExecuteNonQueryAsync();
                            Console.WriteLine($"Inserting/Updating: {memberId}  {member.NickName}  {guild.Name}");
                        }

                    }
                }
            }
            catch (Exception ex)
            {

                Console.WriteLine("Erro ao inserir no banco: " + ex.Message);
            }
        }

        static void DeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Console.WriteLine("File deleted: " + filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error deleting the file: " + ex.Message);
            }
        }
    }

    public class Guild
    {
        [JsonProperty("AdminUID")]
        public string AdminUID { get; set; }

        [JsonProperty("CampNum")]
        public string CampNum { get; set; }

        [JsonProperty("Camps")]
        public Dictionary<string, object> Camps { get; set; }

        [JsonProperty("Level")]
        public int Level { get; set; }

        [JsonProperty("Members")]
        public Dictionary<string, Member> Members { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }
    }

    public class Member
    {
        [JsonProperty("Exp")]
        public int Exp { get; set; }

        [JsonProperty("Level")]
        public int Level { get; set; }

        [JsonProperty("NickName")]
        public string NickName { get; set; }

        [JsonProperty("Pos")]
        public string Pos { get; set; }
    }

}
