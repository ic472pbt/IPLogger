module MicroserviceSetup

open System
open Microsoft.Extensions.Logging
open System.Threading
open System.Runtime.InteropServices

let loggerFactory = LoggerFactory.Create(fun builder -> builder.AddConsole() |> ignore)
let logger = loggerFactory.CreateLogger("MicroserviceSetup")

let processName = System.Reflection.Assembly.GetEntryAssembly().GetName().Name
let processId = System.Diagnostics.Process.GetCurrentProcess().Id

let osPlatform =
    if RuntimeInformation.IsOSPlatform OSPlatform.Windows then "windows"
    elif RuntimeInformation.IsOSPlatform OSPlatform.Linux then "linux"
    elif RuntimeInformation.IsOSPlatform OSPlatform.OSX then "osx"
    else "unspecified"


/// Runs service from the top-level main function
/// ensuring that basic logging setup and logging is performed
let runAsyncService (service : Async<unit>) : int =

    let rec heartbeat() = async {
        let mutable availableWorkerThreads, availableCompletionPortThreads, maxWorkerThreads, maxCompletionPortThreads = 0, 0, 0, 0 
        ThreadPool.GetAvailableThreads(&availableWorkerThreads, &availableCompletionPortThreads)
        ThreadPool.GetMaxThreads(&maxWorkerThreads, &maxCompletionPortThreads)

        let commonHearbeatOutput =
            sprintf "Heartbeat Service=%s Platform=%s PID=%d maxWorkerThreads=%d maxCompletionPortThreads=%d availableWorkerThreads=%d availableCompletionPortThreads=%d allocatedWorkerThreads=%d allocatedCompletionPortThreads=%d"
                processName
                osPlatform
                processId
                maxWorkerThreads
                maxCompletionPortThreads
                availableWorkerThreads
                availableCompletionPortThreads
                (maxWorkerThreads - availableWorkerThreads)
                (maxCompletionPortThreads - availableCompletionPortThreads)

        // Capture Garbage Collection data
        let heartBeatOutput =
            commonHearbeatOutput

        logger.LogInformation heartBeatOutput

        do! Async.Sleep 5000
        return! heartbeat()
    }

    async {
        let! _ = Async.StartChild(heartbeat()) in ()

        let! result = Async.Catch service
        
        match result with
        | Choice1Of2 () ->
            // long running services are not expected to yield, throw an exception if they do
            let message =
                sprintf "Service=%s Platform=%s halted execution without errors."
                    processName 
                    osPlatform

            logger.LogError message
            return raise (Exception message)

        | Choice2Of2 e ->
            logger.LogError(e, "Service={service} Platform={platform} faulted with unhandled exception=%O",
                processName, osPlatform)
            do! Async.Sleep(5000)
            return raise e

    } |> Async.RunSynchronously


/// Runs service from the top-level main function
let runService (service : unit -> unit) : int =
    runAsyncService (async { service () })