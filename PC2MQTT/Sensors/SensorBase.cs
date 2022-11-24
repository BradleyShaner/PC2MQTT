using BadLogger;
using PC2MQTT.MQTT;
using System;
using System.Collections.Generic;
using System.Text;

namespace PC2MQTT.Sensors
{
    /// <summary>
    /// This is the BASE sensor class, inheriting from ISensor.
    /// You can override these methods (with the 'new' keyword) or simply not include them in your sensor to retain their default functionality.
    /// </summary>
    public partial class SensorBase : ISensor
    {
        /// <summary>
        /// If the sensor was initialized properly from <see cref="Initialize(SensorHost)"/>.
        /// </summary>
        public bool IsInitialized { get; set; }

        /// <summary>
        /// This should be saved from <see cref="Initialize(SensorHost)"/> , passed from the parent sensorHost.
        /// </summary>
        public SensorHost sensorHost { get; set; }

        /// <summary>
        /// My own terrible logger so we can pass log messages.
        /// </summary>
        public BadLogger.BadLogger Log;

        /// <summary>
        /// This should always return true. This is a simple test to see if the sensor was loaded properly.
        /// </summary>
        /// <returns>If this returns true, the sensor was loaded and compiled properly.</returns>
        public bool DidSensorCompile() => true;

        /// <summary>
        /// You can call this directly if you want to stop and unload the sensor.
        /// </summary>
        /// <remarks>
        /// PC2MQTT will also call this if the sensor is not marked IsInitialized.
        /// </remarks>
        public void Dispose()
        {
            if (IsInitialized)
                Log.Debug($"Disposing [{GetSensorIdentifier()}]");
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Will return the class instance's name through reflection.
        /// </summary>
        /// <returns>The class instance's name.</returns>
        public string GetSensorIdentifier() => this.GetType().Name;


        /// <summary>
        /// This is called by PC2MQTT after compiling the sensor.
        /// </summary>
        /// <remarks>        
        /// Load any databases, connect to any services, spin up any servers, etc.
        /// Control will be returned to the sensor in <see cref="SensorMain"/> after all sensors have loaded.
        /// Note that sensor scripts are non-blocking so other scripts will run as well, but if you take too long without
        /// returning here your sensor will be disposed because it did not initialize in a timely manner.
        /// </remarks>
        /// <param name="sensorHost"></param>
        /// <returns>True if initialized successfully, false if not.</returns>
        public bool Initialize(SensorHost sensorHost)
        {
            Log = LogManager.GetCurrentClassLogger(GetSensorIdentifier());

            this.sensorHost = sensorHost;

            Log.Info($"Finishing initialization in {this.GetSensorIdentifier()}");

            return true;
        }

        /// <summary>
        /// This is called by the <see cref="SensorHost"/> to verify if the current OS/Runtime is supported by the sensor.
        /// Return false if your sensor is not going to work properly.
        /// </summary>
        /// <returns>Returns true by default.</returns>
        public bool IsCompatibleWithCurrentRuntime()
        {
            bool compatible = true;

            if (CSScriptLib.Runtime.IsCore) compatible = true;
            if (CSScriptLib.Runtime.IsLinux) compatible = true;
            if (CSScriptLib.Runtime.IsWin) compatible = true;

            return compatible;
        }

        /// <summary>
        /// This is called by PC2MQTT when a topic this Sensor has subscribed to has received a <see cref="MqttMessage"/>.
        /// </summary>
        /// <param name="mqttMessage"></param>
        public void ProcessMessage(MqttMessage mqttMessage)
        {
            Log.Info($"{mqttMessage.GetRawTopic()}: {mqttMessage.message}");
        }

        /// <summary>
        /// Control is returned to the sensor from <see cref="SensorHost"/> after initialization.
        /// </summary>
        /// <remarks>
        /// There is no need to exit this method. You can remain in control forever, but I would use a <see cref="System.Threading.CancellationToken"/>
        /// to make sure you can exit here gracefully during a shutdown.
        /// </remarks>
        public void SensorMain()
        {
            Log.Info($"(SensorMain) CPU id: {System.Threading.Thread.GetCurrentProcessorId()} ThreadId: {System.Threading.Thread.CurrentThread.ManagedThreadId}");

        }

        /// <summary>
        /// This is called by PC2MQTT for all sensors when the state of the server connection has changed.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="reason"></param>
        public void ServerStateChange(ServerState state, ServerStateReason reason)
        {
            Log.Debug($"ServerStateChange: {state}: {reason}");
        }

        /// <summary>
        /// This is called when the Sensor is being uninitialized.
        /// Either due to shutdown or sensor unload.
        /// </summary>
        /// <remarks>
        /// Be sure to clean up after yourself if you have any custom objects!
        /// </remarks>
        public void Uninitialize()
        {
            if (IsInitialized)
            {
                Log.Info($"Uninitializing [{GetSensorIdentifier()}]");
            }
        }
    }
}
