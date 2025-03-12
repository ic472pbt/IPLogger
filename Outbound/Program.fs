open Microsoft.Extensions.Logging
open Newtonsoft.Json
open Infrastructure.Postgres.Context
open Infrastructure.Postgres.Repository
open Config
open Initializer
open Contracts.DTO
open Confluent.Kafka

let loggerFactory = LoggerFactory.Create(fun builder -> builder.AddConsole() |> ignore)
let logger = loggerFactory.CreateLogger("KafkaConnector")
do
    use initDb = new AppDbContext(config.postgres.``connection-string``)
    initDb.Database.EnsureCreated() |> ignore

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

let storeBatch (streamId, msgs: LogDataMessage seq) =
    async{        
        use dbContext = new AppDbContext(config.postgres.``connection-string``)
        let userEntity = UserEntity(logger, dbContext)
        let lastEvent = getLastEventForUser dbContext streamId
        let lastConnectionOpt =
            msgs 
            |> Seq.filter (fun msg -> msg.LogData.EventId > lastEvent)
            |> Seq.sortBy (fun msg -> msg.LogData.EventId)
            // ! Sync query (TODO: implement partitionwise Mailbox)
            |> Seq.map (fun msg -> userEntity.StoreLogRecord msg |> Async.AwaitTask |> Async.RunSynchronously)
            |> Seq.tryLast
        match lastConnectionOpt with
        | Some lastConnection ->
            do! userEntity.Save() |> Async.AwaitTask |> Async.Ignore
            
            // Store the last connection id
            lastConnection.UserData.LastConnectionId <- 
                if lastConnection.UserData.LastEventIsIPv6 then
                    lastConnection.ConnectionDataV6.ConnectionDataId
                else
                    lastConnection.ConnectionDataV4.ConnectionDataId
            do! userEntity.Save() |> Async.AwaitTask |> Async.Ignore
        | None -> ()
    }    

let private doProcessing (logger : ILogger) (kafkaMsgs : ConsumeResult<string, string>[]) = async {    
    do! kafkaMsgs
        |> Seq.map (decode logger)
        |> Seq.choose id
        |> Seq.groupBy (fun x -> x.LogData.UserId)
        |> Seq.map storeBatch 
        |> Async.Sequential
        |> Async.Ignore
 }

let start () = async {    
    // let degreeOfParallelism = 4 * System.Environment.ProcessorCount
    
    let consumerConfig =
        KafkaConsumerConfig.FromYamlConfiguration(logger, config)
        
    use consumer = KafkaConsumer.Start consumerConfig (doProcessing logger)
    do! consumer.AwaitCompletion()
}

[<EntryPoint>]
let main _ = start () |> MicroserviceSetup.runAsyncService