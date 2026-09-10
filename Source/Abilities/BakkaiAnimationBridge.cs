using Verse;

// 爆回的桥
namespace NivarianIcecreamTail
{
    public interface IBakkaiAnimationProvider
    {
        void Prepare(Pawn pawn);
        void Cancel(Pawn pawn);
    }

    public static class BakkaiAnimationBridge
    {
        public static IBakkaiAnimationProvider Provider { get; private set; }

        public static void Register(IBakkaiAnimationProvider provider)
        {
            Provider = provider;
        }

        public static bool IsPawnActive(Pawn pawn)
        {
            return IcecreamTailBakkaiRuntime.IsActive(pawn);
        }

        internal static void Prepare(Pawn pawn)
        {
            try
            {
                if (Provider != null) Provider.Prepare(pawn);
            }
            catch (System.Exception exception)
            {
                int key = 1579261219 ^ (pawn == null ? 0 : pawn.thingIDNumber);
                Log.ErrorOnce("Nivarian Icecream Tail: Bakkai animation preparation failed: " + exception, key);
            }
        }

        internal static void Cancel(Pawn pawn)
        {
            try
            {
                if (Provider != null) Provider.Cancel(pawn);
            }
            catch
            {
            }
        }
    }
}
