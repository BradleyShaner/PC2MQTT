using BadLogger;
using CSScriptLib;
using PC2MQTT.MQTT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace PC2MQTT.Sensors
{
    public class SensorHost : IDisposable
    {
        public string code { get; private set; }
        public string GetLastError { get; private set; }
        public bool IsCodeLoaded { get; private set; }
        public bool IsCompiled { get; private set; }
        public ISensor sensor { get; private set; }
        public string SensorIdentifier { get; private set; }
        private IClient _client;
        private bool _hasBeendisposed;

        private SensorManager _sensorManager;
        private BadLogger.BadLogger Log;

        public SensorHost(string code, IClient client, SensorManager sensorManager)
        {
            this.code = code;
            this._client = client;
            this._sensorManager = sensorManager;

            Log = LogManager.GetCurrentClassLogger("SensorHost");

            IsCodeLoaded = true;

            Compile();
        }

        public SensorHost(IClient client, SensorManager sensorManager)
        {
            this._client = client;
            this._sensorManager = sensorManager;

            Log = LogManager.GetCurrentClassLogger("SensorHost");

            IsCodeLoaded = false;
        }

        public SensorHost(ISensor sensor, IClient client, SensorManager sensorManager)
        {
            this._client = client;
            this._sensorManager = sensorManager;
            this.sensor = sensor;

            Log = LogManager.GetCurrentClassLogger("SensorHost");

            if (sensor.IsCompatibleWithCurrentRuntime())
            {
                IsCodeLoaded = true;
                IsCompiled = true;
            }

            this.SensorIdentifier = sensor.GetSensorIdentifier().ToLower();
        }

        public void Dispose()
        {
            DisposeSensor();
            GC.SuppressFinalize(this);
        }

        public void DisposeSensor()
        {
            if (_hasBeendisposed) return;

            _sensorManager.UnmapAlltopics(this);
            try
            {
                if (sensor != null && sensor.IsInitialized)
                {
                    UninitializeSensor();
                    sensor.Dispose();
                }
            }
            catch (Exception ex) { Log.Warn($"Unable to properly dispose of {sensor.GetSensorIdentifier()}: {ex.Message}"); }

            code = null;
            IsCodeLoaded = false;
            IsCompiled = false;
            this._client = null;
            //this.SensorIdentifier = null;
            this.GetLastError = null;
            this.sensor = null;

            _hasBeendisposed = true;
        }

        public bool InitializeSensor()
        {
            if (IsCompiled)
                sensor.IsInitialized = sensor.Initialize(this);

            if (sensor == null)
                return false;

            return sensor.IsInitialized;
        }

        public dynamic LoadData(string name, Object defaultData = null, bool global = false, Type type = null)
        {
            string identifier = !global ? this.SensorIdentifier + "-" + name : "global-" + name;

            Log.Debug($"Loading [{name}] for {this.SensorIdentifier}.");

            if (_sensorManager.settings.config.sensorData.TryGetValue(identifier, out var data))
            {
                var obj = Newtonsoft.Json.JsonConvert.DeserializeObject(data, type);
                return obj;
            }
            else
            {
                if (defaultData == null)
                {
                    Log.Warn($"Failed to load [{name}] for {this.SensorIdentifier}.");
                }
                else
                {
                    SaveData(name, defaultData, false, global);
                    return LoadData(name, null, global, type);
                }
            }

            return data;
        }

        public void LoadFromFile(string filePath, bool compile = true)
        {
            try
            {
                this.code = File.ReadAllText(filePath);
            }
            catch (Exception ex) { GetLastError = ex.Message; }

            IsCodeLoaded = true;
            if (compile) Compile();

            if (this.SensorIdentifier == null)
                this.SensorIdentifier = Path.GetFileName(filePath);
        }

        public bool Publish(MqttMessage mqttMessage)
        {
            if (_client == null)
                return false;

            mqttMessage.SetMessageType(MqttMessage.MqttMessageType.MQTT_PUBLISH);

            if (!mqttMessage.sendImmediately)
            {
                _client.QueueMessage(mqttMessage.PublishMessage);
                return true;
            }

            Log.Verbose($"[{SensorIdentifier}] publishing to [{mqttMessage.GetRawTopic()}]: [{mqttMessage.message}]");

            var success = _client.Publish(mqttMessage);

            if (success.messageId > 0) return true;

            return false;
        }

        public bool SaveData(string name, Object data, bool overWrite = true, bool global = false)
        {
            string identifier = !global ? this.SensorIdentifier + "-" + name : "global-" + name;

            Log.Debug($"Saving [{name}] for {this.SensorIdentifier}.");

            var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(data);

            try
            {
                bool exists = _sensorManager.settings.config.sensorData.ContainsKey(identifier);

                if (overWrite && exists)
                    _sensorManager.settings.config.sensorData.TryRemove(identifier, out var o);

                if (exists && !overWrite)
                {
                    Log.Warn($"Failed to save [{name}] for {this.SensorIdentifier}: overWrite is false.");
                    return false;
                }

                var saved = _sensorManager.settings.config.sensorData.TryAdd(identifier, serialized);

                _sensorManager.settings.newDataToSave = true;

                return saved;
            }
            catch { }

            return true;
        }

        public bool SendMessage(MqttMessage mqttMessage) => SendMqttMessage(mqttMessage);

        public bool SendMqttMessage(MqttMessage mqttMessage)
        {
            if (_client == null)
                return false;

            if (mqttMessage.messageType == MqttMessage.MqttMessageType.MQTT_NONE)
                return false;

            if (!mqttMessage.sendImmediately)
            {
                _client.QueueMessage(mqttMessage);
                _sensorManager.MapTopicToSensor(mqttMessage, this);
                return true;
            }

            var success = _client.SendMessage(mqttMessage);
            Log.Verbose($"[{SensorIdentifier}] sending message for [{mqttMessage.GetRawTopic()}]");

            if (success.messageId > 0)
            {
                _sensorManager.MapTopicToSensor(mqttMessage, this);

                return true;
            }

            return false;
        }

        public bool Subscribe(MqttMessage mqttMessage)
        {
            if (_client == null)
                return false;

            mqttMessage.SetMessageType(MqttMessage.MqttMessageType.MQTT_SUBSCRIBE);

            if (!mqttMessage.sendImmediately)
            {
                if (_sensorManager.MapTopicToSensor(mqttMessage, this))
                {
                    _client.QueueMessage(mqttMessage.SubscribeMessage);
                    return true;
                }
                else
                {
                    return false;
                }
            }

            var success = _client.Subscribe(mqttMessage);
            Log.Verbose($"[{SensorIdentifier}] subscribing to [{mqttMessage.GetRawTopic()}]");

            if (success.messageId > 0)
            {
                _sensorManager.MapTopicToSensor(mqttMessage, this);

                return true;
            }

            return false;
        }

        public void UninitializeSensor()
        {
            if (sensor != null && IsCompiled && sensor.IsInitialized)
            {
                sensor.Uninitialize();
                sensor.IsInitialized = false;
            }
        }

        public bool Unsubscribe(MqttMessage mqttMessage)
        {
            if (_client == null)
                return false;

            mqttMessage.SetMessageType(MqttMessage.MqttMessageType.MQTT_UNSUBSCRIBE);

            if (!mqttMessage.sendImmediately)
            {
                _client.QueueMessage(mqttMessage.UnsubscribeMessage);
                _sensorManager.UnmapTopicToSensor(mqttMessage, this);
                return true;
            }

            var success = _client.Unsubscribe(mqttMessage);
            Log.Verbose($"[{SensorIdentifier}] unsubscribing to [{mqttMessage.GetRawTopic()}] ({success})");

            if (success.messageId > 0)
            {
                _sensorManager.UnmapTopicToSensor(mqttMessage, this);
                return true;
            }

            return false;
        }

        public void UnsubscribeAllTopics() => _sensorManager.UnmapAlltopics(this);

        private void Compile()
        {
            if (!IsCodeLoaded)
                return;

            if (!this.code.Contains("using PC2MQTT.MQTT;"))
                this.code = "using PC2MQTT.MQTT;\r\n" + this.code;

            if (!this.code.Contains("PC2MQTT.Helpers;"))
                this.code = "using PC2MQTT.Helpers;\r\n" + this.code;

            if (!this.code.Contains("using PC2MQTT.Sensors;"))
                this.code = "using PC2MQTT.Sensors;\r\n" + this.code;

            if (!this.code.Contains("using System;"))
                this.code = "using System;\r\n" + this.code;

            string ns = "namespace PC2MQTT.Sensors";

            if (this.code.Contains(ns))
            {
                Log.Verbose("Sensor script contains a namespace. Attempting to remove it..");

                var nsLocation = this.code.IndexOf(ns);
                this.code = this.code.Remove(nsLocation, ns.Length);

                var firstParen = this.code.IndexOf("{", nsLocation);
                this.code = this.code.Remove(firstParen, 1);

                var lastParen = this.code.LastIndexOf("}");
                this.code = this.code.Remove(lastParen, 1);
            }

            try
            {
                this.sensor = CSScript.RoslynEvaluator
                    .ReferenceAssembliesFromCode(code)
                    .ReferenceAssembly(Assembly.GetExecutingAssembly())
                    .ReferenceAssembly(Assembly.GetExecutingAssembly().Location)
                    .LoadCode<ISensor>(code);

                if (sensor.DidSensorCompile() == false || sensor.IsCompatibleWithCurrentRuntime() == false)
                    IsCompiled = false;
                else
                    IsCompiled = true;

                if (IsCompiled)
                    SensorIdentifier = sensor.GetSensorIdentifier().ToLower();
            }
            catch (Exception ex) { GetLastError = ex.Message; return; }
        }
    }
}