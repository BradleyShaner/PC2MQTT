﻿using BadLogger;
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
        private static IClient client;
        private static BadLogger.BadLogger Log;
        private static SensorManager sensorManager;
        private static Settings settings = new Settings();

        private static void Client_ConnectionClosed(string reason, byte errorCode)
        {
            Log.Warn($"Connection to MQTT server closed: {reason}");
            //todo: Unmap all topics in sensormanager?
            // Notify sensors the server connection was closed?
            // auto-resub to topics on reconnection?
        }

        private static void Client_ConnectionConnected()
        {
            Log.Info($"Connected to MQTT server");
        }

        private static void Client_MessagePublished(string topic, string message)
        {
        }

        private static void Client_MessageReceivedString(string topic, string message)
        {
            Log.Trace($"Message received for [{topic}]: {message}");
            sensorManager.ProcessMessage(topic, message);
        }

        private static void Client_TopicSubscribed(string topic)
        {
        }

        private static void Client_TopicUnsubscribed(string topic)
        {
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

        private static void InitializeSensors(bool useOnlyBuiltInScripts = true)
        {
            sensorManager = new SensorManager(client, settings);

            List<string> available = new List<string>();

            if (!useOnlyBuiltInScripts)
            {
                CSScriptLib.RoslynEvaluator.LoadCompilers();
                available.AddRange(sensorManager.LoadSensorScripts());
                available.AddRange(sensorManager.LoadBuiltInScripts());
            }
            else
            {
                Log.Info("Using only built-in scripts. (This improves runtime speeds and memory usage)");
                available.AddRange(sensorManager.LoadBuiltInScripts());
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
            Log = LogManager.GetCurrentClassLogger();

            Task t;
            t = Task.Run(() => InitializeMqtt());

            InitializeSensors(settings.config.useOnlyBuiltInScripts);
            sensorManager.StartSensors();

            while (Console.ReadKey().Key != ConsoleKey.Escape)
            {
                System.Threading.Thread.Sleep(10);
            }

            Log.Info("Escape key pressed, shutting down..");

            Log.Debug("Disposing of SensorManager..");
            sensorManager.Dispose();
            Log.Debug("Disconnecting MQTT..");
            client.MqttDisconnect();

            settings.SaveSettings();
            Environment.Exit(0);
        }
    }
}