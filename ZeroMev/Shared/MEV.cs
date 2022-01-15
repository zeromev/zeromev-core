using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZeroMev.Shared
{
    public enum MEVType
    {
        None,
        Frontrun,
        Backrun,
        Arb,
        Swap,
        NFT,
        Liquidation,
        ToxicArb,
        FrontrunSwap,
        Sandwich,
        PunkSnipe,
        FrontrunLiquidation
    }

    public class MEVModel
    {
        public MEVSummary[] MEVSummaries;
        public int MEVCount;
        public int MEVOtherCount;
        public int MEVToxicCount;

        public MEVRow[] Rows;

        public void BuildMEVSummaries()
        {
            if (Rows == null)
                return;

            MEVSummaries = new MEVSummary[MEV.Rows.Length];
            MEVCount = 0;
            foreach (var row in Rows)
            {
                if (row.MEVType == MEVType.None) continue;
                int mi = (int)row.MEVType;
                if (MEV.Rows[mi].Parent != 0) continue;
                MEVSummaries[mi].Count++;
                if (row.MEVAmount.HasValue)
                    MEVSummaries[mi].Amount += row.MEVAmount.Value;

                if (MEV.Rows[mi].IsVisible)
                {
                    if (MEV.Rows[mi].IsToxic)
                        MEVToxicCount++;
                    else
                        MEVOtherCount++;
                    MEVCount++;
                }
            }
        }

        public void MockMEV(long blockNumber, int txCount)
        {
            // temporarily mock-up mev types to allow for front-end development
            if (txCount < 3) return;

            // use the block number as the seed to the mev is reproduced for each block
            Random r = new Random((int)blockNumber);
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
                        rows.Add(new MEVRow(i, (MEVType)k, MockAmount(r), comment));
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
        public MEVRow(int index, MEVType mEVType, decimal? mEVAmount, string mEVComment)
        {
            Index = index;
            MEVType = mEVType;
            MEVAmount = mEVAmount;
            MEVComment = mEVComment;
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
            new MEVDisplay(0, MEVType.None, "", false, false, "mev-inactive", 0),
            new MEVDisplay(1, MEVType.Frontrun, "Frontrun", true, true, "mev-fr", 9),
            new MEVDisplay(2, MEVType.Backrun, "Backrun", true, true, "mev-br", 9),
            new MEVDisplay(3, MEVType.Arb, "Arb", true, false, "mev-arb", 0),
            new MEVDisplay(4, MEVType.Swap, "Swap", true, false, "mev-swap", 0),
            new MEVDisplay(5, MEVType.NFT, "NFT", true, false, "mev-nft", 0),
            new MEVDisplay(6, MEVType.Liquidation, "Liquidation", true, false, "mev-liq", 0),
            new MEVDisplay(7, MEVType.ToxicArb, "Toxic Arb", true, true, "mev-bad-arb", 0),
            new MEVDisplay(8, MEVType.FrontrunSwap, "Frontrun Swap", true, true, "mev-fr-swap", 0),
            new MEVDisplay(9, MEVType.Sandwich, "Sandwich", true, true, "mev-sw", 0),
            new MEVDisplay(10, MEVType.PunkSnipe, "NFT Snipe", true, true, "mev-snipe", 0),
            new MEVDisplay(11, MEVType.FrontrunLiquidation, "Toxic Liquidation", true, true, "mev-fr-liq", 0)};

        public static double[] MockProbs = new double[]
            {
                0,
                0,
                0,
                0.05,
                0.01,
                0.005,
                0.02,
                0.0075,
                0.01,
                0,
                0.0075,
                0.005
            };

        public static MEVDisplay Get(MEVType mevType)
        {
            return Rows[(int)mevType];
        }

        public static string CssClass(MEVType mevType, OrderBy orderBy)
        {
            if (orderBy != OrderBy.Time)
                return Rows[(int)mevType].CssClass;
            else
                return Rows[0].CssClass;
        }
    }
}