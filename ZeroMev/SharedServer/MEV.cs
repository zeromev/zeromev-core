using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZeroMev.Shared;

namespace ZeroMev.SharedServer
{
    public class MEVTypeSummary
    {
        public MEVType MEVType;
        public int Count;
        public decimal AmountUsd;
    }

    public class MEVTypeSummaries
    {
        public static List<MEVTypeSummary> FromMevBlock(MEVBlock mb)
        {
            MEVTypeSummary[] r = new MEVTypeSummary[Enum.GetValues(typeof(MEVType)).Length];

            for (int i = 0; i < mb.SwapsTx.Count; i++) SetMev(r, mb.SwapsTx[i], mb.SwapsTx[i].Swaps, MEVType.UserSwapVolume, mb, i);
            for (int i = 0; i < mb.Arbs.Count; i++) SetMev(r, mb.Arbs[i], mb.Arbs[i].Swaps, MEVType.ExtractorSwapVolume, mb, i);
            for (int i = 0; i < mb.Liquidations.Count; i++) SetMev(r, mb.Liquidations[i], null, null, mb, i);
            for (int i = 0; i < mb.NFTrades.Count; i++) SetMev(r, mb.NFTrades[i], null, null, mb, i);
            for (int i = 0; i < mb.Backruns.Count; i++) SetMev(r, mb.Backruns[i], mb.Backruns[i].Swaps, MEVType.ExtractorSwapVolume, mb, i);
            for (int i = 0; i < mb.Sandwiched.Count; i++)
                foreach (var s in mb.Sandwiched[i])
                    SetMev(r, s, s.Swaps, MEVType.UserSandwichedSwapVolume, mb, i);
            for (int i = 0; i < mb.Frontruns.Count; i++) SetMev(r, mb.Frontruns[i], mb.Frontruns[i].Swaps, MEVType.ExtractorSwapVolume, mb, i);

            return r.Where(x => x != null).ToList();
        }

        private static void SetMev(MEVTypeSummary[] r, IMEV mev, MEVSwaps swaps, MEVType? swapVolumeType, MEVBlock mb, int mevIndex)
        {
            if (mev == null) return;

            // calculate members
            mev.Cache(mb, mevIndex);

            // calculate swap volumes
            if (swaps != null && swapVolumeType != null)
            {
                // if we can't calculate the sandwich loss, don't count the volume
                if (swapVolumeType.Value != MEVType.UserSandwichedSwapVolume || mev.MEVAmountUsd.HasValue)
                {
                    foreach (var s in swaps.Swaps)
                    {
                        // ignore unknown symbols as we can't estimate mev against them
                        if (s.IsKnown)
                        {
                            var vi = (int)swapVolumeType.Value;
                            if (r[vi] == null)
                            {
                                r[vi] = new MEVTypeSummary();
                                r[vi].MEVType = swapVolumeType.Value;
                                r[vi].Count = 0;
                                r[vi].AmountUsd = 0;
                            }
                            r[vi].Count++;
                            r[vi].AmountUsd += s.AmountOutUsd ?? 0;
                        }
                    }
                }
            }

            if (mev.MEVClass == MEVClass.Info) return;

            int i = (int)mev.MEVType;
            if (r[i] == null)
            {
                r[i] = new MEVTypeSummary();
                r[i].MEVType = (MEVType)i;
                r[i].Count = 0;
                r[i].AmountUsd = 0;
            }
            r[i].Count++;
            if (mev.MEVType == MEVType.Backrun)
                r[i].AmountUsd += ((MEVBackrun)mev).BackrunAmountUsd ?? 0;
            else
                r[i].AmountUsd += mev.MEVAmountUsd ?? 0;
        }
    }
}