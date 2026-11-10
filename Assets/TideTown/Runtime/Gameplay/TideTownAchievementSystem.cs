// 潮汐镇「一题多解」成就收集与“嘀嘟”弹窗系统（TideTownAchievementSystem）。
// 鼓励玩家在同一关卡尝试不同解法（如：吹气 vs 擦拭、摇绞盘 vs 造船渡海、找红球 vs 造美食/造玩具投喂小黑、摄像头挥手 vs 信号闪灯等），
// 每达成一种独特解法即伴随清脆“嘀—嘟！”双音音效弹出成就徽章，并记入邮差成就图鉴。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TideTownAchievementSystem : MonoBehaviour
{
    public sealed class AchDef
    {
        public string id;
        public string title;
        public string desc;
        public string category;
    }

    public static TideTownAchievementSystem Instance { get; private set; }

    public static readonly AchDef[] AllAchievements =
    {
        // 旧物盐霜与暗扣（3种解法）
        new AchDef { id = "breath_dust",   category = "旧物破译", title = "微风之息 · 吹散岁月盐霜", desc = "对着麦克风吹气，吹散旧物背面的海盐灰尘露出黄铜暗扣" },
        new AchDef { id = "tactile_pins",  category = "旧物破译", title = "巧手匠人 · 毫厘暗扣拆解", desc = "亲手拂去盐霜并按正确咬合顺序点开全部三枚立体黄铜暗扣" },
        new AchDef { id = "desk_shortcut", category = "旧物破译", title = "密室侦探 · 课桌下的捷径", desc = "发现教室窗边阿萤课桌背面刻着的总密码，免检视直破木匣" },

        // 面包房生火与防风（3种解法）
        new AchDef { id = "luban_oven",    category = "面包房站", title = "鲁班传人 · 三环风门归位", desc = "自己拨开三根木栓、对准三环刻度，打开烤炉的风门（不用鲁班巧思）" },
        new AchDef { id = "breath_fire",   category = "面包房站", title = "人间烟火 · 吹旺阿婆的炉膛", desc = "对着烤炉火苗吹气鼓风，让面包房烟囱升起滚滚暖烟" },
        new AchDef { id = "word_fire",     category = "面包房站", title = "言灵造物 · 心愿化作火种", desc = "撕下信上的「木柴」是湿的；拼出「干木柴」或许愿造柴，让炉膛里有柴可烧" },

        new AchDef { id = "word_combo",    category = "面包房站", title = "咬文嚼字 · 两个字拼出新的字", desc = "把信上撕下来的两个字叠在一起，拼出一个新的字" },

        // 学校哄开心小黑（3种解法）
        new AchDef { id = "classic_ball",  category = "学校后院", title = "旧日约定 · 秋千下的红球", desc = "寻回秋千下的旧红球并解开爪印木匣放进小黑的食盆" },
        new AchDef { id = "dog_detective", category = "灯塔与海岛", title = "小黑侦探 · 鼻子比眼睛灵", desc = "让小黑嗅出三样旧物的刻度，或者踩着浅滩的爪印踏板捞起老陈的木匣" },
        new AchDef { id = "chef_dog",      category = "学校后院", title = "私厨投喂 · 小黑的定制大餐", desc = "按 V 打开心愿造物笺，在狗碗旁给小黑造一样它爱吃的" },
        new AchDef { id = "toy_dog",       category = "学校后院", title = "玩伴心意 · 造一件新玩具", desc = "按 V 打开心愿造物笺，在狗碗旁给小黑造一件新玩具" },

        // 涨潮渡口过海（3种解法）
        new AchDef { id = "winch_bridge",  category = "涨潮渡口", title = "老船坞工 · 齿轮绞盘升桥", desc = "一个人摇不动岸边的绞盘——和十秒前的自己一起按住它，升起沉在水下的旧栈桥" },
        new AchDef { id = "wish_boat",     category = "涨潮渡口", title = "凭空造舟 · 乘心愿渡海", desc = "在涨潮的渡口按 V 许一条船，踩着它过海" },
        new AchDef { id = "echo_coop",     category = "涨潮渡口", title = "时空旅人 · 与过去的自己接力", desc = "按 R 唤出十秒前的自己，和它一起做成一件一个人做不到的事" },

        new AchDef { id = "tailwind",      category = "涨潮渡口", title = "乘风而行 · 顶住了一阵海风", desc = "在海边迎风的路上蹲低扛过一阵大风，顺风跑起来" },
        new AchDef { id = "print_bridge",  category = "涨潮渡口", title = "以小见大 · 一把尺子搭成桥", desc = "举起取景框，把手里的小物件按透视拍成一座横跨渡口缺口的桥" },
        new AchDef { id = "wood_boat",     category = "涨潮渡口", title = "纸上行舟 · 拼一条木船过海", desc = "用信上的字拼出「木船」，扔到涨潮的渡口缺口上踩着过海" },

        // 灯塔呼应与全岛探索（3种解法）
        new AchDef { id = "cam_wave",      category = "灯塔与海岛", title = "隔空挥别 · 山那边的微光", desc = "在灯塔顶层对着摄像头挥手，唤醒对岸山头的 40 盏手电筒回应" },
        new AchDef { id = "wish_town",     category = "灯塔与海岛", title = "筑梦邮差 · 焕然一新的潮汐镇", desc = "在心愿展台上许一件纪念物，它会一直摆到结局" },
        new AchDef { id = "shadow_key",    category = "灯塔与海岛", title = "影子里的钥匙 · 老陈的影子戏", desc = "转动船棚边的杂物架，让布幕上的影子拼成一把钥匙" },
        new AchDef { id = "music_box",     category = "灯塔与海岛", title = "小镇八音盒 · 把声音带回广场", desc = "用空瓶装下铃声、秋千声、小黑的叫声和劈柴声，放进广场的八音盒" },
        new AchDef { id = "drift_bottles", category = "灯塔与海岛", title = "拾潮之人 · 听见大海的回信", desc = "在退潮浅滩寻获镇民留下的发光潮汐漂流瓶" }
    };

    GameState state;
    SaveStore store;
    EggboyUI ui;

    RectTransform toastCard;
    Text badgeTitleText;
    Text badgeDescText;
    Text badgeCountText;
    float showTimer;
    readonly Queue<AchDef> pendingQueue = new Queue<AchDef>();

    public void Init(GameState s, SaveStore st, EggboyUI u)
    {
        Instance = this;
        state = s;
        store = st;
        ui = u;
    }

    public void Init(EggboyUI u, GameState s) => Init(s, null, u);

    public int UnlockedCount => GetUnlockedCount(state);
    public int TotalCount => AllAchievements.Length;
    public IReadOnlyList<AchDef> AllDefinitions => AllAchievements;
    public bool IsUnlocked(string id) => state != null && state.flags.Contains("ach_" + id);

    public static void Unlock(string achId)
    {
        if (Instance == null) return;
        Instance.UnlockInternal(achId);
    }

    void UnlockInternal(string achId)
    {
        if (state == null || ui == null || !ui.inputClose) return;
        string flag = "ach_" + achId;
        if (state.flags.Contains(flag)) return;

        state.flags.Add(flag);
        if (store != null) store.Save(state);

        AchDef found = null;
        for (int i = 0; i < AllAchievements.Length; i++)
        {
            if (AllAchievements[i].id == achId)
            {
                found = AllAchievements[i];
                break;
            }
        }
        if (found == null) return;

        pendingQueue.Enqueue(found);
        if (showTimer <= 0f)
            PopNextAchievement();
    }

    void PopNextAchievement()
    {
        if (pendingQueue.Count == 0) return;
        var def = pendingQueue.Dequeue();
        EnsureToastUI();
        if (toastCard == null) return;

        int unlocked = GetUnlockedCount(state);
        badgeTitleText.text = "嘀嘟！解锁多解成就 · 【" + def.title + "】";
        badgeDescText.text = def.desc;
        badgeCountText.text = $"解法收集进度：{unlocked} / {AllAchievements.Length}（尝试其它通关方案可解锁更多成就）";

        toastCard.gameObject.SetActive(true);
        showTimer = 4.6f;

        // 播放清脆悦耳的“嘀—嘟！”双音成就音效
        StartCoroutine(PlayDiDuChimeRoutine());
    }

    System.Collections.IEnumerator PlayDiDuChimeRoutine()
    {
        ShadowAndChimeSystem.PlayProceduralChime(659.25f, 0.16f); // E5 "嘀"
        yield return new WaitForSecondsRealtime(0.14f);
        ShadowAndChimeSystem.PlayProceduralChime(987.77f, 0.34f); // B5 "嘟！"
    }

    public static int GetUnlockedCount(GameState s)
    {
        if (s == null) return 0;
        int c = 0;
        for (int i = 0; i < AllAchievements.Length; i++)
        {
            if (s.flags.Contains("ach_" + AllAchievements[i].id))
                c++;
        }
        return c;
    }

    void EnsureToastUI()
    {
        if (toastCard != null) return;
        var root = TideTownStyleUI.FindHudCanvasRoot(ui);
        if (root == null) return;

        toastCard = TideTownStyleUI.CreateParchmentCard(root, "AchievementDiDuCard", 450f, 106f, 28);
        toastCard.anchorMin = toastCard.anchorMax = new Vector2(1f, 1f);
        toastCard.pivot = new Vector2(1f, 1f);
        toastCard.anchoredPosition = new Vector2(-22f, -18f);

        badgeTitleText = TideTownStyleUI.CreateText(
            toastCard, "", 15, TideTownStyleUI.AmberAccent, TextAnchor.MiddleLeft,
            new Vector2(0f, 28f), new Vector2(412f, 24f), true);

        badgeDescText = TideTownStyleUI.CreateText(
            toastCard, "", 13, TideTownStyleUI.InkDark, TextAnchor.MiddleLeft,
            new Vector2(0f, 2f), new Vector2(412f, 24f), false);

        badgeCountText = TideTownStyleUI.CreateText(
            toastCard, "", 11, TideTownStyleUI.InkMuted, TextAnchor.MiddleLeft,
            new Vector2(0f, -26f), new Vector2(412f, 20f), false);

        toastCard.gameObject.SetActive(false);
    }

    void Update()
    {
        if (showTimer > 0f)
        {
            showTimer -= Time.unscaledDeltaTime;
            if (showTimer <= 0f)
            {
                if (toastCard != null) toastCard.gameObject.SetActive(false);
                if (pendingQueue.Count > 0)
                    PopNextAchievement();
            }
        }
    }
}
