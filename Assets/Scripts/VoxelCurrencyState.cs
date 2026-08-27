using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Runtime currency wallet ready for repair costs and future race rewards.</summary>
    public static class VoxelCurrencyState
    {
        public static int Balance { get; private set; }

        public static bool TrySpend(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (amount > Balance)
                return false;
            Balance -= amount;
            return true;
        }

        public static void Add(int amount) => Balance = Mathf.Max(0, Balance + amount);

        public static void Reset() => Balance = 0;
    }
}
