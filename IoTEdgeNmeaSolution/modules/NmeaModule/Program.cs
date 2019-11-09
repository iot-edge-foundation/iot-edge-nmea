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
    using Microsoft.Azure.Devices.Shared;

    class Program
    {
        const string defaultFilter = "";

        static string _filter;

        static NmeaParserList _parserList;

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
            _filter = defaultFilter;

            _parserList = new NmeaParserList();
            _parserList.UpdateFilter(_filter);
            _parserList.NmeaMessagesParsed += NmeaMessageParsed;

            Console.WriteLine("Parser initialized.");

            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            _ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

            // Attach callback for Twin desired properties updates
            await _ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertiesUpdate, _ioTHubModuleClient);

            // Execute callback method for Twin desired properties updates
            var twin = await _ioTHubModuleClient.GetTwinAsync();
            await onDesiredPropertiesUpdate(twin.Properties.Desired, _ioTHubModuleClient);

            await _ioTHubModuleClient.OpenAsync();

            Console.WriteLine("      _                         ___      _____   ___     _");
            Console.WriteLine("     /_\\   ___ _  _  _ _  ___  |_ _| ___|_   _| | __| __| | __ _  ___  ");
            Console.WriteLine("    / _ \\ |_ /| || || '_|/ -_)  | | / _ \\ | |   | _| / _` |/ _` |/ -_)");
            Console.WriteLine("   /_/ \\_\\/__| \\_,_||_|  \\___| |___|\\___/ |_|   |___|\\__,_|\\__, |\\___|");
            Console.WriteLine("                                                           |___/");
            Console.WriteLine("    _  _                 ");
            Console.WriteLine("   | \\| |_ __  ___ __ _  ");
            Console.WriteLine("   | .` | '  \\/ -_) _` | ");
            Console.WriteLine("   |_|\\_|_|_|_\\___\\__,_| ");                                  
            Console.WriteLine(" ");
            Console.WriteLine("   Copyright © 2019 - IoT Edge Foundation");
            Console.WriteLine(" ");

            // Register callback to be called when a message is received by the module
            await _ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, _ioTHubModuleClient);

            Console.WriteLine("Input 'input1' attached.");
        }

        static async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            string messageString = string.Empty;
            try
            {
                var moduleClient = userContext as ModuleClient;
                if (moduleClient == null)
                {
                    throw new InvalidOperationException("UserContext doesn't contain expected ModuleClient");
                }

                byte[] messageBytes = message.GetBytes();
                messageString = Encoding.UTF8.GetString(messageBytes);
                
                var serialMessage = JsonConvert.DeserializeObject<SerialMessage>(messageString);

                await Task.Run(()=>{
                    var parser = _parserList.Find(serialMessage.Port);
                    parser.Parse(serialMessage.Data, serialMessage.Port, serialMessage.TimestampUtc);
                });
            }
            catch (Exception ex)
            {
                var exceptionMessage = new ExceptionMessage{Message= ex.Message};

                var json = JsonConvert.SerializeObject(exceptionMessage);

                using (var messageException = new Message(Encoding.UTF8.GetBytes(json)))
                {
                    await _ioTHubModuleClient.SendEventAsync("Exception", messageException);
                }
            }

            return MessageResponse.Completed;
        }

        static void NmeaMessageParsed(object sender, NmeaMessage e)
        {
            Console.Write($"Parsed: '{e}'");
            
            var json = JsonConvert.SerializeObject(e);

            using (var message = new Message(Encoding.UTF8.GetBytes(json)))
            {
                _ioTHubModuleClient.SendEventAsync(e.Port, message).Wait();
            }

            Console.WriteLine($"-Sent");
        }

        private static Task onDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            if (desiredProperties.Count == 0)
            {
                return Task.CompletedTask;
            }

            try
            {
                Console.WriteLine("Desired property change:");
                Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

                var client = userContext as ModuleClient;

                if (client == null)
                {
                    throw new InvalidOperationException($"UserContext doesn't contain expected ModuleClient");
                }

                var reportedProperties = new TwinCollection();

                if (desiredProperties.Contains("filter")) 
                {
                    if (desiredProperties["filter"] != null)
                    {
                        _filter = desiredProperties["filter"];
                    }
                    else
                    {
                        _filter = defaultFilter;
                    }

                    Console.WriteLine($"Filter changed to '{_filter}'");

                    reportedProperties["filter"] = _filter;

                    _parserList.UpdateFilter(_filter);
                }

                if (reportedProperties.Count > 0)
                {
                    client.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
                }
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
            }

            return Task.CompletedTask;
        }
    }
}
