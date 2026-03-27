using System.Collections.Generic;

namespace TAOM.Features.AdvancedCombat;

public static class HumanAnimationConstants
{
    public static readonly List<string> FrontFallAnimations;
    public static readonly List<string> BackFallAnimations;
    public static readonly List<string> LeftFallAnimations;
    public static readonly List<string> RightFallAnimations;

    public static readonly List<string> FrontFlinchAnimations;
    public static readonly List<string> BackFlinchAnimations;
    public static readonly List<string> LeftFlinchAnimations;
    public static readonly List<string> RightFlinchAnimations;

    static HumanAnimationConstants()
    {
        string backProjection1 = "act_strike_knock_back_abdomen_front";
        string backProjection2 = "act_strike_fall_back_back_rise";
        string backProjection3 = "act_strike_fall_back_back_rise_left_stance";
        string backProjection4 = "act_strike_fall_back_heavy_back_rise";

        string frontProjection1 = "act_strike_knock_back_abdomen_back";
        string frontProjection2 = "act_strike_fall_front_back_rise";
        string frontProjection3 = "act_strike_fall_front_back_rise_left_stance";
        string frontProjection4 = "act_strike_fall_front_heavy_back_rise";

        string leftProjection1 = "act_strike_knock_back_abdomen_left";
        string leftProjection2 = "act_strike_fall_left_back_rise_alt";
        string leftProjection3 = "act_strike_fall_left_back_rise_left_stance_alt";
        string leftProjection4 = "act_strike_fall_left_back_rise";
        string leftProjection5 = "act_strike_fall_left_back_rise_left_stance";
        string leftProjection6 = "act_strike_fall_left_heavy_back_rise";
        string leftProjection7 = "act_strike_fall_left_heavy_back_rise_left_stance";

        string rightProjection1 = "act_strike_knock_back_abdomen_right";
        string rightProjection2 = "act_strike_fall_right_back_rise_alt";
        string rightProjection3 = "act_strike_fall_right_back_rise_left_stance_alt";
        string rightProjection4 = "act_strike_fall_right_back_rise";
        string rightProjection5 = "act_strike_fall_right_back_rise_left_stance";
        string rightProjection6 = "act_strike_fall_right_heavy_back_rise";
        string rightProjection7 = "act_strike_fall_right_heavy_back_rise_left_stance";

        FrontFlinchAnimations = new() { frontProjection1 };
        BackFlinchAnimations = new() { backProjection1 };
        LeftFlinchAnimations = new() { leftProjection1 };
        RightFlinchAnimations = new() { rightProjection1 };

        FrontFallAnimations = new() { frontProjection2, frontProjection3, frontProjection4 };
        BackFallAnimations = new() { backProjection2, backProjection3, backProjection4 };
        LeftFallAnimations = new() { leftProjection2, leftProjection3, leftProjection4, leftProjection5, leftProjection6, leftProjection7 };
        RightFallAnimations = new() { rightProjection2, rightProjection3, rightProjection4, rightProjection5, rightProjection6, rightProjection7 };
    }
}
