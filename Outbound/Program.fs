open Microsoft.Extensions.Logging
open Config
open Infrastructure.Kafka

type ControlMessage =
    | Push of string

let kafkaConnector = 
    let loggerFactory = LoggerFactory.Create(fun builder -> builder.AddConsole() |> ignore)
    let logger = loggerFactory.CreateLogger("KafkaConnector")
    Connector(logger, config.kafka.``bootstrap-servers``, config.kafka.topic)


let consumer = kafkaConnector.CreateConsumer(config.kafka.``group-id``)

let rec consume() =
    let records = consumer.Consume(1000)
    records |> Seq.iter (fun record -> printfn "Consumed: %s" record.Value)
    consume()