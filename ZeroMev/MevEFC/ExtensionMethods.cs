using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ZeroMev.MevEFC
{
    public static class ExtensionMethods
    {
        public static long GetLastProcessedMevInspectBlock(this zeromevContext db)
        {
            return (long)db.LatestBlockUpdates.Max(x => x.BlockNumber);
        }

        public static long GetLastProcessedBlock(this zeromevContext db)
        {
            return (long)db.ZmLatestBlockUpdates.Max(x => x.BlockNumber);
        }

        public static async Task SetLastProcessedBlock(this zeromevContext db, long blockNumber)
        {
            // remove any previous rows
            var lastProcessed = await db.ZmLatestBlockUpdates.ToListAsync();
            if (lastProcessed != null)
                foreach (var update in lastProcessed)
                    db.Remove(update);

            // add the new row
            ZmLatestBlockUpdate lastUpdate = new ZmLatestBlockUpdate()
            {
                BlockNumber = (decimal)blockNumber,
                UpdatedAt = DateTime.Now
            };
            db.ZmLatestBlockUpdates.Add(lastUpdate);
            db.SaveChanges();
        }
    }
}