using BadLogger;
using PC2MQTT.Helpers;
using PC2MQTT.MQTT;
using PC2MQTT.Sensors;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PC2MQTT
{
    internal class Program
    {
        public static readonly string Version = "0.1.0-dev";
        private static readonly AutoResetEvent _closing = new AutoResetEvent(false);
        private static IClient client;
        private static bool disconnectedUnexpectedly = false;
        private static BadLogger.BadLogger Log;
        private static SensorManager sensorManager;
        private static Settings settings = new Settings();
        private static List<MqttMessage> overflow = new List<MqttMessage>();

        protected static void OnExit(object sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            _closing.Set();
        }

        private static void Client_ConnectionClosed(string reason, byte errorCode)
        {
            Log.Warn($"Connection to MQTT server closed: {reason}");
            //todo: Unmap all topics in sensormanager?
            // Notify sensors the server connection was closed?
            // auto-resub to topics on reconnection?
            disconnectedUnexpectedly = true;
            sensorManager.NotifySensorsServerStatus(ServerState.Reconnecting, ServerStateReason.Unknown);
        }

        private static void Client_ConnectionConnected()
        {
            Log.Info($"Connected to MQTT server");

            if (disconnectedUnexpectedly && settings.config.resubscribeOnReconnect)
            {
                sensorManager.ReMapTopics();
                disconnectedUnexpectedly = false;
                sensorManager.NotifySensorsServerStatus(ServerState.Reconnected, ServerStateReason.Unknown);
            }
        }

        private static void Client_MessagePublished(MqttMessage mqttMessage)
        {
            Log.Verbose($"Message published for {mqttMessage.GetRawTopic()}: [{mqttMessage.message}]");
            //throw new NotImplementedException();
        }

        private static void Client_MessageReceivedString(MqttMessage mqttMessage)
        {
            Log.Verbose($"Message received for [{mqttMessage.GetTopicWithoutDeviceId()}]: {mqttMessage.message}");
            sensorManager.ProcessMessage(mqttMessage);
            if (sensorManager == null)
            {
                Log.Verbose($"SensorManager not initialized yet, adding to overflow..");
                overflow.Add(mqttMessage);
            } else { 
                sensorManager.ProcessMessage(mqttMessage);
            }
        }

        private static void Client_TopicSubscribed(MqttMessage mqttMessage)
        {
            Log.Verbose($"Topic subscribed for {mqttMessage.GetRawTopic()}: [{mqttMessage.message}]");
            //throw new NotImplementedException();
        }

        private static void Client_TopicUnsubscribed(MqttMessage mqttMessage)
        {
            Log.Verbose($"Topic Unsubscribed for {mqttMessage.GetRawTopic()}: [{mqttMessage.message}]");
            //throw new NotImplementedException();
        }

        private static void InitializeExtensions()
        {
            ExtensionMethods.Extensions.deviceId = settings.config.mqttSettings.deviceId;
        }

        private static void InitializeMqtt()
        {
            if (settings.config.mqttSettings.broker.Length == 0 || settings.config.mqttSettings.port == 0)
            {
                Log.Fatal("Unable to initialized MQTT, missing connection details!");
                Environment.Exit(1);
            }

            Log.Debug($"Initializing MQTT client..");

            if (settings.config.mqttSettings.useFakeMqttServer)
                client = new FakeClient(settings.config.mqttSettings);
            else
                client = new MQTT.Client(settings.config.mqttSettings, true);

            client.ConnectionClosed += Client_ConnectionClosed;
            client.ConnectionConnected += Client_ConnectionConnected;
            client.MessagePublished += Client_MessagePublished;
            client.TopicSubscribed += Client_TopicSubscribed;
            client.MessageReceivedString += Client_MessageReceivedString;
            client.TopicUnsubscribed += Client_TopicUnsubscribed;

            client.MqttConnect();
        }

        private static void InitializeSensors(bool useOnlyBuiltInSensors = true, Task roslynLoading = null)
        {
            sensorManager = new SensorManager(client, settings);

            List<string> available = new List<string>();

            while (roslynLoading != null && !roslynLoading.IsCompleted)
            {
                Log.Verbose("Roslyn still pre-loading..");
                Thread.Sleep(50);
            }

            if (!useOnlyBuiltInSensors)
            {
                available.AddRange(sensorManager.LoadSensorScripts());

                foreach (var item in sensorManager.LoadBuiltInSensors())
                    if (!available.Contains(item))
                        available.Add(item);
            }
            else
            {
                Log.Info("Using only built-in sensors. (This improves runtime speeds and memory usage)");
                available.AddRange(sensorManager.LoadBuiltInSensors());
            }

            if (settings.config.enabledSensors.Count == 0)
            {
                Log.Info("No sensors enabled, enabling all found sensors..");
                settings.config.enabledSensors = available;
                settings.SaveSettings();
            }

            sensorManager.InitializeSensors(settings.config.enabledSensors);
        }

        private static void InitializeSettings()
        {
            if (!settings.LoadSettings())
            {
                settings.SaveSettings();
                Console.WriteLine("Generating default settings. Please edit config.json and re-launch the program.");
                Environment.Exit(0);
            }
        }

        private static void Main(string[] args)
        {
            Console.WriteLine($"PC2MQTT v{Version} starting");

            InitializeSettings();
            InitializeExtensions();

            Logging.InitializeLogging(settings);
            Log = LogManager.GetCurrentClassLogger("PC2MQTT");

            Task roslynLoading = null;

            if (!settings.config.useOnlyBuiltInSensors)
            {
                roslynLoading = Task.Run(() =>
                {
                    Log.Verbose("Pre-loading Roslyn compiler..");
                    CSScriptLib.RoslynEvaluator.LoadCompilers();
                    Log.Verbose("Roslyn finished loading.");
                });
            }

            InitializeMqtt();

            InitializeSensors(settings.config.useOnlyBuiltInSensors, roslynLoading);

            // this isn't ideal, but sometimes the mqtt server will send data before sensors
            // have fully initialized.
            foreach (var item in overflow)
            {
                sensorManager.ProcessMessage(item);
            }

            sensorManager.StartSensors();

            Console.CancelKeyPress += new ConsoleCancelEventHandler(OnExit);
            _closing.WaitOne();
            sensorManager.NotifySensorsServerStatus(ServerState.Disconnecting, ServerStateReason.ShuttingDown);

            Log.Info("Shutting down..");

            Log.Debug("Disposing of SensorManager..");
            sensorManager.Dispose();
            Log.Debug("Disconnecting MQTT..");
            client.MqttDisconnect();

            settings.SaveSettings();
            Environment.Exit(0);
        }
    }
}