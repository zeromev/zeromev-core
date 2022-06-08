﻿using System;
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

            for (int i = 0; i < mb.SwapsTx.Count; i++) SetMev(r, mb.SwapsTx[i], mb, i);
            for (int i = 0; i < mb.Arbs.Count; i++) SetMev(r, mb.Arbs[i], mb, i);
            for (int i = 0; i < mb.Liquidations.Count; i++) SetMev(r, mb.Liquidations[i], mb, i);
            for (int i = 0; i < mb.NFTrades.Count; i++) SetMev(r, mb.NFTrades[i], mb, i);
            for (int i = 0; i < mb.Frontruns.Count; i++) SetMev(r, mb.Frontruns[i], mb, i);
            for (int i = 0; i < mb.Sandwiched.Count; i++)
                foreach (var s in mb.Sandwiched[i])
                    SetMev(r, s, mb, i);
            for (int i = 0; i < mb.Backruns.Count; i++) SetMev(r, mb.Backruns[i], mb, i);

            return r.Where(x => x != null).ToList();
        }

        private static void SetMev(MEVTypeSummary[] r, IMEV mev, MEVBlock mb, int mevIndex)
        {
            if (mev == null || mev.MEVClass == MEVClass.Info) return;

            // calculate members
            mev.Cache(mb, mevIndex);

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