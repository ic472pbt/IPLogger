using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Kafka
{
    public class Connector(ILogger logger, string bootstrapServers, string topic)
    {
        private readonly string _bootstrapServers = bootstrapServers;
        private readonly string _topic = topic;

        public IProducer<string, string> CreateProducer()
        {
            var config = new ProducerConfig { BootstrapServers = _bootstrapServers };
            return new ProducerBuilder<string, string>(config).Build();
        }

        public async Task ProduceMessageAsync(string key, string message)
        {
            using var producer = CreateProducer();
            var result = await producer.ProduceAsync(_topic, new Message<string, string> {Key = key, Value = message });
            logger.LogInformation("Message '{Message}' of '{TopicPartitionOffset}' delivered.", result.Value, result.TopicPartitionOffset);
        }

        public IConsumer<string, string> CreateConsumer(string groupId)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = _bootstrapServers,
                GroupId = groupId,
                AutoOffsetReset = AutoOffsetReset.Earliest
            };
            return new ConsumerBuilder<string, string>(config).Build();
        }

        public void ConsumeMessages(CancellationToken cancellationToken)
        {
            using var consumer = CreateConsumer("consumer-group");
            consumer.Subscribe(_topic);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var consumeResult = consumer.Consume(cancellationToken);
                    logger.LogInformation("Consumed message '{Message}' at: '{TopicPartitionOffset}'.", consumeResult.Message.Value, consumeResult.TopicPartitionOffset);
                }
            }
            catch (OperationCanceledException)
            {
                consumer.Close();
            }
        }
    }
}
