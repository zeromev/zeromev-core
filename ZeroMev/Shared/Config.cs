namespace ZeroMev.Shared
{
    public static class Config
    {
        public static AppSettings Settings = new AppSettings();
    }
    public class AppSettings
    {
        // hardcode Blazor client settings
        // (this avoids the overhead of adding Microsoft Configuration Extensions to the Blazor payload)
        // other projects will overwrite these from appsettings.json

        public string EthereumRPC { get; set; } = "your_ethereum_full_node_rpc_url";
        public string EthplorerAPIKey { get; set; } = "your_ethereum_full_node_web_socket_url";
        public string EtherscanAPIKey { get; set; } = "your_etherscan_api_key";
        public string EthereumWSS { get; set; }
        public string ImagesUrl { get; set; } = "https://ethplorer.io/images/";
        public short ExtractorIndex { get; set; }
        public int PurgeAfterDays { get; set; }
        public bool DoExtractFlashbots { get; set; } = false;
        public string ZeromevAPI { get; set; }
        public string MevDB { get; set; }
        public string DB { get; set; }
        public string MevWebDB { get; set; }
        public string MevApiDB { get; set; }
        public long? ImportZmBlocksFrom { get; set; }
        public long? ImportZmBlocksTo { get; set; }
        public int? BlockBufferSize { get; set; } = 7200 * 15; // approx 15 days (1 more than PurgeAfterDays)
        public bool FastImport { get; set; } = false;
    }
}