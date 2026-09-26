using System.Collections.Generic;

namespace HoverForHire
{
    /// <summary>
    /// The jobs offered between deliveries: up to three distinct available contracts. The first starts at the pad the
    /// aircraft is on when any does, so a new job never begins with an empty repositioning flight.
    /// </summary>
    public static class OfferBoard
    {
        public const int Size = 3;

        /// <summary>Deal offers from <paramref name="available"/>; the same seed always deals the same offers.</summary>
        public static List<ContractDefinition> Deal(IEnumerable<ContractDefinition> available, string padId, int seed)
        {
            var random = new System.Random(seed);
            var pool = new List<ContractDefinition>(available);
            var offers = new List<ContractDefinition>(Size);
            var local = pool.FindAll(contract => contract.Pickup.Id == padId);
            if (local.Count > 0) offers.Add(local[random.Next(local.Count)]);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            foreach (ContractDefinition contract in pool)
            {
                if (offers.Count >= Size) break;
                if (!offers.Contains(contract)) offers.Add(contract);
            }
            return offers;
        }
    }
}
