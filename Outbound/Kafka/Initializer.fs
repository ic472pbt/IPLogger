module Initializer
    open System
    open System.Collections.Generic
    open Microsoft.Extensions.Logging
    open Config
    open Kafka
    open Confluent.Kafka

    type KafkaConsumerConfig =
        static member FromYamlConfiguration(logger : ILogger, config : ServiceConfig) =
            let kafkaConfig = Dictionary<string, string>()
            // TODO: Make configuration options optional
            let clientId = config.kafka.``client-id``
            let fetchMinBytes = config.kafka.CONSUMER_FETCH_MIN_BYTES
            let fetchMaxBytes = config.kafka.CONSUMER_FETCH_MAX_BYTES
            let retries = config.kafka.CONSUMER_RETRIES
            let retryBackOff = config.kafka.CONSUMER_RETRY_BACKOFF_MS
            let maxBatchSize = config.kafka.CONSUMER_MAX_BATCH_SIZE
            let maxBatchDelay = config.kafka.CONSUMER_MAX_BATCH_DELAY_MS
            let offsetCommitInterval = config.kafka.CONSUMER_OFFSET_COMMIT_INTERVAL_MS
            let minInFlightBytes = config.kafka.CONSUMER_MIN_IN_FLIGHT_BYTES
            let maxInFlightBytes = config.kafka.CONSUMER_MAX_IN_FLIGHT_BYTES
            let autoOffsetReset = 
                match config.kafka.CONSUMER_AUTO_OFFSET_RESET with
                    | "earliest" -> AutoOffsetReset.Earliest
                    | "latest" -> AutoOffsetReset.Latest
                    | _ -> invalidArg "autoOffsetRest" "unrecognized AutoOffsetReset variable"

            kafkaConfig.Add("retries", retries.ToString())
            kafkaConfig.Add("retry.backoff.ms", retryBackOff.ToString())
        
            KafkaConsumerConfig.Create(
                logger = logger,
                clientId = clientId,
                broker = config.kafka.``bootstrap-servers``,
                topic = config.kafka.topic,
                groupId = config.kafka.``group-id``,
                autoOffsetReset = Some autoOffsetReset,
                fetchMaxBytes = Some fetchMaxBytes,
                ?fetchMinBytes = Some fetchMinBytes,
                ?autoCommitInterval = Some offsetCommitInterval,
                ?config = (if kafkaConfig.Count > 0 then Some (kafkaConfig :> IDictionary<string, string>) else None),
                ?minInFlightBytes = Some minInFlightBytes,
                ?maxInFlightBytes = Some maxInFlightBytes,
                ?maxBatchDelay = Some maxBatchDelay,
                ?maxBatchSize = Some maxBatchSize)

        static member FromEnvironmentConfiguration(config) =
            KafkaConsumerConfig.FromYamlConfiguration(config)

    type KafkaConsumer =
        static member Start config handler =
            BatchedConsumer.Start(config, handler)
