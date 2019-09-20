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

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            _ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await _ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            _parser = new NmeaParser();

            _parser.NmeaMessageParsed += NmeaMessageParsed;

            Console.WriteLine("Parser initialized.");

            // Register callback to be called when a message is received by the module
            await _ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, _ioTHubModuleClient);

            Console.WriteLine("Input 'input1' attached.");
        }
        static async void NmeaMessageParsed(object sender, NmeaMessage e)
        {
            // if (!(e is GnrmcMessage) 
            //            || !(e as GnrmcMessage).ModeIndicator.IsValid())
            // {
            //     Console.WriteLine($"Ignored: '{e}'");
            //     return;
            // }
            // else
            // {
            //     Console.WriteLine($"Parsed: '{e}'");
            // }

            // var telemetry = new Telemetry
            // {
            //     Location = new TelemetryLocation
            //     {
            //         Latitude = (e as GnrmcMessage).Latitude.ToDecimalDegrees(),
            //         Longitude = (e as GnrmcMessage).Longitude.ToDecimalDegrees(),
            //     },
            //     FixTaken = (e as GnrmcMessage).TimeOfFix,
            //     Speed = Convert.ToDecimal( (e as GnrmcMessage).SpeedOverGround),
            //     Course = Convert.ToDecimal( (e as GnrmcMessage).CourseMadeGood),
            //     ModeIndicator = (e as GnrmcMessage).ModeIndicator,
            //     Port = e.Port,
            //     TimestampUtc = e.TimestampUtc,
            // };

            await Task.Run(()=> Console.WriteLine($"Parsed: '{e}'"));
            
            // var json = JsonConvert.SerializeObject(e);

            // using (var message = new Message(Encoding.ASCII.GetBytes(json)))
            // {
            //     await _ioTHubModuleClient.SendEventAsync(e.Port, message);
            //     Console.WriteLine($"Received message sent to port '{e.Port}'");
            // }
        }

        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            
            Console.WriteLine($"Input Body: '{messageString}'");

            var serialMessage = JsonConvert.DeserializeObject<SerialMessage>(messageString);

//await Task.Run(()=> Console.WriteLine("TEST"));

            await Task.Run(()=>_parser.Parse(serialMessage.Data, serialMessage.Port, serialMessage.TimestampUtc));

            return MessageResponse.Completed;
        }
    }

    public class Telemetry
    {
        [JsonProperty("timestampUtc")]
        public DateTime TimestampUtc { get; set; }

        [JsonProperty(PropertyName = "location")]
        public TelemetryLocation Location { get; set; }

        [JsonProperty(PropertyName = "modeIndicator")]
        public ModeIndicator ModeIndicator { get; set; }

        [JsonProperty(PropertyName = "speed")]
        public decimal Speed {get; set;}

        [JsonProperty(PropertyName = "course")]
        public decimal Course {get; set;}

        [JsonProperty("port")]
        public string Port { get; set; }

        [JsonProperty(PropertyName = "fixTaken")]
        public string FixTaken { get; set; }
    }
    public class TelemetryLocation
    {
        [JsonProperty(PropertyName = "lat")]
        public decimal Latitude { get; set; }

        [JsonProperty(PropertyName = "lon")]
        public decimal Longitude { get; set; }

        [JsonProperty(PropertyName = "alt")]
        public decimal? Altitude { get; set; }
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
