namespace Kafka
open System
open System.Collections.Concurrent
open System.Diagnostics
open System.Threading
open Confluent.Kafka
open Microsoft.Extensions.Logging

[<Struct>]
type KafkaMessage<'Key, 'Value>(cr : ConsumeResult<'Key, 'Value>) =
    member internal __.UnderlyingMessage = cr.Message
    member __.Topic = cr.Topic
    member __.Partition = cr.Partition
    member __.Offset = cr.Offset.Value
    member __.Key = cr.Message.Key
    member __.Value = cr.Message.Value
    member __.Timestamp = DateTimeOffset.FromUnixTimeMilliseconds cr.Message.Timestamp.UnixTimestampMs 

type KafkaMessage = KafkaMessage<string, string>

module Core =
    type ConsumerBufferingConfig = { minInFlightBytes : int64; maxInFlightBytes : int64; maxBatchSize : int; maxBatchDelay : TimeSpan }

    module Constants =
        [<Literal>]
        let messageCounterSourceContext = "FsKafka.Core.InFlightMessageCounter"

    type ConterMessage =
        | Delta of int64
        | CheckOverflow of AsyncReplyChannel<bool>
    type InFlightMessageCounter(minInFlightBytes : int64, maxInFlightBytes : int64) =
        do  if minInFlightBytes < 1L then invalidArg "minInFlightBytes" "must be positive value"
            if maxInFlightBytes < 1L then invalidArg "maxInFlightBytes" "must be positive value"
            if minInFlightBytes > maxInFlightBytes then invalidArg "maxInFlightBytes" "must be greater than minInFlightBytes"
    
        let holdOn = new AutoResetEvent(false)
        let inFlightBytes = ref 0L
        let mutable overflow = false
        let innerCounter = MailboxProcessor<ConterMessage>.Start(fun inbox ->
            let rec innerLoop () = 
                async{
                    match! inbox.Receive() with
                    | Delta numBytes ->
                        inFlightBytes := !inFlightBytes + numBytes
                    | CheckOverflow rc -> 
                        if not overflow then
                            overflow <- !inFlightBytes > maxInFlightBytes
                            // block AwaitThreshold flow
                            if overflow then holdOn.Reset() |> ignore
                        rc.Reply(overflow)
                    if overflow && !inFlightBytes <= minInFlightBytes then 
                        // unblock AwaitThreshold flow
                        overflow <- (not << holdOn.Set)()

                    return! innerLoop()
                }
            innerLoop()
        )
        member __.OnCancel = (new Action(fun () -> if overflow then holdOn.Set() |> ignore))
        member __.InFlightMb = float !inFlightBytes / 1024. / 1024.
        member __.Delta(numBytes) = Delta >> innerCounter.Post <| numBytes
        member __.IsOverLimitNow() = 
            innerCounter.PostAndReply(fun rc -> CheckOverflow(rc)) // counter is quick, good to make sync call
        
        member __.AwaitThreshold() = 
            if __.IsOverLimitNow() then
                // log.Information("Consuming... breached in-flight message threshold (now ~{max:n0}B), quiescing until it drops to < ~{min:n1}MB",
                //    !inFlightBytes, float minInFlightBytes / 1024. / 1024.)
                if holdOn.WaitOne() then  // waiting state was set during IsOverLimitNow call
                    printfn "Consumer resuming polling"
                    // log.Verbose "Consumer resuming polling"

open System.Collections.Generic

[<NoComparison>]
type KafkaConsumerConfig = private { inner: ConsumerConfig; topic: string; buffering: Core.ConsumerBufferingConfig; logger : ILogger;} with
    member __.Buffering  = __.buffering
    member __.Inner  = __.inner
    member __.Broker = __.inner.BootstrapServers
    member __.GroupId = __.inner.GroupId
    member __.Logger  = __.logger


    
    /// Builds a Kafka Consumer Config suitable for KafkaConsumer.Start*
    static member Create
        (   logger : ILogger,
            /// Identify this consumer in logs etc
            clientId,
            broker : string,
            topic,
            /// Consumer group identifier.
            groupId,
            /// Specifies handling when Consumer Group does not yet have an offset recorded. Confluent.Kafka default: start from Latest. Default: start from Earliest.
            autoOffsetReset,
            /// Default 100kB. Confluent.Kafka default: 500MB
            fetchMaxBytes,
            /// Default: use `fetchMaxBytes` value (or its default, 100kB). Confluent.Kafka default: 1mB
            ?fetchMinBytes,
            /// Default: use `fetchMaxBytes` value (or its default, 100kB). Confluent.Kafka default: 1mB
            ?messageMaxBytes,
            /// Consumed offsets commit interval. Default 5s.
            ?autoCommitInterval,
            /// Misc configuration parameters to be passed to the underlying CK consumer. Same as constructor argument for Confluent.Kafka >=1.2.
            ?config : IDictionary<string,string>,

            (* Client-side batching / limiting of reading ahead to constrain memory consumption *)
    
            /// Minimum total size of consumed messages in-memory for the consumer to attempt to fill. Default 2/3 of maxInFlightBytes.
            ?minInFlightBytes,
            /// Maximum total size of consumed messages in-memory before broker polling is throttled. Default 24MiB.
            ?maxInFlightBytes,
            /// Message batch linger time. Default 500ms.
            ?maxBatchDelay,
            /// Maximum number of messages to group per batch on consumer callbacks for BatchedConsumer. Default 1000.
            ?maxBatchSize
            ) =
    
        let maxInFlightBytes = defaultArg maxInFlightBytes (16L * 1024L * 1024L)
        let minInFlightBytes = defaultArg minInFlightBytes (maxInFlightBytes * 2L / 3L)
        let fetchMaxBytes = defaultArg fetchMaxBytes 100_000
        let c =
            let customPropsDictionary = 
                config
                    |> Option.defaultValue(Dictionary<string,string>() :> IDictionary<string,string>)
       
            ConsumerConfig(customPropsDictionary, 
                ClientId=clientId, BootstrapServers=broker, GroupId=groupId,
                AutoOffsetReset = Nullable (defaultArg autoOffsetReset AutoOffsetReset.Earliest), // default: latest
                FetchMaxBytes = Nullable fetchMaxBytes, // default: 524_288_000
                MessageMaxBytes = Nullable (defaultArg messageMaxBytes fetchMaxBytes), // default 1_000_000
                EnableAutoCommit = Nullable true, // at AutoCommitIntervalMs interval, write value supplied by StoreOffset call
                EnableAutoOffsetStore = Nullable false, // explicit calls to StoreOffset are the only things that effect progression in offsets
                LogConnectionClose = Nullable false) // https://github.com/confluentinc/confluent-kafka-dotnet/issues/124#issuecomment-289727017
        fetchMinBytes |> Option.iter (fun x -> c.FetchMinBytes <- x) // Fetch waits for this amount of data for up to FetchWaitMaxMs (100)
        autoCommitInterval |> Option.iter<int> (fun x -> c.AutoCommitIntervalMs <- Nullable <| x)
        {   inner = c
            topic = topic
            logger = logger
            buffering = {
                maxBatchDelay = TimeSpan.FromMilliseconds <| defaultArg maxBatchDelay 500; maxBatchSize = defaultArg maxBatchSize 1000
                minInFlightBytes = minInFlightBytes; maxInFlightBytes = maxInFlightBytes }
        }
    
        /// Builds a Kafka Consumer Config suitable for KafkaConsumer.Start*
        static member Create
            (   clientId,broker : string,topic, groupId, enableSSL,
                ?autoOffsetReset,?fetchMaxBytes,?messageMaxBytes,?fetchMinBytes,
                ?autoCommitInterval,?config : IDictionary<string,string>,?minInFlightBytes,?maxInFlightBytes,
                ?maxBatchDelay,?maxBatchSize) =
                KafkaConsumerConfig.Create(clientId,broker,topic,groupId, enableSSL,?autoOffsetReset=autoOffsetReset,
                    ?fetchMaxBytes=fetchMaxBytes,?messageMaxBytes=messageMaxBytes,?fetchMinBytes=fetchMinBytes,
                    ?autoCommitInterval=autoCommitInterval,?config=config,
                    ?minInFlightBytes=minInFlightBytes,?maxInFlightBytes=maxInFlightBytes,?maxBatchDelay=maxBatchDelay,?maxBatchSize=maxBatchSize
                )
open System.Threading.Tasks

type OffsetValue =
    | Unset
    | Valid of value: int64
    override this.ToString() =
        match this with
        | Unset -> "Unset"
        | Valid value -> value.ToString()
module OffsetValue =
    let ofOffset (offset : Offset) =
        match offset.Value with
        | _ when offset = Offset.Unset -> Unset
        | valid -> Valid valid

type ConsumerBuilder =
    static member WithLogging(log : ILogger, config : ConsumerConfig, ?onRevoke) =
        ConsumerBuilder<_,_>(config)
            .SetLogHandler(fun _c m -> log.LogInformation("Consuming... {message} level={level} name={name} facility={facility}", m.Message, m.Level, m.Name, m.Facility))
            .SetErrorHandler(fun _c e -> log.LogError("Consuming... Error reason={reason} code={code} broker={isBrokerError}", e.Reason, e.Code, e.IsBrokerError))
            .SetPartitionsAssignedHandler(fun _c xs ->
                for topic,partitions in xs |> Seq.groupBy (fun p -> p.Topic) |> Seq.map (fun (t,ps) -> t, [| for p in ps -> let p = p.Partition in p.Value |]) do
                    log.LogInformation("Consuming... Assigned {topic:l} {partitions}", topic, partitions))
            .SetPartitionsRevokedHandler(fun _c xs ->
                for topic,partitions in xs |> Seq.groupBy (fun p -> p.Topic) |> Seq.map (fun (t,ps) -> t, [| for p in ps -> let p = p.Partition in p.Value |]) do
                    log.LogInformation("Consuming... Revoked {topic:l} {partitions}", topic, partitions)
                onRevoke |> Option.iter (fun f -> f xs))
            .SetOffsetsCommittedHandler(fun _c cos ->
                for t,ps in cos.Offsets |> Seq.groupBy (fun p -> p.Topic) do
                    let o = seq { for p in ps -> let pp = p.Partition in pp.Value, OffsetValue.ofOffset p.Offset(*, fmtError p.Error*) }
                    let e = cos.Error
                    if not e.IsError then log.LogInformation("Consuming... Committed {topic} {offsets}", t, o)
                    else log.LogWarning("Consuming... Committed {topic} {offsets} reason={error} code={code} isBrokerError={isBrokerError}", t, o, e.Reason, e.Code, e.IsBrokerError))
            .Build()
    
module private ConsumerImpl =
    /// guesstimate approximate message size in bytes
    let approximateMessageBytes (message : ConsumeResult<string, string>) =
        let inline len (x:string) = match x with null -> 0 | x -> sizeof<char> * x.Length
        16 + len message.Message.Key + len message.Message.Value |> int64

    type BlockingCollection<'T> with
        member bc.FillBuffer(buffer : 'T[], maxDelay : TimeSpan) : int =
            let cts = new CancellationTokenSource()
            do cts.CancelAfter maxDelay

            let n = buffer.Length
            let mutable i = 0
            let mutable t = Unchecked.defaultof<'T>

            while i < n && not cts.IsCancellationRequested do
                if bc.TryTake(&t, 5 (* ms *)) then
                    buffer.[i] <- t ; i <- i + 1
                    while i < n && not cts.IsCancellationRequested && bc.TryTake(&t) do 
                        buffer.[i] <- t ; i <- i + 1
            i

    type PartitionedBlockingCollection<'Key, 'Message when 'Key : equality>(?perPartitionCapacity : int) =
        let collections = new ConcurrentDictionary<'Key, Lazy<BlockingCollection<'Message>>>()
        let onPartitionAdded = new Event<'Key * BlockingCollection<'Message>>()

        let createCollection() =
            match perPartitionCapacity with
            | None -> new BlockingCollection<'Message>()
            | Some c -> new BlockingCollection<'Message>(boundedCapacity = c)

        [<CLIEvent>]
        member __.OnPartitionAdded = onPartitionAdded.Publish

        member __.Add (key : 'Key, message : 'Message) =
            let factory key = lazy(
                let coll = createCollection()
                onPartitionAdded.Trigger(key, coll)
                coll)

            let buffer = collections.GetOrAdd(key, factory)
            buffer.Value.Add message

        member __.Revoke(key : 'Key) =
            match collections.TryRemove key with
            | true, coll -> Task.Delay(10000).ContinueWith(fun _ -> coll.Value.CompleteAdding()) |> ignore
            | _ -> ()
    
    let mkBatchedMessageConsumer (config : KafkaConsumerConfig) (ct : CancellationToken) (consumer : IConsumer<string, string>)
            (partitionedCollection: PartitionedBlockingCollection<TopicPartition, ConsumeResult<string, string>>)
            (handler : ConsumeResult<string,string>[] -> Async<unit>) = async {
        let buf = config.buffering 
        let tcs = new TaskCompletionSource<unit>()
        use cts = CancellationTokenSource.CreateLinkedTokenSource(ct)
        use _ = ct.Register(fun _ -> tcs.TrySetResult () |> ignore)

        use _ = consumer

        let counter = new Core.InFlightMessageCounter(buf.minInFlightBytes, buf.maxInFlightBytes)

        // starts a tail recursive loop that dequeues batches for a given partition buffer and schedules the user callback
        let consumePartition (key : TopicPartition) (collection : BlockingCollection<ConsumeResult<string, string>>) =
            let buffer = Array.zeroCreate buf.maxBatchSize

            let swBatch = Stopwatch()
            let nextBatch () =
                swBatch.Restart()

                let count = collection.FillBuffer(buffer, buf.maxBatchDelay)

                if count <> 0 then printfn "Consuming batchCount=%i" count

                let batch = Array.init count (fun i -> buffer.[i])
                Array.Clear(buffer, 0, count)
                batch            

            let batchHandler = 
                    handler

            let sw = Stopwatch()
            let rec loop () = async {
                if not collection.IsCompleted then
                    try match nextBatch() with
                        | [||] -> ()
                        | batch ->
                            let consumeStartTime = DateTimeOffset.Now

                            // run the handler function
                            sw.Restart()                            
                            do! batchHandler batch
                            
                            // store completed offsets
                            let lastItem = batch |> Array.maxBy (fun m -> let o = m.Offset in o.Value)
                            consumer.StoreOffset(lastItem)

                            for message in batch do
                                let lag = max (consumeStartTime - (DateTimeOffset.FromUnixTimeMilliseconds message.Message.Timestamp.UnixTimestampMs)) TimeSpan.Zero
                                printfn  "kafkaConsumerWallTimeLagMs %f" lag.TotalMilliseconds

                            // decrement in-flight message counter
                            let batchSize = batch |> Array.sumBy approximateMessageBytes
                            counter.Delta(-batchSize)
                    with e ->
                        tcs.TrySetException e |> ignore
                        cts.Cancel()
                    return! loop() }

            Async.Start(loop(), cts.Token)

        use _ = partitionedCollection.OnPartitionAdded.Subscribe ((<||) consumePartition)

        // run the consumer
        let ct = cts.Token
        try while not ct.IsCancellationRequested do
                counter.AwaitThreshold() //(fun () -> Thread.Sleep 1)
                try let message = consumer.Consume(ct) // NB TimeSpan overload yields AVEs on 1.0.0-beta2
                    if not (isNull message) then
                        counter.Delta(+approximateMessageBytes message)
                        partitionedCollection.Add(message.TopicPartition, message)
                with
                    | :? ConsumeException as e -> 
                        config.logger.LogWarning(e, "Consuming... exception consumer={name}", consumer.Name)
                    | :? System.OperationCanceledException -> 
                        config.logger.LogWarning("Consuming... cancelled consumer={name}", consumer.Name)
        finally
            consumer.Close()

        // await for handler faults or external cancellation
        return! Async.AwaitTask tcs.Task
    }

/// Creates and wraps a Confluent.Kafka IConsumer, wrapping it to afford a batched consumption mode with implicit offset progression at the end of each
/// (parallel across partitions, sequenced/monotonic within) batch of processing carried out by the `partitionHandler`
/// Conclusion of the processing (when a `partionHandler` throws and/or `Stop()` is called) can be awaited via `AwaitCompletion()`
type BatchedConsumer private (inner : IConsumer<string, string>, task : Task<unit>, triggerStop) =

    member __.Inner = inner

    interface IDisposable with member __.Dispose() = __.Stop()
    /// Request cancellation of processing
    member __.Stop() =  triggerStop ()
    /// Inspects current status of processing task
    member __.Status = task.Status
    member __.RanToCompletion = task.Status = System.Threading.Tasks.TaskStatus.RanToCompletion 
    /// Asynchronously awaits until consumer stops or is faulted
    member __.AwaitCompletion() = Async.AwaitTask task

    /// Starts a Kafka consumer with the provided configuration. Batches are grouped by topic partition.
    /// Batches belonging to the same topic partition will be scheduled sequentially and monotonically; however batches from different partitions can run concurrently.
    /// Completion of the `partitionHandler` saves the attained offsets so the auto-commit can mark progress; yielding an exception terminates the processing
    static member Start(config : KafkaConsumerConfig, partitionHandler : ConsumeResult<string,string>[] -> Async<unit>) =
        if String.IsNullOrEmpty config.topic then invalidArg "config" "must specify at least one topic"
        config.logger.LogInformation("Consuming... broker={broker} topic={topic} groupId={groupId} autoOffsetReset={autoOffsetReset} fetchMaxBytes={fetchMaxB} maxInFlight={maxInFlightGB:n1}MB maxBatchDelay={maxBatchDelay}s maxBatchSize={maxBatchSize}",
            config.inner.BootstrapServers, config.topic, config.inner.GroupId, (let x = config.inner.AutoOffsetReset in x.Value), config.inner.FetchMaxBytes,
            float config.buffering.maxInFlightBytes / 1024. / 1024., (let t = config.buffering.maxBatchDelay in t.TotalSeconds), config.buffering.maxBatchSize)
        let partitionedCollection = new ConsumerImpl.PartitionedBlockingCollection<TopicPartition, ConsumeResult<string, string>>()
        let onRevoke (xs : seq<TopicPartitionOffset>) = 
            for x in xs do
                partitionedCollection.Revoke(x.TopicPartition)
        let consumer : IConsumer<string,string> = ConsumerBuilder.WithLogging(config.logger, config.inner, onRevoke = onRevoke)
        let cts = new CancellationTokenSource()
        let triggerStop () =
            config.logger.LogInformation("Consuming... Stopping consumer={name}", consumer.Name)
            cts.Cancel()
        let task = ConsumerImpl.mkBatchedMessageConsumer config cts.Token consumer partitionedCollection partitionHandler |> Async.StartAsTask
        let c = new BatchedConsumer(consumer, task, triggerStop)
        consumer.Subscribe config.topic
        c

    /// Starts a Kafka consumer instance that schedules handlers grouped by message key. Additionally accepts a global degreeOfParallelism parameter
    /// that controls the number of handlers running concurrently across partitions for the given consumer instance.
    static member StartByKey(config : KafkaConsumerConfig, degreeOfParallelism : int, keyHandler : ConsumeResult<_,_> [] -> Async<unit>) =
        let semaphore = new SemaphoreSlim(degreeOfParallelism)
        let partitionHandler (messages : ConsumeResult<_,_>[]) = async {
            return!
                messages
                |> Seq.groupBy (fun m -> m.Message.Key) // group by userId
                |> Seq.map (fun (_,gp) -> async { 
                    let! ct = Async.CancellationToken
                    let! _ = semaphore.WaitAsync ct |> Async.AwaitTask
                    try 
                        do! keyHandler (Seq.toArray gp)
                    finally 
                        semaphore.Release() |> ignore 
                   })
                |> Async.Parallel
                |> Async.Ignore
        }

        BatchedConsumer.Start(config, partitionHandler)