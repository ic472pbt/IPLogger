module Config
    open FSharp.Configuration
    type ServiceConfig = YamlConfig<"config.yml">
    let config = ServiceConfig()
