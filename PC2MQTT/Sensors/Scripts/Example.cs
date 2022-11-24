using BadLogger;
using PC2MQTT.MQTT;
using System;
using System.Collections.Generic;
using System.Timers;

// Namespaces aren't allowed, so either remove this (and its {} brackets) or leave it alone
// PC2MQTT attempts to remove namespaces automatically before compiling the sensor script
namespace PC2MQTT.Sensors
{
    // Change "Example" to whatever you want your Sensor to be.
    public class Example : SensorBase, ISensor
    {
        // Be sure to check out SensorBase.cs for a complete list of methods you can override in your sensors!
        

        // An example timer, see more below in SensorMain()
        private Timer unloadTimer;

        /// <inheritdoc cref="SensorBase.Initialize(SensorHost)"/>
        public new bool Initialize(SensorHost sensorHost)
        {
            // Initialize BadLogger so we can pass log messages
            Log = LogManager.GetCurrentClassLogger(GetSensorIdentifier());

            // You'll want to save this for later in the interface sensorHost object
            this.sensorHost = sensorHost;

            Log.Info($"(Initialize) CPU id: {System.Threading.Thread.GetCurrentProcessorId()} ThreadId: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            Log.Info($"IsWin: {CSScriptLib.Runtime.IsWin} IsCore: { CSScriptLib.Runtime.IsCore}");

            // Initialize needs to return true relatively quickly but you can stall for a while or run processor-intensive things beforehand.
            // Control is returned to the sensor in SensorMain after initialization is complete.
            Log.Info($"We're not done initializing yet.. just a bit longer..");
            System.Threading.Thread.Sleep(2000);

            // You can save and load data types that NewtonsoftJson can handle using built-in Save/Load features shown below
            // Note that this data is only saved every 5 minutes or if the program gracefully shuts down!
            // Or you can of course roll your own save/load methods.
            string test = "hello world";

            // Save the string "hello world" under the key "test" using sensorHost's SaveData method
            sensorHost.SaveData("test", test);

            // Retrieve the string with the same key "test".
            // With this, you need to specify the type of the data you are retrieving
            var stringResult1 = sensorHost.LoadData("test", type: typeof(string));

            // This one will fail because it doesn't exist
            var stringResult2 = sensorHost.LoadData("test2", type: typeof(string));

            // This one will succeed because it has a default value supplied.
            var stringResult3 = sensorHost.LoadData("test3", "test3", type: typeof(string));

            Log.Info("Load Data [test] result: " + stringResult1);

            // Can also use other data types such as collections
            List<string> testList = new List<string>();
            testList.Add("hello");
            testList.Add("world");
            testList.Add("!");

            // save the collection under "testList"
            // Note that overWrite is true by default and only shown as an example
            sensorHost.SaveData("testList", testList, overWrite: true);

            // Load it again. Note that we're setting global = false but it's not necessary.
            // You can store global objects for use in other sensors with global = true
            // For this data, however, we need to pass the type otherwise it returns a JArray.
            List<string> resultList = sensorHost.LoadData("testList", global: false, type: typeof(List<string>));

            Log.Info($"Load Data [testList] result: Count:{resultList.Count}  [{String.Join(" ", resultList)}]");

            System.Threading.Thread.Sleep(5000);
            Log.Info($"Finishing initialization in {this.GetSensorIdentifier()}");

            // Let PC2MQTT know that we're done and initialized properly.
            // If for some reason your code didn't initialize properly you can return false
            // Just make sure to clean up after yourself as best you can.
            // uninitialized sensors are cleaned up automatically after all sensors are done loading.
            return true;
        }

        /// <inheritdoc cref="SensorBase.IsCompatibleWithCurrentRuntime"/>
        public new bool IsCompatibleWithCurrentRuntime()
        {
            // Simple way to set compatibility..
            bool compatible = true;

            // If incompatible with a specific OS/Runtime, set that one below to false and remove all the others that your sensor runs on
            if (CSScriptLib.Runtime.IsCore) compatible = true;
            if (CSScriptLib.Runtime.IsLinux) compatible = true;
            if (CSScriptLib.Runtime.IsWin) compatible = true;

            return compatible;
        }

        /// <inheritdoc cref="SensorBase.ProcessMessage(MqttMessage)"/>
        public new void ProcessMessage(MqttMessage mqttMessage)
        {
            Log.Info($"[ProcessMessage] Processing topic [{mqttMessage.GetRawTopic()}]: {mqttMessage.message}");

            var topic = mqttMessage.GetTopicWithoutDeviceId();

            // If we receive a message for our unload topic, call sensorHost.Dispose to start the process
            if (mqttMessage.GetTopicWithoutDeviceId() == "example3/unload_example_script" && mqttMessage.message == "unload")
            {
                Log.Info("Disposing of myself in 5 seconds..");
                System.Threading.Thread.Sleep(5000);
                sensorHost.Dispose();
            }
        }

        /// <inheritdoc cref="SensorBase.SensorMain"/>
        public new void SensorMain()
        {
            // We should be in our own thread. Hopefully. I'm still new to threading..
            Log.Debug($"(SensorMain) CPU id: {System.Threading.Thread.GetCurrentProcessorId()} ThreadId: {System.Threading.Thread.CurrentThread.ManagedThreadId}");

            // Below are several different ways to create an MqttMessage object. Pick whichever one works best for you!

            // Subscribe to JUST /example/status topic
            if (!sensorHost.Subscribe(new MqttMessageBuilder().SubscribeMessage.AddDeviceIdToTopic.AddTopic("/example1/status").Build()))
                Log.Info("Failed to subscribe to /example1/status");

            // Since this is a direct MQTT message to /example/status and we're subscribed, we should receive it
            if (!sensorHost.Publish(new MqttMessageBuilder().PublishMessage.AddDeviceIdToTopic.AddTopic("/example1/status").SetMessage("1").DoNotRetain.Build()))
                Log.Info("Failed to publish to /example1/status");

            // subscribe to all levels above and including /example/
            // for example, /example/hello/world/test would trigger.
            var msg = MqttMessageBuilder.NewMessage().SubscribeMessage.AddDeviceIdToTopic.AddTopic("/example2/").AddMultiLevelWildcard.DoNotRetain.Build();
            if (!sensorHost.Subscribe(msg))
                Log.Info("Failed to subscribe to /example2/#");

            var msg2 = MqttMessageBuilder
                .NewMessage()
                .PublishMessage
                .DoNotRetain
                .AddDeviceId
                .AddTopic("/example2/")
                .AddTopic("test")
                .SetMessage("2")
                .Build();

            // We should receive both of these because /# is a multi-level wildcard
            sensorHost.Publish(msg2);

            var msg3 = MqttMessageBuilder
                .NewMessage()
                .PublishMessage
                .DoNotRetain
                .AddDeviceId
                .AddTopic("/example2/test//")
                .AddTopic("should_also_receive")
                .AddTopic("this_message")
                .SetMessage("3")
                .Build();

            sensorHost.Publish(msg3);

            // Subscribe to all /example2/ topics one level up.
            // For example, /example2/hello would trigger
            // but /example2/hello/test would not.
            if (!sensorHost.Subscribe(new MqttMessage().SubscribeMessage.AddDeviceId.AddTopic("/example3/").AddSingleLevelWildcard))
                Log.Info("Failed to subscribe to /example3/+");

            // We should receive the first message but not the second, because /+ is only a single-level wildcard.

            sensorHost.Publish(new MqttMessageBuilder().PublishMessage.AddDeviceId.AddTopic("/example3/test").SetMessage("4").Build());

            sensorHost.Publish(MqttMessageBuilder
                .NewMessage()
                .PublishMessage
                .AddDeviceId
                .AddTopic("/example3/test/should_not_receive")
                .SetMessage("5")
                .DoNotRetain
                .Build());

            Log.Info("In 10 seconds I will send an unload message to /example2/unload_example_script");

            // Set up the timer we declared above for 10 seconds
            unloadTimer = new Timer(10000);

            // We're using a delegate here, but you could call your own method all the same
            // This will run 10 seconds after .Start() is called below
            unloadTimer.Elapsed += delegate
            {
                Log.Info("Sending unload MQTT message to myself");

                if (!sensorHost.Publish(new MqttMessage().PublishMessage.AddDeviceId.AddTopic("/example3/unload_example_script").SetMessage("unload")))
                    Log.Info("Failed to publish to /example3/unload_example_script");
            };
            // Start the timer
            unloadTimer.Start();

            /* This will not send because you cannot have multiple wildcards in the same sub message!
            var dc = new MqttMessageBuilder().SubscribeMessage.AddDeviceId.AddMultiLevelWildcard.AddSingleLevelWildcard.Build();
            sensorHost.Subscribe(dc);
            */

            // At any time you can unsubscribe from all topics but it's not necessary here
            // sensorHost.UnsubscribeAllTopics();

            while (this.IsInitialized)
            {
                Log.Info("If you want, you can stay in control for the life of the sensor using something like this.");
                System.Threading.Thread.Sleep(10000); // You can use a smaller sleep, just make sure you sleep to reduce CPU usage.
            }
        }

        /// <inheritdoc cref="SensorBase.Uninitialize"/>
        public new void Uninitialize()
        {
            // Best to check if it's already been initialized as it may get called a few times to be safe.
            if (IsInitialized)
            {
                Log.Info($"Uninitializing [{GetSensorIdentifier()}]");

                // Don't need to unsubscribe topcs as it's done by PC2MQTT
                // this.sensorHost.Unsubscribe("/example/status");
                // this.sensorHost.Unsubscribe("/example/#");
                // this.sensorHost.Unsubscribe("/example2/+");
                // or simply
                // this.sensorHost.UnsubscribeAllTopics();

                // stop our timer to prevent any issues!
                if (unloadTimer != null) unloadTimer.Stop();
            }
        }
    }
}