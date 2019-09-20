namespace NmeaModule
{
    using System;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using svelde.nmea.parser;
    using Newtonsoft.Json;

// TODO : one parser per port
// TODO : filter in desired properties

    class Program
    {
        static NmeaParser _parser;

        static ModuleClient _ioTHubModuleClient;

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            _ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

            await _ioTHubModuleClient.OpenAsync();

            Console.WriteLine(@"");
            Console.WriteLine(@"     /$$$$$$      /$$$$$$  /$$    /$$ /$$$$$$$$ /$$       /$$$$$$$  /$$$$$$$$ ");
            Console.WriteLine(@"   /$$$__  $$$   /$$__  $$| $$   | $$| $$_____/| $$      | $$__  $$| $$_____/ ");
            Console.WriteLine(@"  /$$_/  \_  $$ | $$  \__/| $$   | $$| $$      | $$      | $$  \ $$| $$       ");
            Console.WriteLine(@" /$$/ /$$$$$  $$|  $$$$$$ |  $$ / $$/| $$$$$   | $$      | $$  | $$| $$$$$    ");
            Console.WriteLine(@"| $$ /$$  $$| $$ \____  $$ \  $$ $$/ | $$__/   | $$      | $$  | $$| $$__/    ");
            Console.WriteLine(@"| $$| $$\ $$| $$ /$$  \ $$  \  $$$/  | $$      | $$      | $$  | $$| $$       ");
            Console.WriteLine(@"| $$|  $$$$$$$$/|  $$$$$$/   \  $/   | $$$$$$$$| $$$$$$$$| $$$$$$$/| $$$$$$$$ ");
            Console.WriteLine(@"|  $$\________/  \______/     \_/    |________/|________/|_______/ |________/ ");
            Console.WriteLine(@" \  $$$   /$$$                                                                ");
            Console.WriteLine(@"  \_  $$$$$$_/                                                                ");
            Console.WriteLine(@"    \______/                                                                  ");
            Console.WriteLine("NMEA module client initialized.");

            var filter = "GPGSV,GLGSV,GNGSV,GPGSA,GNGSA,GNGLL,GNRMC,GNVTG";
            _parser = new NmeaParser(filter);
            _parser.NmeaMessageParsed += NmeaMessageParsed;

            Console.WriteLine($"NMEA Filter on {filter}");
            Console.WriteLine("Parser initialized.");

            // Register callback to be called when a message is received by the module
            await _ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, _ioTHubModuleClient);

            Console.WriteLine("Input 'input1' attached.");
        }

        static async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            
            //Console.WriteLine($"Input: '{messageString}'");

            var serialMessage = JsonConvert.DeserializeObject<SerialMessage>(messageString);

            await Task.Run(() =>
                {
                    try
                    {
                        _parser.Parse(serialMessage.Data, serialMessage.Port, serialMessage.TimestampUtc);
                    }
                    catch(Exception ex)
                    {
                       Console.WriteLine($"Parse exception {ex.Message}");
                    }
                });

            return MessageResponse.Completed;
        }

        static void NmeaMessageParsed(object sender, NmeaMessage e)
        {
            Console.WriteLine($"Parsed: '{e}'");
            
            var json = JsonConvert.SerializeObject(e);

            using (var message = new Message(Encoding.UTF8.GetBytes(json)))
            {
                _ioTHubModuleClient.SendEventAsync(e.Port, message).Wait();
            }
        }
    }

    public class SerialMessage
    {
        [JsonProperty("data")]
        public string Data { get; set; }

        [JsonProperty("port")]
        public string Port { get; set; }

        [JsonProperty("timestampUtc")]
        public DateTime TimestampUtc { get; set; }
    }
}
