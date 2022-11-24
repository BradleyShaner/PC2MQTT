using BadLogger;
using ExtensionMethods;
using PC2MQTT.MQTT;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;

namespace PC2MQTT.Sensors
{
    public class Uptime : SensorBase, PC2MQTT.Sensors.ISensor
    {
        private Timer fiveSeconds;

        private static string HumanReadableTimeSpan(double milliseconds)
        {
            if (milliseconds == 0) return "0 ms";

            StringBuilder sb = new StringBuilder();

            Action<int, StringBuilder, int> addActionToSB =  //or pass an entire new format!
            (val, displayunit, zeroplaces) =>
            {
                if (val > 0)
                    sb.AppendFormat(
            " {0:DZ}X".Replace("X", displayunit.ToString())
            .Replace("Z", zeroplaces.ToString())
            , val
           );
            };

            var t = TimeSpan.FromMilliseconds(milliseconds);

            //addActionToSBList(timespan property, readable display displayunit, number of zero placeholders) //Sun 24-Sep-17 8:30pm metadataconsulting.ca - Star Trek Disco
            addActionToSB(t.Days, new StringBuilder("d"), 1);
            addActionToSB(t.Hours, new StringBuilder("h"), 1);
            addActionToSB(t.Minutes, new StringBuilder("m"), 2);
            addActionToSB(t.Seconds, new StringBuilder("s"), 2);
            addActionToSB(t.Milliseconds, new StringBuilder("ms"), 3);

            return sb.ToString().TrimStart();
        }

        public new void Dispose()
        {
            Log.Debug($"Disposing [{GetSensorIdentifier()}]");
            Log = null;
            fiveSeconds.Dispose();
            sensorHost = null;
        }

        private TimeSpan GetUpTime()
        {
            if (CSScriptLib.Runtime.IsLinux)
            {
                string output = "cat /proc/uptime".LinuxBashResult();
                var uptime = output.Split(" ");
                return TimeSpan.FromSeconds(double.Parse(uptime[0]));
            }

            if (CSScriptLib.Runtime.IsWin)
            {
                //string cmd = "wmic path Win32_OperatingSystem get LastBootUpTime".WindowsShellResult();
                //Log.Info("cmd: " + cmd);

                return TimeSpan.FromMilliseconds(GetTickCount64());
            }

            return TimeSpan.MinValue;
        }

        public new void ProcessMessage(MqttMessage mqttMessage)
        {
            Log.Debug($"[{GetSensorIdentifier()}] Processing topic [{mqttMessage.GetRawTopic()}]");

            if (mqttMessage.GetTopicWithoutDeviceId() == "uptime/get")
            {
                Log.Info("Received uptime request");
                if (!sensorHost.Publish(new MqttMessageBuilder().PublishMessage.AddDeviceIdToTopic.AddTopic("/uptime/current").SetMessage(GetUpTime().TotalMilliseconds.ToString()).DoNotRetain.Build()))
                    Log.Info("Failed to publish to /example1/status");
            }
            else if (mqttMessage.GetTopicWithoutDeviceId() == "uptime/current")
            {
                //Log.Info("Received uptime message: " + mqttMessage.message + "ms");
                Log.Info("Uptime received: " + HumanReadableTimeSpan(double.Parse(mqttMessage.message)));
            }
        }

        public new void SensorMain()
        {
            Log.Debug($"(SensorMain) CPU id: {System.Threading.Thread.GetCurrentProcessorId()} ThreadId: {System.Threading.Thread.CurrentThread.ManagedThreadId}");

            //this.sensorHost.Subscribe("/uptime/get");
            //this.sensorHost.Subscribe("/uptime/current");

            var msg = MqttMessageBuilder.NewMessage().SubscribeMessage.AddDeviceIdToTopic.AddTopic("/uptime/").AddSingleLevelWildcard.DoNotRetain.Build();
            if (!sensorHost.Subscribe(msg))
                Log.Info("Failed to subscribe to /example2/#");

            Log.Info("Requesting my own uptime every 15 seconds..");

            fiveSeconds = new Timer(15000);
            fiveSeconds.Elapsed += delegate { sensorHost.Publish(new MqttMessageBuilder().PublishMessage.AddDeviceIdToTopic.AddTopic("/uptime/get").DoNotRetain.Build()); };

            fiveSeconds.Start();
        }

        public new void Uninitialize()
        {
            if (fiveSeconds != null)
            {
                fiveSeconds.Stop();
                fiveSeconds.Dispose();
            }
        }

        [DllImport("kernel32")]
        private static extern UInt64 GetTickCount64();
    }
}