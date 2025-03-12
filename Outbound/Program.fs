open System
open Microsoft.Extensions.Logging
open Newtonsoft.Json
open Infrastructure.Postgres.Context
open Infrastructure.Postgres.Repository
open Config
open Initializer
open Contracts.DTO
open Confluent.Kafka
open System.Collections.Generic

let loggerFactory = LoggerFactory.Create(fun builder -> builder.AddConsole() |> ignore)
let logger = loggerFactory.CreateLogger("KafkaConnector")


let private decode (logger : ILogger) (msi:ConsumeResult<string,string>) : LogDataMessage option =
    try
        msi.Message.Value
        |> JsonConvert.DeserializeObject<LogDataMessage>
        |> Some
    with ex ->
        logger.LogError(ex, "Could not decode {value} due to {ex}", msi.Message.Value, ex.Message)
        None


let getLastEventForUser (dbContext : AppDbContext) (userId : int64) =
    query {
        for user in dbContext.Users do
        where (user.UserId = userId)        
        select user.LastEventId
        headOrDefault
    } 

let storeBatch (dbContext : AppDbContext) (userEntity: UserEntity) (streamId, msgs: LogDataMessage seq) =
    async{        
        let lastEvent = getLastEventForUser dbContext streamId
        do! msgs 
            |> Seq.filter (fun msg -> msg.LogData.EventId > lastEvent)
            |> Seq.sortBy (fun msg -> msg.LogData.EventId)
            |> Seq.map (userEntity.StoreLogRecord >> Async.AwaitTask)
            |> Async.Sequential
            |> Async.Ignore
    }    

let private doProcessing (logger : ILogger) (kafkaMsgs : ConsumeResult<string, string>[]) = async {
    use dbContext = new AppDbContext(config.postgres.``connection-string``)
    let userEntity = UserEntity(logger, dbContext)
    
    do! kafkaMsgs
        |> Seq.map (decode logger)
        |> Seq.choose id
        |> Seq.groupBy (fun x -> x.LogData.UserId)
        |> Seq.map (storeBatch dbContext userEntity)
        |> Async.Sequential
        |> Async.Ignore
    do! userEntity.Save() |> Async.AwaitTask |> Async.Ignore
 }

let start () = async {    
    let degreeOfParallelism = 4 * System.Environment.ProcessorCount
    
    let consumerConfig =
        KafkaConsumerConfig.FromYamlConfiguration(logger, config)
        
    use consumer = KafkaConsumer.Start consumerConfig (doProcessing logger)
    do! consumer.AwaitCompletion()
}

[<EntryPoint>]
let main _ = start () |> MicroserviceSetup.runAsyncService