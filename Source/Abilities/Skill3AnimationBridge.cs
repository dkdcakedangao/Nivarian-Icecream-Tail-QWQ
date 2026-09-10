using Verse;

// 动画桥
// gpt老师做的
namespace NivarianIcecreamTail
{
    public enum Skill3AnimationStatus
    {
        Unavailable,
        Running,
        Completed,
        Interrupted
    }

    public interface ISkill3AnimationProvider
    {
        bool Prepare(Pawn caster, Pawn target);
        bool TryStart(Pawn caster, Pawn target);
        Skill3AnimationStatus GetStatus(Pawn caster, Pawn target);
        void Cancel(Pawn caster, Pawn target);
    }

    public static class Skill3AnimationBridge
    {
        public static ISkill3AnimationProvider Provider { get; private set; }

        public static void Register(ISkill3AnimationProvider provider)
        {
            Provider = provider;
        }

        public static bool IsPawnProtected(Pawn pawn)
        {
            return IcecreamTailSkill3Runtime.IsProtected(pawn);
        }

        internal static bool Prepare(Pawn caster, Pawn target)
        {
            try
            {
                return Provider == null || Provider.Prepare(caster, target);
            }
            catch (System.Exception exception)
            {
                int key = 1967300417 ^ (caster == null ? 0 : caster.thingIDNumber);
                Log.ErrorOnce("Nivarian Icecream Tail: Skill3 animation preparation failed: " + exception, key);
                return false;
            }
        }

        internal static bool TryStart(Pawn caster, Pawn target)
        {
            try
            {
                return Provider != null && Provider.TryStart(caster, target);
            }
            catch
            {
                return false;
            }
        }

        internal static Skill3AnimationStatus GetStatus(Pawn caster, Pawn target)
        {
            try
            {
                return Provider == null
                    ? Skill3AnimationStatus.Interrupted
                    : Provider.GetStatus(caster, target);
            }
            catch
            {
                return Skill3AnimationStatus.Interrupted;
            }
        }

        internal static void Cancel(Pawn caster, Pawn target)
        {
            try
            {
                if (Provider != null) Provider.Cancel(caster, target);
            }
            catch
            {
            }
        }
    }
}
