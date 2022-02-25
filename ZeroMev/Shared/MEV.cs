using System;
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

    public interface IMEV
    {
        int? TxIndex { get; } // preferred
        string? TxHash { get; } // suppied if TxIndex not available
        MEVType MEVType { get; }
        MEVClass MEVClass { get; }
        public decimal? MEVAmountUSD { get; }
        string? MEVSummary(MEVBlock2 mevBlock);
        string? MEVDetail(MEVBlock2 mevBlock);
        string? ActionSummary(MEVBlock2 mevBlock);
        string? ActionDetail(MEVBlock2 mevBlock);
    }

    public class MEVSwap
    {
        public MEVSwap(int symbolInIndex, int symbolOutIndex, ZMDecimal amountIn, ZMDecimal amountOut, ZMDecimal? outUSDRate)
        {
            SymbolInIndex = symbolInIndex;
            SymbolOutIndex = symbolOutIndex;
            AmountIn = amountIn;
            AmountOut = amountOut;
            if (outUSDRate.HasValue)
                AmountOutUSD = (amountOut * outUSDRate.Value).ToUSD(); // store the output usd because it's smaller than the BigDecimal rate
        }

        [JsonPropertyName("i")]
        int SymbolInIndex { get; set; }

        [JsonPropertyName("o")]
        int SymbolOutIndex { get; set; }

        [JsonPropertyName("a")]
        ZMDecimal AmountIn { get; set; }

        [JsonPropertyName("b")]
        ZMDecimal AmountOut { get; set; }

        [JsonPropertyName("u")]
        decimal? AmountOutUSD { get; set; }

        public ZMDecimal Rate
        {
            get
            {
                return AmountOut / AmountIn;
            }
        }

        public string ActionSummary(MEVBlock2 mevBlock)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(mevBlock.Symbols[SymbolInIndex].Name);
            sb.Append(" > ");
            sb.Append(mevBlock.Symbols[SymbolOutIndex].Name);
            return sb.ToString();
        }

        public string ActionDetail(MEVBlock2 mevBlock)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(mevBlock.Symbols[SymbolInIndex].Name);
            sb.Append(" ");
            sb.Append(AmountIn.Shorten());
            sb.Append(" ");
            sb.Append(" > ");
            sb.Append(mevBlock.Symbols[SymbolOutIndex].Name);
            sb.Append(" ");
            sb.Append(AmountOut.Shorten());
            sb.Append(" @");
            sb.Append(Rate.Shorten());
            sb.Append(" $");
            sb.Append(AmountOutUSD);
            return sb.ToString();
        }
    }

    public class Symbol
    {
        public const string UnknownImage = @"/un.png";

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
        public MEVBlock2(long blockNumber)
        {
            BlockNumber = blockNumber;
        }

        // persisted
        [JsonPropertyName("bn")]
        public long BlockNumber { get; private set; }
        [JsonPropertyName("sb")]
        public List<Symbol> Symbols { get; private set; } = new List<Symbol>();
        [JsonPropertyName("eu")]
        public decimal? ETHUSD { get; private set; }
        [JsonPropertyName("f")]
        public List<MEVFrontrun> Frontruns { get; set; } = new List<MEVFrontrun>();
        [JsonPropertyName("s")]
        public List<MEVSandwiched> Sandwiched { get; set; } = new List<MEVSandwiched>();
        [JsonPropertyName("b")]
        public List<MEVBackrun> Backruns { get; set; } = new List<MEVBackrun>();
        [JsonPropertyName("a")]
        public List<MEVArb> Arbs { get; set; } = new List<MEVArb>();

        // calculated
        [JsonIgnore]
        public MEVSummary[] MEVSummaries { get; private set; }
        [JsonIgnore]
        public int[] MEVClassCount { get; private set; }
        [JsonIgnore]
        public int[] MEVClassAmount { get; private set; }
    }

    public class MEVFrontrun : IMEV
    {
        public MEVFrontrun(int? txIndex, MEVSwap swap)
        {
            TxIndex = txIndex;
            Swap = swap;
        }

        [JsonPropertyName("s")]
        public MEVSwap Swap { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; private set; }

        [JsonIgnore]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Frontrun;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Toxic;

        [JsonIgnore]
        public decimal? MEVAmountUSD => null;

        public string MEVSummary(MEVBlock2 mevBlock) => throw new NotImplementedException();

        public string MEVDetail(MEVBlock2 mevBlock) => throw new NotImplementedException();

        public string ActionSummary(MEVBlock2 mevBlock)
        {
            return Swap.ActionSummary(mevBlock);
        }

        public string ActionDetail(MEVBlock2 mevBlock)
        {
            return Swap.ActionDetail(mevBlock);
        }
    }

    public class MEVSandwiched : IMEV
    {
        public MEVSandwiched(int? txIndex, MEVSwap swap)
        {
            TxIndex = txIndex;
            Swap = swap;
        }

        [JsonPropertyName("s")]
        public MEVSwap Swap { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; private set; }

        [JsonIgnore]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Sandwich;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Toxic;

        [JsonIgnore]
        public decimal? MEVAmountUSD => null;

        public string MEVSummary(MEVBlock2 mevBlock) => throw new NotImplementedException();

        public string MEVDetail(MEVBlock2 mevBlock) => throw new NotImplementedException();

        public string ActionSummary(MEVBlock2 mevBlock)
        {
            return Swap.ActionSummary(mevBlock);
        }

        public string ActionDetail(MEVBlock2 mevBlock)
        {
            return Swap.ActionDetail(mevBlock);
        }
    }

    public class MEVBackrun : IMEV
    {
        public MEVBackrun(int? txIndex, MEVSwap swap, decimal? mevAmountUSD)
        {
            TxIndex = txIndex;
            MEVAmountUSD = mevAmountUSD;
            Swap = swap;
        }

        [JsonPropertyName("s")]
        public MEVSwap Swap { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; private set; }

        [JsonPropertyName("h")]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Backrun;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Toxic;

        [JsonPropertyName("u")]
        public decimal? MEVAmountUSD { get; private set; }

        public string MEVSummary(MEVBlock2 mevBlock) => throw new NotImplementedException();

        public string MEVDetail(MEVBlock2 mevBlock) => throw new NotImplementedException();

        public string ActionSummary(MEVBlock2 mevBlock)
        {
            return Swap.ActionSummary(mevBlock);
        }

        public string ActionDetail(MEVBlock2 mevBlock)
        {
            return Swap.ActionDetail(mevBlock);
        }
    }

    public class MEVArb : IMEV
    {
        public MEVArb(int? txIndex, string txHash, MEVClass mevClass, decimal? mevAmount)
        {
            TxIndex = txIndex;
            TxHash = txHash;
            MEVClass = mevClass;
        }

        [JsonPropertyName("i")]
        public int? TxIndex { get; private set; }

        [JsonPropertyName("h")]
        public string TxHash { get; private set; }

        [JsonIgnore]
        public MEVType MEVType => MEVType.Arb;

        [JsonPropertyName("c")]
        public MEVClass MEVClass { get; private set; }

        [JsonPropertyName("u")]
        public decimal? MEVAmountUSD { get; private set; }

        public string MEVSummary(MEVBlock2 mevBlock) => throw new NotImplementedException();

        public string MEVDetail(MEVBlock2 mevBlock) => throw new NotImplementedException();

        public string ActionSummary(MEVBlock2 mevBlock) => throw new NotImplementedException();

        public string ActionDetail(MEVBlock2 mevBlock) => throw new NotImplementedException();
    }

    public class MEVNFT : IMEV
    {
        public MEVNFT(string txHash, decimal? mevAmount, int paymentSymbolIndex, string collectionAddress, string tokenId)
        {
            TxHash = txHash;
            MEVAmountUSD = mevAmount;
            PaymentSymbolIndex = paymentSymbolIndex;
            CollectionAddress = collectionAddress;
            TokenId = tokenId;
        }

        [JsonPropertyName("s")]
        int PaymentSymbolIndex { get; set; }
        [JsonPropertyName("c")]
        public string CollectionAddress { get; set; }
        [JsonPropertyName("t")]
        public string TokenId { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; private set; }

        [JsonPropertyName("h")]
        public string TxHash { get; private set; }

        [JsonIgnore]
        public MEVType MEVType => MEVType.NFT;

        [JsonPropertyName("c")]
        public MEVClass MEVClass => MEVClass.Info;

        [JsonPropertyName("u")]
        public decimal? MEVAmountUSD { get; private set; }

        public string MEVSummary(MEVBlock2 mevBlock) => throw new NotImplementedException();

        public string MEVDetail(MEVBlock2 mevBlock) => throw new NotImplementedException();

        public string ActionSummary(MEVBlock2 mevBlock) => throw new NotImplementedException();

        public string ActionDetail(MEVBlock2 mevBlock) => throw new NotImplementedException();
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