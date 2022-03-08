using System;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace ZeroMev.Shared
{
    public enum MEVType
    {
        None,
        Swap,
        ContractSwaps,
        Frontrun,
        Sandwich,
        Backrun,
        Arb,
        Liquidation,
        NFT,
        PunkBid,
        PunkAcceptance,
        PunkSnipe
    }

    public enum MEVClass
    {
        Unclassified,
        Positive,
        Neutral,
        Toxic,
        Info
    }

    public enum MEVError
    {
        None,
        Unknown,
        Reverted,
        OutOfGas
    }

    public enum ProtocolSwap
    {
        Unknown,
        Uniswap2,
        Uniswap3,
        Curve,
        ZeroX,
        Balancer1,
        Bancor
    }

    public enum ProtocolLiquidation
    {
        Unknown,
        Aave,
        CompoundV2
    }

    public enum ProtocolNFT
    {
        Unknown,
        Opensea
    }

    public interface IMEV
    {
        int? TxIndex { get; } // preferred
        string? TxHash { get; } // suppied if TxIndex not available
        MEVType MEVType { get; }
        MEVClass MEVClass { get; }
        decimal? MEVAmountUsd { get; set; }
        string? MEVDetail { get; set; }
        string? ActionSummary { get; set; }
        string? ActionDetail { get; set; }
        void Cache(MEVBlock2 mevBlock, int mevIndex); // mevIndex is allows mev instances to find other related instances, eg: backrun can cheaply find related sandwiched and frontun txs without duplicating them
    }

    public class Symbol
    {
        public const int EthSymbolIndex = -2;
        public const string UnknownImage = @"/un.png";
        public static Symbol EthSymbol = new Symbol("Eth", "eth.png", "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2");

        public Symbol()
        {
        }

        public Symbol(string name, string image, string tokenAddress)
        {
            Name = name;
            Image = image;
            TokenAddress = tokenAddress;
        }

        [JsonPropertyName("n")]
        public string Name { get; set; }

        [JsonPropertyName("i")]
        public string Image { get; set; }

        [JsonIgnore] // to save storage
        public string TokenAddress { get; set; }

        public string GetImage()
        {
            if (Image == null) return Symbol.UnknownImage;
            return Image;
        }

        public string GetImageUrl()
        {
            if (Image == null) return Symbol.UnknownImage;
            return Config.Settings.ImagesUrl + Image;
        }
    }

    public class MEVBlock2
    {
        public MEVBlock2()
        {
        }

        public MEVBlock2(long blockNumber)
        {
            BlockNumber = blockNumber;
        }

        // persisted
        [JsonPropertyName("bn")]
        public long BlockNumber { get; set; }

        [JsonPropertyName("sb")]
        public List<Symbol> Symbols { get; set; } = new List<Symbol>();

        [JsonPropertyName("eu")]
        public decimal? ETHUSD { get; set; }

        [JsonPropertyName("s")]
        public List<MEVSwap> Swaps { get; set; } = new List<MEVSwap>();

        [JsonPropertyName("c")]
        public List<MEVContractSwaps> ContractSwaps { get; set; } = new List<MEVContractSwaps>();

        [JsonPropertyName("f")]
        public List<MEVFrontrun> Frontruns { get; set; } = new List<MEVFrontrun>();

        [JsonPropertyName("w")]
        public List<MEVSandwiched[]> Sandwiched { get; set; } = new List<MEVSandwiched[]>();

        [JsonPropertyName("b")]
        public List<MEVBackrun> Backruns { get; set; } = new List<MEVBackrun>();

        [JsonPropertyName("a")]
        public List<MEVArb> Arbs { get; set; } = new List<MEVArb>();

        [JsonPropertyName("l")]
        public List<MEVLiquidation> Liquidations { get; set; } = new List<MEVLiquidation>();

        [JsonPropertyName("n")]
        public List<MEVNFT> NFTrades { get; set; } = new List<MEVNFT>();

        // calculated
        [JsonIgnore]
        public MEVSummary[] MEVSummaries { get; private set; }
        [JsonIgnore]
        public int[] MEVClassCount { get; private set; }
        [JsonIgnore]
        public int[] MEVClassAmount { get; private set; }

        public Symbol? GetSymbol(int symbolIndex)
        {
            if (symbolIndex < 0)
            {
                if (symbolIndex == Symbol.EthSymbolIndex)
                    return Symbol.EthSymbol;
                return null;
            }
            return Symbols[symbolIndex];
        }

        public string? GetSymbolName(int symbolIndex)
        {
            if (symbolIndex < 0)
            {
                if (symbolIndex == Symbol.EthSymbolIndex)
                    return "Eth";
                return "";
            }
            return Symbols[symbolIndex].Name;
        }
    }

    public class MEVSwap : IMEV
    {
        public MEVSwap()
        {
        }

        public MEVSwap(int txIndex, ProtocolSwap protocol, int symbolInIndex, int symbolOutIndex, ZMDecimal amountIn, ZMDecimal amountOut, ZMDecimal? inUsdRate, ZMDecimal? outUsdRate)
        {
            TxIndex = txIndex;
            Protocol = protocol;
            SymbolInIndex = symbolInIndex;
            SymbolOutIndex = symbolOutIndex;
            AmountIn = amountIn;
            AmountOut = amountOut;

            // store the output usd because it's smaller than the BigDecimal rate and generally more useful
            if (inUsdRate.HasValue) AmountInUsd = (amountIn * inUsdRate.Value).ToUsd();
            if (outUsdRate.HasValue) AmountOutUsd = (amountOut * outUsdRate.Value).ToUsd();
        }

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

        [JsonIgnore]
        public string TxHash => null;

        [JsonPropertyName("a")]
        public int SymbolInIndex { get; set; }

        [JsonPropertyName("b")]
        public int SymbolOutIndex { get; set; }

        [JsonPropertyName("c")]
        public ZMDecimal AmountIn { get; set; }

        [JsonPropertyName("d")]
        public ZMDecimal AmountOut { get; set; }

        [JsonPropertyName("e")]
        public decimal? AmountInUsd { get; set; }

        [JsonPropertyName("f")]
        public decimal? AmountOutUsd { get; set; }

        [JsonPropertyName("p")]
        public ProtocolSwap Protocol { get; set; }

        [JsonIgnore]
        public ZMDecimal Rate
        {
            get
            {
                return AmountOut / AmountIn;
            }
        }

        [JsonIgnore]
        public MEVType MEVType => MEVType.Swap;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Info;

        [JsonIgnore]
        public decimal? MEVAmountUsd { get; set; } = null;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = "";

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public void Cache(MEVBlock2 mevBlock, int mevIndex)
        {
            StringBuilder sb = new StringBuilder();
            BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
        }

        public void BuildActionSummary(MEVBlock2 mevBlock, StringBuilder sb)
        {
            sb.Append(mevBlock.GetSymbolName(SymbolInIndex));
            sb.Append(" > ");
            sb.Append(mevBlock.GetSymbolName(SymbolOutIndex));
        }

        public void BuildActionDetail(MEVBlock2 mevBlock, StringBuilder sb)
        {
            sb.Append(Protocol);
            sb.Append(" ");
            sb.Append(mevBlock.GetSymbolName(SymbolInIndex));
            sb.Append(" ");
            sb.Append(AmountIn.Shorten());
            sb.Append(" ($");
            sb.Append(AmountInUsd != null ? AmountInUsd.ToString() : "?");
            sb.Append(") > ");
            sb.Append(mevBlock.GetSymbolName(SymbolOutIndex));
            sb.Append(" ");
            sb.Append(AmountOut.Shorten());
            sb.Append(" ($");
            sb.Append(AmountOutUsd != null ? AmountOutUsd.ToString() : "?");
            sb.Append(") @");
            sb.Append(Rate.Shorten());
        }
    }

    public class MEVContractSwaps : IMEV
    {
        public MEVContractSwaps()
        {
        }

        public MEVContractSwaps(int? txIndex)
        {
            TxIndex = txIndex;
        }

        [JsonPropertyName("s")]
        public List<MEVSwap> Swaps { get; set; } = new List<MEVSwap>();

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

        [JsonIgnore]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.ContractSwaps;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Info;

        [JsonIgnore]
        public decimal? MEVAmountUsd { get; set; } = null;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = "";

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public void Cache(MEVBlock2 mevBlock, int mevIndex)
        {
            StringBuilder sb = new StringBuilder();
            BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
        }

        public string BuildActionSummary(MEVBlock2 mevBlock, StringBuilder sb)
        {
            if (Swaps == null || Swaps.Count == 0) return "no swaps";
            Swaps[0].BuildActionSummary(mevBlock, sb);
            if (Swaps.Count > 1)
            {
                sb.Append(" +");
                sb.Append(Swaps.Count - 1);
                sb.Append(" more");
            }
            sb.AppendLine();
            return sb.ToString();
        }

        public string BuildActionDetail(MEVBlock2 mevBlock, StringBuilder sb)
        {
            sb.Append(Swaps.Count);
            sb.AppendLine(" swaps in contract.");
            foreach (var swap in Swaps)
            {
                swap.BuildActionDetail(mevBlock, sb);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }

    public class MEVFrontrun : IMEV
    {
        public MEVFrontrun()
        {
        }

        public MEVFrontrun(int? txIndex, MEVSwap swap)
        {
            TxIndex = txIndex;
            Swap = swap;
        }

        [JsonPropertyName("s")]
        public MEVSwap Swap { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

        [JsonIgnore]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Frontrun;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Toxic;

        [JsonIgnore]
        public decimal? MEVAmountUsd { get; set; } = null;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = "see backrun.";

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public void Cache(MEVBlock2 mevBlock, int mevIndex)
        {
            StringBuilder sb = new StringBuilder();
            Swap.BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            Swap.BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
            return;
        }
    }

    public class MEVSandwiched : IMEV
    {
        public MEVSandwiched()
        {
        }

        public MEVSandwiched(int? txIndex, MEVSwap swap)
        {
            TxIndex = txIndex;
            Swap = swap;
        }

        [JsonPropertyName("s")]
        public MEVSwap Swap { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

        [JsonIgnore]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Sandwich;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Toxic;

        [JsonIgnore]
        public decimal? MEVAmountUsd { get; set; } = null;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = "see backrun.";

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public void Cache(MEVBlock2 mevBlock, int mevIndex)
        {
            StringBuilder sb = new StringBuilder();
            Swap.BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            Swap.BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
            return;
        }
    }

    public class MEVBackrun : IMEV
    {
        public MEVBackrun()
        {
        }

        public MEVBackrun(int? txIndex, MEVSwap swap)
        {
            TxIndex = txIndex;
            Swap = swap;
        }

        [JsonPropertyName("s")]
        public MEVSwap Swap { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

        [JsonIgnore]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Backrun;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Toxic;

        [JsonIgnore]
        public decimal? MEVAmountUsd { get; set; }

        [JsonIgnore]
        public string? MEVDetail { get; set; }

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public void Cache(MEVBlock2 mevBlock, int mevIndex)
        {
            var frontrun = mevBlock.Frontruns[mevIndex].Swap;
            if (frontrun.AmountInUsd == null || Swap.AmountOutUsd == null)
            {
                MEVAmountUsd = null;
                MEVDetail = "missing exchange rates, can't calculate";
            }
            else
            {
                MEVAmountUsd = frontrun.AmountInUsd - Swap.AmountOutUsd;
                MEVDetail = $"backrun (in) ${frontrun.AmountInUsd} - frontrun (out) ${Swap.AmountOutUsd} = victim impact ${frontrun.AmountInUsd - Swap.AmountOutUsd}";
            }

            StringBuilder sb = new StringBuilder();
            Swap.BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            Swap.BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
            return;
        }
    }

    public class MEVArb : IMEV
    {
        public MEVArb()
        {
        }

        public MEVArb(int? txIndex, MEVClass mevClass, decimal? mevAmountUsd)
        {
            TxIndex = txIndex;
            MEVClass = mevClass;
            MEVAmountUsd = mevAmountUsd;
        }

        [JsonPropertyName("s")]
        public List<MEVSwap> Swaps { get; set; } = new List<MEVSwap>();

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

        [JsonIgnore]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Arb;

        [JsonPropertyName("c")]
        public MEVClass MEVClass { get; set; } = MEVClass.Unclassified;

        [JsonPropertyName("u")]
        public decimal? MEVAmountUsd { get; set; }

        [JsonIgnore]
        public string? MEVDetail { get; set; }

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public void Cache(MEVBlock2 mevBlock, int mevIndex)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Swaps.Count);
            sb.Append(" swaps = impact $");
            if (MEVAmountUsd != null)
            {
                sb.Append(MEVAmountUsd.Value.ToString());
                sb.AppendLine(".");
            }
            else
            {
                sb.AppendLine("?");
            }
            BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
        }

        public string BuildActionSummary(MEVBlock2 mevBlock, StringBuilder sb)
        {
            if (Swaps == null || Swaps.Count == 0) return "no swaps.";
            Swaps[0].BuildActionSummary(mevBlock, sb);
            if (Swaps.Count > 1)
            {
                sb.Append(" +");
                sb.Append(Swaps.Count - 1);
                sb.Append(" more");
            }
            sb.AppendLine();
            return sb.ToString();
        }

        public string BuildActionDetail(MEVBlock2 mevBlock, StringBuilder sb)
        {
            sb.Append(Swaps.Count);
            sb.AppendLine(" swaps in arb.");
            foreach (var swap in Swaps)
            {
                swap.BuildActionDetail(mevBlock, sb);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }

    public class MEVLiquidation : IMEV
    {
        public MEVLiquidation()
        {
        }

        public MEVLiquidation(string txHash, ProtocolLiquidation protocol, BigInteger? debtPurchaseAmount, decimal? debtPurchaseAmountUsd, int debtSymbolIndex, BigInteger? receivedAmount, decimal? receivedAmountUsd, int receivedSymbolIndex, bool? isReverted)
        {
            TxHash = txHash;
            Protocol = protocol;
            DebtPurchaseAmount = debtPurchaseAmount;
            DebtPurchaseAmountUsd = debtPurchaseAmountUsd;
            ReceivedAmount = receivedAmount;
            DebtSymbolIndex = debtSymbolIndex;
            MEVAmountUsd = receivedAmountUsd;
            ReceivedSymbolIndex = receivedSymbolIndex;
            IsReverted = isReverted;
        }

        [JsonPropertyName("p")]
        public ProtocolLiquidation Protocol { get; set; }

        [JsonPropertyName("d")]
        public BigInteger? DebtPurchaseAmount { get; set; }

        [JsonPropertyName("du")]
        public decimal? DebtPurchaseAmountUsd { get; set; }

        [JsonPropertyName("r")]
        public BigInteger? ReceivedAmount { get; set; }

        [JsonPropertyName("a")]
        public int DebtSymbolIndex { get; set; }

        [JsonPropertyName("b")]
        public int ReceivedSymbolIndex { get; set; }

        [JsonPropertyName("v")]
        public bool? IsReverted { get; set; }

        [JsonPropertyName("u")]
        public decimal? MEVAmountUsd { get; set; }

        [JsonPropertyName("h")]
        public string TxHash { get; set; }

        [JsonIgnore]
        public int? TxIndex => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Liquidation;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Unclassified;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = "";

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public void Cache(MEVBlock2 mevBlock, int mevIndex)
        {
            StringBuilder sb = new StringBuilder();
            BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
        }

        public string BuildActionSummary(MEVBlock2 mevBlock, StringBuilder sb)
        {
            sb.Append(mevBlock.GetSymbolName(DebtSymbolIndex));
            sb.Append(" $ ");
            sb.Append(mevBlock.GetSymbolName(ReceivedSymbolIndex));
            return sb.ToString();
        }

        public string BuildActionDetail(MEVBlock2 mevBlock, StringBuilder sb)
        {
            // aave protocol.
            // debt purchase amount symbolA $143.11 (2784324.33).
            // received amount usd symbolB $54.17 (243432.11).
            sb.Append(Protocol.ToString());
            sb.AppendLine(" protocol.");

            sb.Append("debt purchase amount ");
            sb.Append(mevBlock.GetSymbolName(DebtSymbolIndex));
            sb.Append(" $");
            if (DebtPurchaseAmountUsd != null)
                sb.Append(DebtPurchaseAmountUsd);
            else
                sb.Append("?");
            sb.Append(" (");
            sb.Append(DebtPurchaseAmount);
            sb.AppendLine(").");

            sb.Append("received amount ");
            sb.Append(mevBlock.GetSymbolName(ReceivedSymbolIndex));
            sb.Append(" $");
            if (MEVAmountUsd != null)
                sb.Append(MEVAmountUsd);
            else
                sb.Append("?");
            sb.Append(" (");
            sb.Append(ReceivedAmount);
            sb.AppendLine(").");

            return sb.ToString();
        }
    }

    public class MEVNFT : IMEV
    {
        public MEVNFT(int? txIndex, ProtocolNFT protocol, int paymentSymbolIndex, string collectionAddress, string tokenId, ZMDecimal? paymentAmount, ZMDecimal? paymentAmountUsd, MEVError? error)
        {
            TxIndex = txIndex;
            Protocol = protocol;
            PaymentSymbolIndex = paymentSymbolIndex;
            PaymentAmount = paymentAmount;
            PaymentAmountUsd = paymentAmountUsd;
            CollectionAddress = collectionAddress;
            TokenId = tokenId;
            Error = error;
        }

        [JsonPropertyName("p")]
        public ProtocolNFT Protocol { get; set; }

        [JsonPropertyName("s")]
        public int PaymentSymbolIndex { get; set; }

        [JsonPropertyName("c")]
        public string CollectionAddress { get; set; }

        [JsonPropertyName("t")]
        public string TokenId { get; set; }

        [JsonPropertyName("a")]
        public ZMDecimal? PaymentAmount { get; set; }

        [JsonPropertyName("u")]
        public ZMDecimal? PaymentAmountUsd { get; set; }

        [JsonPropertyName("e")]
        public MEVError? Error { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

        [JsonIgnore]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.NFT;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Info;

        [JsonIgnore]
        public decimal? MEVAmountUsd { get; set; } = null;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = "";

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public void Cache(MEVBlock2 mevBlock, int mevIndex)
        {
            // Opensea nft protocol
            // link https://opensea.io/assets/0xac3d871d3431847bdff9eebb42eb51ae06e131c3/6016
            // $1313.10 (0.33 WETH)

            StringBuilder sb = new StringBuilder();

            string nftLink = null;
            if (Protocol == ProtocolNFT.Opensea)
            {
                sb.Append("https://opensea.io/assets/");
                sb.Append(CollectionAddress);
                sb.Append(@"/");
                sb.Append(TokenId);
                nftLink = sb.ToString();
            }

            sb.Clear();
            sb.Append("<a href='");
            sb.Append(nftLink);
            sb.Append("'>");
            sb.Append("nft $");
            if (PaymentAmountUsd != null)
                sb.Append(PaymentAmountUsd);
            else
                sb.Append("?");
            sb.Append("</a>");
            ActionSummary = sb.ToString();

            sb.Clear();
            sb.Append(Protocol);
            sb.AppendLine(" nft protocol");
            if (nftLink != null)
            {
                sb.Append("link ");
                sb.AppendLine(nftLink);
            }
            else
            {
                sb.Append("collection ");
                sb.AppendLine(CollectionAddress);
                sb.Append(@"token ");
                sb.AppendLine(TokenId);
            }
            sb.Append("payment $");
            if (PaymentAmountUsd != null)
                sb.Append(PaymentAmountUsd);
            else
                sb.Append("?");
            sb.Append(" (");
            sb.Append(PaymentAmount);
            sb.Append(" ");
            sb.Append(mevBlock.GetSymbolName(PaymentSymbolIndex));
            sb.Append(")");
            if (Error != MEVError.None)
            {
                sb.AppendLine();
                sb.AppendLine(Error.ToString());
            }
            ActionDetail = sb.ToString();
        }
    }

    public class MEVBlock
    {
        public long BlockNumber { get; private set; }
        public MEVSummary[] MEVSummaries { get; private set; }
        public int MEVCount { get; private set; }
        public int MEVOtherCount { get; private set; }
        public int MEVToxicCount { get; private set; }
        public decimal MEVAmount { get; private set; }
        public decimal MEVOtherAmount { get; private set; }
        public decimal MEVToxicAmount { get; private set; }

        public MEVRow[] Rows;

        public MEVBlock(long blockNumber)
        {
            this.BlockNumber = blockNumber;
        }

        public void BuildMEVSummaries()
        {
            if (Rows == null)
                return;

            MEVSummaries = new MEVSummary[MEV.Rows.Length];

            MEVCount = 0;
            MEVOtherCount = 0;
            MEVToxicCount = 0;

            MEVAmount = 0;
            MEVOtherAmount = 0;
            MEVToxicAmount = 0;

            foreach (var row in Rows)
            {
                if (row.MEVType == MEVType.None) continue;
                int mi = (int)row.MEVType;
                if (MEV.Rows[mi].Parent != 0) continue;
                MEVSummaries[mi].Count++;
                decimal mevAmount = 0;
                if (row.MEVAmount.HasValue)
                    mevAmount = row.MEVAmount.Value;

                MEVSummaries[mi].Amount += mevAmount;

                if (MEV.Rows[mi].IsVisible)
                {
                    if (MEV.Rows[mi].IsToxic)
                    {
                        MEVToxicCount++;
                        MEVToxicAmount += mevAmount;
                    }
                    else
                    {
                        MEVOtherCount++;
                        MEVOtherAmount += mevAmount;
                    }
                    MEVCount++;
                    MEVAmount += mevAmount;
                }
            }
        }

        public void MockMEV(int txCount)
        {
            // temporarily mock-up mev types to allow for front-end development
            if (txCount < 3) return;

            // use the block number as the seed to the mev is reproduced for each block
            Random r = new Random((int)BlockNumber);
            const string comment = "<p>Some information about this mev attack</p><p>not sure how much</p><p>could include a list of txs</p><p>tx1...</p><p>tx2...</p><p>tx3...</p>";
            List<MEVRow> rows = new List<MEVRow>();

            // put a sandwich at the start of every block (likely fairly accurate!)
            int i = 0;
            rows.Add(new MEVRow(i++, MEVType.Frontrun, null, comment));
            rows.Add(new MEVRow(i++, MEVType.Sandwich, MockAmount(r), comment));
            rows.Add(new MEVRow(i++, MEVType.Backrun, null, comment));

            // generate mock mev
            for (; i < txCount; ++i)
            {
                for (int k = 0; k < MEV.MockProbs.Length; ++k)
                {
                    if (MEV.MockProbs[k] != 0 && r.NextDouble() < MEV.MockProbs[k])
                    {
                        decimal? amount = null;
                        if ((MEVType)k != MEVType.Swap)
                            amount = MockAmount(r);
                        rows.Add(new MEVRow(i, (MEVType)k, amount, comment));
                        break;
                    }
                }
            }
            Rows = rows.ToArray();
        }

        public decimal MockAmount(Random r)
        {
            return (decimal)r.NextDouble() * -120;
        }
    }

    public struct MEVSummary
    {
        public MEVType MevType;
        public decimal Count;
        public decimal Amount;
    }

    public class MEVRow
    {
        public MEVRow(int index, MEVType mevType, decimal? mevAmount, string mevComment)
        {
            Index = index;
            MEVType = mevType;
            MEVAmount = mevAmount;
            MEVComment = mevComment;
        }

        public int Index { get; set; }
        public MEVType MEVType { get; set; }
        public decimal? MEVAmount { get; set; }
        public string MEVComment { get; set; }
    }

    public struct MEVDisplay
    {
        public readonly int Index;
        public readonly MEVType Type;
        public readonly string Name;
        public readonly bool IsVisible;
        public readonly bool IsToxic;
        public readonly string CssClass;
        public readonly int Parent;

        public MEVDisplay(int index, MEVType type, string name, bool isVisible, bool isToxic, string cssClass, int parent)
        {
            Index = index;
            Type = type;
            Name = name;
            IsVisible = isVisible;
            IsToxic = isToxic;
            CssClass = cssClass;
            Parent = parent;
        }

        public bool DoDisplay
        {
            get
            {
                return IsVisible && Parent == 0;
            }
        }
    }

    public class MEV
    {
        public static MEVDisplay[] Rows = {
            new MEVDisplay(0, MEVType.None, "", false, false, "mev-inactive", 0), // TODO don't need cssClass. Colour is set by MEVClass not MEVType
            new MEVDisplay(1, MEVType.Swap, "Swap", true, false, "mev-swap", 0),
            new MEVDisplay(2, MEVType.Frontrun, "Frontrun", true, true, "mev-fr", 9),
            new MEVDisplay(3, MEVType.Sandwich, "Sandwich", true, true, "mev-sw", 0),
            new MEVDisplay(4, MEVType.Backrun, "Backrun", true, true, "mev-br", 9),
            new MEVDisplay(5, MEVType.Arb, "Arb", true, false, "mev-arb", 0),
            new MEVDisplay(6, MEVType.Liquidation, "Liquidation", true, false, "mev-liq", 0),
            new MEVDisplay(7, MEVType.NFT, "NFT", true, false, "mev-nft", 0),
            new MEVDisplay(8, MEVType.PunkBid, "Punk Bid", true, true, "mev-snipe", 0),
            new MEVDisplay(9, MEVType.PunkAcceptance, "Punk Accept", true, true, "mev-snipe", 0),
            new MEVDisplay(10, MEVType.PunkSnipe, "Punk Snipe", true, true, "mev-snipe", 0) };

        public static double[] MockProbs = new double[]
            {
                0,
                0.06,
                0,
                0,
                0,
                0.04,
                0.0075,
                0.02,
                0.01,
                0.005,
                0.005
            };

        public static MEVDisplay Get(MEVType mevType)
        {
            return Rows[(int)mevType];
        }

        public static string CssClass(MEVType mevType, OrderBy orderBy)
        {
            if (orderBy != OrderBy.Time || !Rows[(int)mevType].IsToxic)
                return Rows[(int)mevType].CssClass;
            else
                return Rows[0].CssClass;
        }
    }
}