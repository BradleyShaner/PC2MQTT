using BadLogger;
using PC2MQTT.MQTT;
using System;
using System.Collections.Concurrent;

namespace PC2MQTT.Sensors
{
    public partial class RhasspyAudioCopy : SensorBase, PC2MQTT.Sensors.ISensor
    {

        public new void ProcessMessage(MqttMessage mqttMessage)
        {
            var baseStation = "hassio";
            var satellite = "satZero1";
            var baseTopic = $"hermes/audioServer/{baseStation}/playBytes/";
            var satTopic = $"hermes/audioServer/{satellite}/playBytes/";
            var t = mqttMessage.GetRawTopic();

            if (t.Contains(baseTopic))
                t = mqttMessage.GetRawTopic().Substring(baseTopic.Length);
            else
                return;

            sensorHost.Publish(mqttMessage.SetTopic(satTopic).AddTopic(t));

        }

        public new void SensorMain()
        {
            base.SensorMain();

            // Subscribe to ALL mqtt messages with a root multi-level wildcard
            sensorHost.Subscribe(MqttMessageBuilder.
                NewMessage().
                SubscribeMessage.
                AddTopic("hermes/audioServer/").
                AddMultiLevelWildcard.
                DoNotRetain.
                QueueMessage.
                Build());
        }

    }
}