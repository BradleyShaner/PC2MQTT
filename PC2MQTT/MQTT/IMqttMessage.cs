using static PC2MQTT.MQTT.MqttMessage;

namespace PC2MQTT.MQTT
{
    public interface IMqttMessage2
    {
        public string GetMessage();

        public ushort GetMessageId();

        public byte[] GetRawMessage();

        public string GetTopic();

        public void SetMessage(string message);

        public void SetRawMessage(byte[] message);

        public void SetMessageId(ushort messageId);

        public void SetMessageType(MqttMessageType messageType);

        public void SetRetainFlag(bool retain);

        public void SetTopic(string topic);
    }
}