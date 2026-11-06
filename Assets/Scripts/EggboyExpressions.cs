// 16 个表情的 id。顺序和网页版一致，Z/X 切换时按这个顺序走。
public enum ExprId
{
    happy, wink, surprised, curious, angry, cute, smug, sleepy,
    meh, sad, nervous, shy, thinking, puzzled, excited, scared,
}

public static class EggboyExpressions
{
    public static readonly ExprId[] All =
    {
        ExprId.happy, ExprId.wink, ExprId.surprised, ExprId.curious,
        ExprId.angry, ExprId.cute, ExprId.smug, ExprId.sleepy,
        ExprId.meh, ExprId.sad, ExprId.nervous, ExprId.shy,
        ExprId.thinking, ExprId.puzzled, ExprId.excited, ExprId.scared,
    };

    public static readonly string[] Names =
    {
        "开心", "眨眼", "惊讶", "好奇", "生气", "撒娇", "得意", "困倦",
        "无奈", "伤心", "紧张", "害羞", "思考", "疑惑", "兴奋", "害怕",
    };
}
