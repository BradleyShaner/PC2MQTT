using BadLogger;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using static PC2MQTT.MQTT.MqttMessage;

namespace PC2MQTT.MQTT
{
    //public delegate void MessageReceivedByte(string topic, byte[] message);

    public delegate void MessageReceivedString(MqttMessage mqttMessage);

    public delegate void MqttConnectionClosed(string reason, byte errorCode);

    public delegate void MqttConnectionConnected();

    public delegate void MqttMessagePublished(MqttMessage mqttMessage);

    public delegate void MqttReconnecting();

    public delegate void MqttTopicSubscribed(MqttMessage mqttMessage);

    public delegate void MqttTopicUnsubscribed(MqttMessage mqttMessage);

    public class Client : IClient
    {
        public event MqttConnectionClosed ConnectionClosed;

        public event MqttConnectionConnected ConnectionConnected;

        public event MqttReconnecting ConnectionReconnecting;

        public event MqttMessagePublished MessagePublished;

        //public event MessageReceivedByte MessageReceivedByte;

        public event MessageReceivedString MessageReceivedString;

        public event MqttTopicSubscribed TopicSubscribed;

        public event MqttTopicUnsubscribed TopicUnsubscribed;

        //private ConcurrentQueue<MqttMessage> _messageQueue = new ConcurrentQueue<MqttMessage>();

        //private BlockingCollection<MqttMessage> _messageQueue = new BlockingCollection<MqttMessage>();

        public bool IsConnected => client.IsConnected;
        private bool _autoReconnect;
        private BlockingCollection<MqttMessage> _messageQueue = new BlockingCollection<MqttMessage>(new ConcurrentQueue<MqttMessage>(), 500);
        private MqttSettings _mqttSettings;

        private CancellationTokenSource _queueCancellationTokenSource;
        private System.Timers.Timer _reconnectTimer;

        private bool _reconnectTimerStarted;
        private MqttClient client;

        private BadLogger.BadLogger Log;

        public Client(MqttSettings mqttSettings, bool autoReconnect = true)
        {
            this._mqttSettings = mqttSettings;
            this._autoReconnect = autoReconnect;

            Log = LogManager.GetCurrentClassLogger("MqttClient");

            client = new MqttClient(_mqttSettings.broker, _mqttSettings.port, false, null, null, MqttSslProtocols.None);

            client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
            client.ConnectionClosed += Client_ConnectionClosed;

            string clientId = _mqttSettings.deviceId;

            if (autoReconnect)
            {
                _reconnectTimer = new System.Timers.Timer(_mqttSettings.reconnectInterval);
                _reconnectTimer.Elapsed += delegate
                {
                    if (client != null && !client.IsConnected)
                    {
                        ConnectionReconnecting?.Invoke();
                        _queueCancellationTokenSource.Cancel();
                        MqttConnect();
                    }
                };
            }
        }

        public void MqttConnect()
        {
            if ((_autoReconnect) && (!_reconnectTimerStarted))
            {
                _reconnectTimer.Start();
                _reconnectTimerStarted = true;
            }

            if (client.IsConnected)
                client.Disconnect();

            System.Threading.Thread.Sleep(10);

            var offlineWill = MqttMessageBuilder
                .NewMessage()
                .AddDeviceIdToTopic
                .AddTopic(_mqttSettings.will.topic)
                .Build();

            byte conn = client.Connect(_mqttSettings.deviceId, _mqttSettings.user, _mqttSettings.password, true, MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                _mqttSettings.will.enabled, offlineWill.GetRawTopic(), _mqttSettings.will.offlineMessage, false, _mqttSettings.will.keepAlive);

            if (client.IsConnected)
            {
                var onlineWill = MqttMessageBuilder
                    .NewMessage()
                    .AddDeviceIdToTopic
                    .AddTopic(_mqttSettings.will.topic)
                    .SetMessage(_mqttSettings.will.onlineMessage)
                    .SetMessageType(MqttMessageType.MQTT_PUBLISH)
                    .Build();

                Publish(onlineWill);
                ConnectionConnected?.Invoke();
            }
            else
            {
                switch (conn)
                {
                    case MqttMsgConnack.CONN_REFUSED_IDENT_REJECTED:
                        ConnectionClosed?.Invoke("Ident rejected by server", conn);
                        break;

                    case MqttMsgConnack.CONN_REFUSED_NOT_AUTHORIZED:
                        ConnectionClosed?.Invoke("User not authorized", conn);
                        break;

                    case MqttMsgConnack.CONN_REFUSED_PROT_VERS:
                        ConnectionClosed?.Invoke("Protocol version mismatch", conn);
                        break;

                    case MqttMsgConnack.CONN_REFUSED_SERVER_UNAVAILABLE:
                        ConnectionClosed?.Invoke("Server unavailable", conn);
                        break;

                    case MqttMsgConnack.CONN_REFUSED_USERNAME_PASSWORD:
                        ConnectionClosed?.Invoke("User/Pass error", conn);
                        break;

                    default:
                        ConnectionClosed?.Invoke("Unknown error", conn);
                        break;
                }
            }

            _queueCancellationTokenSource = new CancellationTokenSource();

            Task t;
            t = Task.Run(() => ProcessMessageQueue(), _queueCancellationTokenSource.Token);
        }

        public void MqttDisconnect()
        {
            client.Disconnect();
            ConnectionClosed?.Invoke("Disconnected by MqttDisconnect", 98);
            client = null;
        }

        public MqttMessage Publish(MqttMessage mqttMessage)
        {
            // todo: Check for connectivity? Or QoS level if message will be retained
            if (mqttMessage.GetRawMessage() == null || mqttMessage.GetRawMessage().Length == 0)
            {
                mqttMessage.messageId = client.Publish(mqttMessage.GetRawTopic(),
                    Encoding.UTF8.GetBytes(mqttMessage.message),
                    MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                    mqttMessage.retain);
            } 
            else
            {
                mqttMessage.messageId = client.Publish(mqttMessage.GetRawTopic(),
                    mqttMessage.rawMessage,
                    MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                    mqttMessage.retain);
            }

            if (mqttMessage.messageId > 0) MessagePublished?.Invoke(mqttMessage);

            return mqttMessage;
        }

        public bool QueueMessage(MqttMessage message)
        {
            _messageQueue.Add(message);

            return true;
        }

        public MqttMessage SendMessage(MqttMessage message) => ProcessMessage(message);

        public MqttMessage Subscribe(MqttMessage mqttMessage)
        {
            mqttMessage.messageId = client.Subscribe(new string[] { mqttMessage.GetRawTopic() }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });

            if (mqttMessage.messageId > 0) TopicSubscribed?.Invoke(mqttMessage);

            return mqttMessage;
        }

        public MqttMessage Unsubscribe(MqttMessage mqttMessage)
        {
            mqttMessage.messageId = client.Unsubscribe(new string[] { mqttMessage.GetRawTopic() });

            if (mqttMessage.messageId > 0) TopicUnsubscribed?.Invoke(mqttMessage);

            return mqttMessage;
        }

        private void Client_ConnectionClosed(object sender, EventArgs e) => ConnectionClosed?.Invoke($"Connection closed:", 99);

        private void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            var msg = MqttMessageBuilder
                .NewMessage()
                .AddTopic(e.Topic)
                .SetMessage(Encoding.UTF8.GetString(e.Message))
                .SetRawMessage(e.Message)
                .SetMessageType(MqttMessageType.MQTT_PUBLISH)
                .Build();

            MessageReceivedString?.Invoke(msg);
        }

        private MqttMessage ProcessMessage(MqttMessage mqttMessage)
        {
            switch (mqttMessage.messageType)
            {
                case MqttMessage.MqttMessageType.MQTT_PUBLISH:
                    this.Publish(mqttMessage);
                    break;

                case MqttMessage.MqttMessageType.MQTT_SUBSCRIBE:
                    this.Subscribe(mqttMessage);
                    break;

                case MqttMessage.MqttMessageType.MQTT_UNSUBSCRIBE:
                    this.Unsubscribe(mqttMessage);
                    break;
            }

            return mqttMessage;
        }

        private void ProcessMessageQueue()
        {
            Log.Verbose("Starting Message Queue Processing..");
            while (!_queueCancellationTokenSource.Token.IsCancellationRequested)
            {
                if (client.IsConnected)
                {
                    var msg = _messageQueue.Take();

                    Log.Verbose($"Process msg queue: [{msg.messageType}] {msg.GetRawTopic()}: {msg.message}");
                    ProcessMessage(msg);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }

        /*
        private void Client_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            MessageReceivedByte?.Invoke(e.Topic.ResultantTopic(false), e.Message);
            MessageReceivedString?.Invoke(e.Topic.ResultantTopic(false), Encoding.UTF8.GetString(e.Message));
        }
        */
    }

    public class MqttSettings
    {
        public string broker = "";
        public string deviceId = "PC2MQTT";
        public string password = "";
        public int port = 1883;
        public byte publishQosLevel = MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE;
        public int reconnectInterval = 10000;
        public byte subscribeQosLevel = MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE;
        public bool useFakeMqttDelays = true;
        public bool useFakeMqttFailures = false;
        public bool useFakeMqttServer = false;
        public string user = "";
        public MqttWill will = new MqttWill();
    }

    public class MqttWill
    {
        public bool enabled = true; // WillFlag
        public ushort keepAlive = 60000;
        public string offlineMessage = "Offline";
        public string onlineMessage = "Online";
        public byte qosLevel = MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE;
        public bool retain = true;
        public string topic = "/status";
    }
}