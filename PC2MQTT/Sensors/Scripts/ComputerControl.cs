using BadLogger;
using PC2MQTT.MQTT;
using System;

namespace PC2MQTT.Sensors
{
    public class ComputerControl : SensorBase, PC2MQTT.Sensors.ISensor
    {
        public new void ProcessMessage(MqttMessage mqttMessage)
        {
            Log.Info($"[ProcessMessage] Processing topic [{mqttMessage.GetRawTopic()}]: {mqttMessage.message}");

            var topic = mqttMessage.GetTopicWithoutDeviceId().Split('/');

            if (topic == null || topic.Length <= 1)
                return;

            if (topic[0] == "computer")
                HandleCommand(topic, mqttMessage.message);
            else
                Log.Info($"[ProcessMessage] Unknown topic [{mqttMessage.GetRawTopic()}]");
        }

        public new void SensorMain()
        {
            Log.Info($"(SensorMain) CPU id: {System.Threading.Thread.GetCurrentProcessorId()} ThreadId: {System.Threading.Thread.CurrentThread.ManagedThreadId}");

            sensorHost.Subscribe(MqttMessageBuilder.
                NewMessage().
                AddDeviceIdToTopic.
                SubscribeMessage.
                AddTopic("computer").
                AddMultiLevelWildcard.
                DoNotRetain.
                QueueMessage.
                Build());
            /*
            while (this.IsInitialized)
            {
                //Log.Info("If you want, you can stay in control for the life of the sensor using something like this.");
                System.Threading.Thread.Sleep(10000);
                sensorHost.Publish(MqttMessageBuilder.
                NewMessage().
                AddDeviceIdToTopic.
                PublishMessage.
                AddTopic("computer").
                AddTopic("shutdown").
                DoNotRetain.
                QueueMessage.
                Build());
            }
            */
        }

        private void HandleCommand(string[] command, string message)
        {
            var args = message;
            Log.Info($"[HandleCommand] Processing [{String.Join("/", command[1..])}] with args [{message}]");

            switch (command[1])
            {
                case "shutdown":
                    sensorHost.Dispose();
                    break;

                case "restart":
                case "reboot":
                    break;

                case "lock":
                    break;

                case "run":
                case "shell":
                    break;

                case "sleep":
                    break;

                case "hibernate":
                    break;

                case "monitor":
                    break;

                case "audio":
                case "sound":
                    break;

                case "screenshot":
                    break;

                case "ping":
                    break;

                case "uptime":
                    break;

                case "activity":
                    break;

                // list all running programs
                case "programs":
                    break;

                default:
                    break;
            }
        }
    }
}