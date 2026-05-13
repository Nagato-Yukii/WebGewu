using System.Text;

public sealed class ExperimentDirectorSkillResolver
{
    public bool TryResolveSkill(RobotConfig config, string rawSkillType, out SkillSlot slot, out SkillConfig skill)
    {
        slot = ResolveSkillSlot(config.species, NormalizeSkillType(rawSkillType));
        skill = ExtractSkillFromConfig(config, slot);
        return slot != SkillSlot.Unknown;
    }

    public SkillConfig ExtractSkillFromConfig(RobotConfig config, SkillSlot slot)
    {
        switch (slot)
        {
            case SkillSlot.BipedWalk: return config.bipedWalk;
            case SkillSlot.BipedRun: return config.bipedRun;
            case SkillSlot.BipedJump: return config.bipedJump;
            case SkillSlot.QuadTrot: return config.quadTrot;
            case SkillSlot.QuadBound: return config.quadBound;
            case SkillSlot.QuadPronk: return config.quadPronk;
            case SkillSlot.WheelDrive: return config.wheelDrive;
            case SkillSlot.WheelWalk: return config.wheelWalk;
            case SkillSlot.WheelJump: return config.wheelJump;
            default: return default;
        }
    }

    public SkillSlot ResolveSkillSlot(RobotSpecies species, string normalized)
    {
        switch (normalized)
        {
            case "bipedwalk": return SkillSlot.BipedWalk;
            case "bipedrun": return SkillSlot.BipedRun;
            case "bipedjump": return SkillSlot.BipedJump;
            case "quadtrot": return SkillSlot.QuadTrot;
            case "quadbound": return SkillSlot.QuadBound;
            case "quadpronk": return SkillSlot.QuadPronk;
            case "wheeldrive":
            case "legwheeleddrive": return SkillSlot.WheelDrive;
            case "wheelwalk":
            case "legwheeledwalk": return SkillSlot.WheelWalk;
            case "wheeljump":
            case "legwheeledjump": return SkillSlot.WheelJump;

            case "walk":
                if (species == RobotSpecies.Biped) return SkillSlot.BipedWalk;
                if (species == RobotSpecies.LegWheeled) return SkillSlot.WheelWalk;
                if (species == RobotSpecies.Quadruped) return SkillSlot.QuadTrot;
                return SkillSlot.Unknown;

            case "run":
                return species == RobotSpecies.Biped ? SkillSlot.BipedRun : SkillSlot.Unknown;

            case "jump":
                if (species == RobotSpecies.Biped) return SkillSlot.BipedJump;
                if (species == RobotSpecies.LegWheeled) return SkillSlot.WheelJump;
                return SkillSlot.Unknown;

            case "trot": return SkillSlot.QuadTrot;
            case "bound": return SkillSlot.QuadBound;
            case "pronk": return SkillSlot.QuadPronk;
            case "drive": return SkillSlot.WheelDrive;

            default: return SkillSlot.Unknown;
        }
    }

    public string NormalizeSkillType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        StringBuilder sb = new StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }
}
