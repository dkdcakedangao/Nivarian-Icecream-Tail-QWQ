namespace NivarianIcecreamTail
// 音效的开关控制器
{
    public static class IcecreamTailSkillAudio
    {
        public static bool Enabled
        {
            get
            {
                IcecreamTailSettings settings = IcecreamTailMod.Settings;
                return settings == null || settings.EnableEasterEggSkillSounds;
            }
        }
    }
}
