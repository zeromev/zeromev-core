using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace ZeroMev.Shared
{
    public class TraceAddress : IComparable<TraceAddress>
    {
        [JsonPropertyName("t")]
        public int[] Values { get; set; }

        public TraceAddress()
        {
        }

        public TraceAddress(int[] values)
        {
            Values = values;
        }

        public bool IsEqualTo(TraceAddress other)
        {
            if (Values == null)
            {
                if (other.Values == null)
                    return true; // this is the reason for not using the Equals override
                return false;
            }

            if (other.Values == null)
                return false;

            if (Values.Length != other.Values.Length)
                return false;

            for (int i = 0; i < Values.Length; i++)
                if (Values[i] != other.Values[i])
                    return false;

            return true;
        }

        public int CompareTo(TraceAddress other)
        {
            // handle null and empty trace address values
            bool noTrace = (this.Values == null || this.Values.Length == 0);
            bool noOtherTrace = (other.Values == null || other.Values.Length == 0);
            if (noTrace && noOtherTrace) return 0;
            if (!noTrace && noOtherTrace) return 1;
            if (noTrace && !noOtherTrace) return -1;

            // compare trace address arrays directly now we know they both exist
            for (int i = 0; i < this.Values.Length; i++)
            {
                if (other.Values.Length <= i) break;
                int r = this.Values[i].CompareTo(other.Values[i]);
                if (r != 0)
                    return r;
            }

            // if the are both equivalent as far as they go, the shorter one takes priority
            return this.Values.Length.CompareTo(other.Values.Length);
        }
    }
}