﻿using System;
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
        Swaps,
        Frontrun,
        Sandwich,
        Backrun,
        Arb,
        Liquid,
        NFT,
        PunkBid,
        PunkAccept,
        PunkSnipe
    }

    public enum MEVClass
    {
        All,
        Unclassified,
        Positive,
        Neutral,
        Toxic,
        Info
    }

    public enum MEVFilter
    {
        All,
        Info,
        Toxic,
        Other
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
        void Cache(MEVBlock mevBlock, int mevIndex); // mevIndex is allows mev instances to find other related instances, eg: backrun can cheaply find related sandwiched and frontun txs without duplicating them
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

    public class MEVLiteCache
    {
        public const int CachedMevBlockCount = 300;

        [JsonPropertyName("r")]
        public List<MEVLiteBlock> Blocks = new List<MEVLiteBlock>();

        [JsonIgnore]
        public MEVSummary[] Totals = new MEVSummary[Enum.GetValues(typeof(MEVFilter)).Length];

        [JsonConstructor]
        public MEVLiteCache()
        {
        }

        public void CalculateSummaries()
        {
            foreach (var b in Blocks)
                b.CalculateSummaries(Totals);
        }

        public string Duration()
        {
            if (Blocks == null || Blocks.Count == 0) return "0 secs";
            if (Blocks[0].BlockTime == null || Blocks[1].BlockTime == null) return "?";
            TimeSpan ts = Blocks[0].BlockTime.Value - Blocks[Blocks.Count - 1].BlockTime.Value;
            return ZMTx.DurationStr(ts);
        }

        public string BlockCount()
        {
            if (Blocks == null) return "0";
            return Blocks.Count.ToString();
        }
    }

    public class MEVLiteBlock
    {
        [JsonConstructor]
        public MEVLiteBlock()
        {
        }

        public MEVLiteBlock(long blockNumber, DateTime? blockTime)
        {
            BlockNumber = blockNumber;
            BlockTime = blockTime;
        }

        [JsonPropertyName("b")]
        public long BlockNumber { get; set; }

        [JsonPropertyName("t")]
        public DateTime? BlockTime { get; set; }

        [JsonPropertyName("m")]
        public List<MEVLite> MEVLite { get; set; }

        [JsonIgnore]
        public MEVSummary[] MEVSummaries;

        public void CalculateSummaries(MEVSummary[] totals)
        {
            MEVSummaries = new MEVSummary[Enum.GetValues(typeof(MEVFilter)).Length];
            foreach (var m in MEVLite)
            {
                MEVWeb.UpdateSummaries((IMEV)m, MEVSummaries);
                MEVWeb.UpdateSummaries((IMEV)m, totals);
            }
        }
    }

    public class MEVLite : IMEV
    {
        public MEVLite(IMEV mev)
        {
            MEVType = mev.MEVType;
            MEVClass = mev.MEVClass;
            MEVAmountUsd = mev.MEVAmountUsd;
        }

        [JsonConstructor]
        public MEVLite()
        {
        }

        [JsonIgnore]
        public int? TxIndex => null;

        [JsonIgnore]
        public string TxHash => null;

        [JsonPropertyName("m")]
        public MEVType MEVType { get; set; } = MEVType.None;

        [JsonPropertyName("c")]
        public MEVClass MEVClass { get; set; } = MEVClass.All;

        [JsonPropertyName("u")]
        public decimal? MEVAmountUsd { get; set; } = null;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = null;

        [JsonIgnore]
        public string? ActionSummary { get; set; } = null;

        [JsonIgnore]
        public string? ActionDetail { get; set; } = null;

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
        }
    }

    public class MEVBlock
    {
        public MEVBlock()
        {
        }

        public MEVBlock(long blockNumber)
        {
            BlockNumber = blockNumber;
        }

        // persisted
        [JsonPropertyName("bn")]
        public long BlockNumber { get; set; }

        [JsonPropertyName("ts")]
        public DateTime? BlockTime { get; set; }

        [JsonPropertyName("sb")]
        public List<Symbol> Symbols { get; set; } = new List<Symbol>();

        [JsonPropertyName("eu")]
        public decimal? EthUsd { get; set; }

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
        [JsonIgnore]
        public bool[] ExistingMEV { get; set; }

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

        public string? GetImage(int symbolIndex)
        {
            if (symbolIndex < 0)
            {
                if (symbolIndex == Symbol.EthSymbolIndex)
                    return Symbol.EthSymbol.GetImageUrl();
                return Symbol.UnknownImage;
            }
            return Symbols[symbolIndex].GetImageUrl();
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
        public string? MEVDetail { get; set; } = null;

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public ZMDecimal? InUsdRate()
        {
            if (AmountInUsd == null || AmountIn == 0) return null;
            return (ZMDecimal)AmountInUsd / AmountIn;
        }

        public ZMDecimal? OutUsdRate()
        {
            if (AmountOutUsd == null || AmountOut == 0) return null;
            return (ZMDecimal)AmountOutUsd / AmountOut;
        }

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            StringBuilder sb = new StringBuilder();
            BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
        }

        public void BuildActionSummary(MEVBlock mevBlock, StringBuilder sb)
        {
            sb.Append("<img src=\"");
            sb.Append(mevBlock.GetImage(SymbolInIndex));
            sb.Append("\" width=\"20\" height=\"20\"><img src=\"swap.svg\" width=\"20\" height=\"20\"><img src=\"");
            sb.Append(mevBlock.GetImage(SymbolOutIndex));
            sb.Append("\" width=\"20\" height=\"20\">");
        }

        public void BuildActionDetail(MEVBlock mevBlock, StringBuilder sb)
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
        public MEVType MEVType => MEVType.Swaps;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Info;

        [JsonIgnore]
        public decimal? MEVAmountUsd { get; set; } = null;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = null;

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            StringBuilder sb = new StringBuilder();
            BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
        }

        public string BuildActionSummary(MEVBlock mevBlock, StringBuilder sb)
        {
            if (Swaps == null || Swaps.Count == 0) return "no swaps";
            Swaps[0].BuildActionSummary(mevBlock, sb);
            if (Swaps.Count > 1)
            {
                sb.Append(" +");
                sb.Append(Swaps.Count - 1);
            }
            sb.AppendLine();
            return sb.ToString();
        }

        public string BuildActionDetail(MEVBlock mevBlock, StringBuilder sb)
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

        public void Cache(MEVBlock mevBlock, int mevIndex)
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

        public void Cache(MEVBlock mevBlock, int mevIndex)
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

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            var frontrun = mevBlock.Frontruns[mevIndex].Swap;
            var backrun = Swap;

            // see https://github.com/flashbots/mev-inspect-py/issues/283
            if (frontrun != null && backrun != null && frontrun.AmountInUsd != null && frontrun.AmountOutUsd != null && backrun.AmountInUsd != null && backrun.AmountOutUsd != null && backrun.AmountInUsd.Value != 0 && frontrun.AmountOutUsd != 0)
            {
                //MEVAmountUsd = frontrun.AmountInUsd - Swap.AmountOutUsd;
                //MEVDetail = $"backrun (in) ${frontrun.AmountInUsd} - frontrun (out) ${Swap.AmountOutUsd} = victim impact ${frontrun.AmountInUsd - Swap.AmountOutUsd}";

                decimal frontIn = frontrun.AmountInUsd.Value;
                decimal frontOut = frontrun.AmountOutUsd.Value;
                decimal backIn = backrun.AmountInUsd.Value;
                decimal backOut = backrun.AmountOutUsd.Value;

                decimal profitFrontrun = (frontOut * (backOut / backIn) - frontIn);
                decimal profitBackrun = (backOut - (backIn * (frontIn / frontOut)));

                decimal sandwichProfit;
                if (Math.Abs(profitFrontrun) < Math.Abs(profitBackrun))
                    sandwichProfit = profitFrontrun;
                else
                    sandwichProfit = profitBackrun;

                MEVAmountUsd = -sandwichProfit;
                MEVDetail = $"victim impact ${MEVAmountUsd}";
            }
            else
            {
                MEVAmountUsd = null;
                MEVDetail = "missing swap data, can't calculate";
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

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            StringBuilder sb = new StringBuilder();

            if (MEVAmountUsd == null)
            {
                sb.AppendLine("arb missing exchange rates, can't calculate.");
            }
            else
            {
                if (Swaps == null || Swaps.Count == 0)
                {
                    sb.AppendLine("no arb swaps.");
                }
                else
                {
                    sb.Append(Swaps.Count);
                    sb.AppendLine(" swaps in arb.");
                }
                sb.Append("arb victim impact $");
                sb.Append(MEVAmountUsd);
                sb.AppendLine(".");
            }
            MEVDetail = sb.ToString();

            sb.Clear();
            BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
        }

        public string BuildActionSummary(MEVBlock mevBlock, StringBuilder sb)
        {
            if (Swaps == null || Swaps.Count == 0) return "no swaps.";
            Swaps[0].BuildActionSummary(mevBlock, sb);
            if (Swaps.Count > 1)
            {
                sb.Append(" +");
                sb.Append(Swaps.Count - 1);
            }
            sb.AppendLine();
            return sb.ToString();
        }

        public string BuildActionDetail(MEVBlock mevBlock, StringBuilder sb)
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
        public MEVType MEVType => MEVType.Liquid;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Unclassified;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = null;

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            StringBuilder sb = new StringBuilder();
            BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
        }

        public string BuildActionSummary(MEVBlock mevBlock, StringBuilder sb)
        {
            sb.Append("<img src=\"");
            sb.Append(mevBlock.GetImage(DebtSymbolIndex));
            sb.Append("\" width=\"20\" height=\"20\"><img src=\"liq.svg\" width=\"20\" height=\"20\"><img src=\"");
            sb.Append(mevBlock.GetImage(ReceivedSymbolIndex));
            sb.Append("\" width=\"20\" height=\"20\">");
            return sb.ToString();
        }

        public string BuildActionDetail(MEVBlock mevBlock, StringBuilder sb)
        {
            // aave protocol liquidation.
            // debt purchase amount symbolA $143.11 (2784324.33).
            // received amount usd symbolB $54.17 (243432.11).
            sb.Append(Protocol.ToString());
            sb.AppendLine(" protocol liquidation.");
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
        public string? MEVDetail { get; set; } = null;

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public void Cache(MEVBlock mevBlock, int mevIndex)
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
            sb.Append("<a href=\"");
            sb.Append(nftLink);
            sb.Append("\" target=\"_blank\"><img src=\"\\nft.png\" alt=\"nft\" width=\"20\" height=\"20\"><img src=\"\\link.svg\" alt=\"nft link\" width=\"20\" height=\"20\"></a>");
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

    public struct MEVSummary
    {
        public MEVFilter MEVFilter;
        public decimal Count;
        public decimal AmountUsd;
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
        public readonly MEVClass Class;
        public readonly string Name;
        public readonly bool IsVisible;
        public readonly string CssClass;

        public MEVDisplay(MEVClass mevClass, string name, bool isVisible, string cssClass)
        {
            Index = (int)mevClass;
            Class = mevClass;
            Name = name;
            IsVisible = isVisible;
            CssClass = cssClass;
        }

        public bool DoDisplay
        {
            get
            {
                return IsVisible;
            }
        }
    }

    public class MEVWeb
    {
        public static MEVDisplay[] Rows = {
            new MEVDisplay(MEVClass.All, "", false, "mev-any"),
            new MEVDisplay(MEVClass.Unclassified, "Unclassified", true, "mev-un"),
            new MEVDisplay(MEVClass.Positive, "Positive", true, "mev-pos"),
            new MEVDisplay(MEVClass.Neutral, "Neutral", true, "mev-neu"),
            new MEVDisplay(MEVClass.Toxic, "Toxic", true, "mev-tox"),
            new MEVDisplay(MEVClass.Info, "Info", true, "mev-inf")};

        public static MEVDisplay Get(MEVClass mevClass)
        {
            return Rows[(int)mevClass];
        }

        public static string CssClass(MEVClass mevClass)
        {
            return Rows[(int)mevClass].CssClass;
        }

        public static MEVFilter GetMEVFilter(MEVClass mevClass)
        {
            switch (mevClass)
            {
                case MEVClass.Toxic:
                    return MEVFilter.Toxic;

                case MEVClass.Unclassified:
                case MEVClass.Positive:
                case MEVClass.Neutral:
                    return MEVFilter.Other;

                default:
                    return MEVFilter.Info;
            }
        }

        public static void UpdateSummaries(IMEV mev, MEVSummary[] mevSummaries)
        {
            var mevFilter = MEVWeb.GetMEVFilter(mev.MEVClass);
            mevSummaries[(int)MEVFilter.All].Count++;
            mevSummaries[(int)MEVFilter.All].AmountUsd += mev.MEVAmountUsd ?? 0;
            if (mevFilter == MEVFilter.Toxic || mevFilter == MEVFilter.Other)
            {
                mevSummaries[(int)mevFilter].Count++;
                mevSummaries[(int)mevFilter].AmountUsd += mev.MEVAmountUsd ?? 0;
            }
        }
    }

    public static class MEVCalc
    {
        public static void PoolFromSwapsAB(ZMDecimal[] a, ZMDecimal[] b, ZMDecimal c, out ZMDecimal x, out ZMDecimal y, out ZMDecimal k)
        {
            if (a.Length < 3 || b.Length < 3) throw new ArgumentException("min 3 data points.");
            PoolFromSwapsAB(a[0], a[1], a[2], b[0], b[1], b[2], c, out x, out y, out k);
        }

        public static void PoolFromSwapsBA(ZMDecimal[] a, ZMDecimal[] b, ZMDecimal c, out ZMDecimal x, out ZMDecimal y, out ZMDecimal k)
        {
            if (a.Length < 3 || b.Length < 3) throw new ArgumentException("min 3 data points.");
            PoolFromSwapsBA(a[0], a[1], a[2], b[0], b[1], b[2], c, out x, out y, out k);
        }

        public static void PoolFromSwapsAB(ZMDecimal a1, ZMDecimal a2, ZMDecimal a3, ZMDecimal b1, ZMDecimal b2, ZMDecimal b3, ZMDecimal c, out ZMDecimal x, out ZMDecimal y, out ZMDecimal k)
        {
            ZMDecimal a2pow2 = a2.Pow(2);
            ZMDecimal a3pow2 = a3.Pow(2);
            ZMDecimal b2pow2 = b2.Pow(2);
            ZMDecimal b3pow2 = b3.Pow(2);

            ZMDecimal a3b2 = a3 * b2;
            ZMDecimal a2b3 = a2 * b3;
            ZMDecimal a3b3 = a3 * b3;
            ZMDecimal a3b2_a2b3 = a3b2 - a2b3;

            x = -((a1 * a3b2_a2b3) - (a2 * a3b3) - (a2pow2 * b3)) / a3b2_a2b3;
            y = c * ((b1 * (a3b2 - a2b3)) + (a3b2 * b3) + (a3 * b2pow2)) / a3b2_a2b3;
            k = c * ((b2 * ((a2 * a3pow2 * b3pow2) + (a2pow2 * a3 * b3pow2))) + (b2pow2 * ((a2 * a3pow2 * b3) + (a2pow2 * a3b3)))) / ((a2pow2 * b3pow2) - (2 * a2 * a3b2 * b3) + (a3pow2 * b2pow2));
        }

        public static void PoolFromSwapsBA(ZMDecimal a1, ZMDecimal a2, ZMDecimal a3, ZMDecimal b1, ZMDecimal b2, ZMDecimal b3, ZMDecimal c, out ZMDecimal x, out ZMDecimal y, out ZMDecimal k)
        {
            ZMDecimal a2pow2 = a2.Pow(2);
            ZMDecimal a3pow2 = a3.Pow(2);
            ZMDecimal b2pow2 = b2.Pow(2);
            ZMDecimal b3pow2 = b3.Pow(2);

            ZMDecimal a3b2 = a3 * b2;
            ZMDecimal a2b3 = a2 * b3;
            ZMDecimal a3b3 = a3 * b3;

            x = ((((a1 * a3b2) - (a1 * a2b3)) - (a2 * a3b3) - (a2pow2 * b3)) * c) / (a3b2 - a2b3);
            y = -((b1 * (a3b2 - a2b3)) + (a3b2 * b3) + (a3 * b2pow2)) / (a3b2 - a2b3);
            k = (((b2 * ((a2 * a3pow2 * b3pow2) + (a2pow2 * a3 * b3pow2))) + (b2pow2 * ((a2 * a3pow2 * b3) + (a2pow2 * a3b3)))) * c) / ((a2pow2 * b3pow2) - (2 * a2 * a3b2 * b3) + (a3pow2 * b2pow2));
        }

        public static ZMDecimal[] KFromSwapsAndPool(ZMDecimal a1, ZMDecimal a2, ZMDecimal a3, ZMDecimal b1, ZMDecimal b2, ZMDecimal b3, ZMDecimal c, ZMDecimal x, ZMDecimal y)
        {
            var k = new ZMDecimal[3];
            k[0] = (x + a1) * (y - b1 * c);
            k[1] = (x + a1 + a2) * (y - b1 * c - b2 * c);
            k[2] = (x + a1 + a2 + a3) * (y - b1 * c - b2 * c - b3 * c);
            return k;
        }

        public static ZMDecimal SwapAB(ZMDecimal a, ZMDecimal prev_b, ZMDecimal x, ZMDecimal y, ZMDecimal k, ZMDecimal c, out ZMDecimal xOut, out ZMDecimal yOut)
        {
            /*
            Xout2=Xout+a2
            Yout2=Yout-b1c 
            b2= ((Xout2*Yout2)-k)/(c*Xout2)
             */

            ZMDecimal b;
            xOut = x + a;
            yOut = y - (prev_b * c);
            return b = ((xOut * yOut) - k) / (c * xOut);
        }

        public static ZMDecimal SwapBA(ZMDecimal b, ZMDecimal prev_a, ZMDecimal x, ZMDecimal y, ZMDecimal k, ZMDecimal c, out ZMDecimal xOut, out ZMDecimal yOut)
        {
            /*
            Xout2=Xout-a1c
            Yout2=Yout+b2
            a2= ((Xout2*Yout2)-k)/(c*Yout2)
            */

            ZMDecimal a;
            xOut = x - (prev_a * c);
            yOut = y + b;
            a = ((xOut * yOut) - k) / (c * yOut);
            return a;
        }
    }
}