namespace ZeroMev.Shared
{
    public static class Config
    {
        public static AppSettings Settings = new AppSettings();
    }

    public class AppSettings
    {
        // hardcode these Blazor client settings
        // (this avoids the overhead of adding Microsoft Configuration Extensions to the Blazor payload)
        // other projects will overwrite these from appsettings.json
        public string EthereumRPC { get; set; } = "your_ethereum_full_node_rpc_url";
        public string EthplorerAPIKey { get; set; } = "your_ethereum_full_node_web_socket_url";
        public string EtherscanAPIKey { get; set; } = "your_etherscan_api_key";
        public string ImagesUrl { get; set; } = "https://ethplorer.io/images/";

        public string EthereumWSS { get; set; }
        public short ExtractorIndex { get; set; } = 10000;
        public int PurgeAfterDays { get; set; } = 14;
        public bool DoExtractFlashbots { get; set; } = true;
        public string ZeromevAPI { get; set; }
        public string MevDB { get; set; }
        public string DB { get; set; }
        public string MevWebDB { get; set; }
        public string MevApiDB { get; set; }
        public long? ImportZmBlocksFrom { get; set; }
        public long? ImportZmBlocksTo { get; set; }
        public int? BlockBufferSize { get; set; }
        public bool FastImport { get; set
    }
}