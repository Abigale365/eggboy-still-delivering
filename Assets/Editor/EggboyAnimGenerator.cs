using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class EggboyAnimGenerator
{
    const string ModelPath = "Assets/Resources/Models/eggboy.fbx";
    const string OutDir = "Assets/Resources/Animations";

    struct KeyPose
    {
        public float time;
        public float bodyY;
        public float lean;    // X axis (deg), + = forward
        public float tilt;    // Z axis (deg)
        public float twist;   // Y axis (deg)
        public float legBend; // deg, bends legs forward & keeps feet flat
        public float armR0, armR1; // armR0: raise/lower (Z), armR1: swing fwd/back (Y)
        public float armL0, armL1;
        public float handR0, handR1;
        public float handL0, handL1;
        public float antX;
    }

    [MenuItem("EGGBOY/生成抱柴火动作资产")]
    public static void GeneratePickupWood() => GenerateAll();

    [MenuItem("EGGBOY/生成全部互动动作资产")]
    public static void GenerateAll()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (prefab == null)
        {
            Debug.LogError("找不到模型: " + ModelPath);
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(OutDir))
            AssetDatabase.CreateFolder("Assets/Resources", "Animations");

        var inst = Object.Instantiate(prefab);
        try
        {
            var anim = inst.GetComponentInChildren<Animation>();
            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            if (anim == null || smr == null)
            {
                Debug.LogError("模型缺少 Animation 或 SkinnedMeshRenderer");
                return;
            }
            anim.Stop();
            var bones = new EggboyBones(inst, smr);

            // 4.1 序章与通用
            BuildClip(anim, smr, bones, "pickup_mailbox", WrapMode.Once, new[]
            {
                new KeyPose { time = 0.00f, bodyY =  0.000f, lean =  0f, armR0 = -16f, armR1 = 10f, armL0 = -16f, armL1 = 10f, handR1 = 10f, handL1 = 10f },
                new KeyPose { time = 0.18f, bodyY =  0.012f, lean = -4f, twist = -8f, armR0 =  42f, armR1 = 36f, armL0 = -18f, armL1 =  6f, handR1 = 22f, antX = -6f },
                new KeyPose { time = 0.40f, bodyY =  0.018f, lean =  6f, twist = -12f, armR0 = 58f, armR1 = 62f, armL0 = -12f, armL1 = 16f, handR1 = 48f, antX =  8f },
                new KeyPose { time = 0.72f, bodyY =  0.002f, lean =  8f, twist =  0f, armR0 =  24f, armR1 = 50f, armL0 =  20f, armL1 = 46f, handR1 = 42f, handL1 = 38f, antX = 10f },
                new KeyPose { time = 1.00f, bodyY =  0.000f, lean =  2f, twist =  0f, armR0 =  12f, armR1 = 38f, armL0 =  12f, armL1 = 38f, handR1 = 30f, handL1 = 30f, antX =  2f },
            });

            BuildClip(anim, smr, bones, "read_letter", WrapMode.Loop, new[]
            {
                new KeyPose { time = 0.00f, bodyY = -0.004f, lean = 10f, tilt =  0f, armR0 = 18f, armR1 = 48f, armL0 = 18f, armL1 = 48f, handR1 = 44f, handL1 = 44f, antX = 12f },
                new KeyPose { time = 0.40f, bodyY = -0.008f, lean = 14f, tilt =  3f, armR0 = 20f, armR1 = 50f, armL0 = 20f, armL1 = 50f, handR1 = 46f, handL1 = 46f, antX = 16f },
                new KeyPose { time = 0.80f, bodyY = -0.005f, lean = 11f, tilt = -2f, armR0 = 19f, armR1 = 49f, armL0 = 19f, armL1 = 49f, handR1 = 45f, handL1 = 45f, antX = 13f },
                new KeyPose { time = 1.20f, bodyY = -0.004f, lean = 10f, tilt =  0f, armR0 = 18f, armR1 = 48f, armL0 = 18f, armL1 = 48f, handR1 = 44f, handL1 = 44f, antX = 12f },
            });

            BuildClip(anim, smr, bones, "pickup_drop", WrapMode.Once, new[]
            {
                new KeyPose { time = 0.00f, bodyY =  0.000f, lean =  0f, legBend =  0f, armR0 = -16f, armR1 = 10f, armL0 = -16f, armL1 = 10f },
                new KeyPose { time = 0.16f, bodyY = -0.030f, lean = 18f, legBend = 16f, armR0 = -20f, armR1 = 22f, armL0 = -20f, armL1 = 22f, antX = 12f },
                new KeyPose { time = 0.42f, bodyY = -0.065f, lean = 38f, legBend = 32f, armR0 = -14f, armR1 = 58f, armL0 = -14f, armL1 = 54f, handR1 = 46f, handL1 = 42f, antX = 22f },
                new KeyPose { time = 0.72f, bodyY =  0.004f, lean = -4f, legBend =  0f, armR0 =  16f, armR1 = 46f, armL0 =  16f, armL1 = 46f, handR1 = 38f, handL1 = 38f, antX = -8f },
                new KeyPose { time = 0.95f, bodyY =  0.000f, lean =  0f, legBend =  0f, armR0 = -12f, armR1 = 16f, armL0 = -12f, armL1 = 16f, handR1 = 14f, handL1 = 14f, antX =  0f },
            });

            // 4.2 面包店章节
            BuildClip(anim, smr, bones, "pickup_matches", WrapMode.Once, new[]
            {
                new KeyPose { time = 0.00f, bodyY =  0.000f, lean =  0f, twist =   0f, armR0 = -16f, armR1 = 10f, armL0 = -16f, armL1 = 10f },
                new KeyPose { time = 0.16f, bodyY = -0.008f, lean =  8f, twist =  -8f, armR0 =  18f, armR1 = 38f, armL0 = -18f, armL1 =  8f, handR1 = 20f, antX =  6f },
                new KeyPose { time = 0.35f, bodyY = -0.012f, lean = 14f, twist = -14f, armR0 =  28f, armR1 = 58f, armL0 = -14f, armL1 = 14f, handR1 = 52f, antX = 12f },
                new KeyPose { time = 0.62f, bodyY =  0.002f, lean =  2f, twist =  16f, armR0 =   6f, armR1 = 42f, armL0 =  10f, armL1 = 34f, handR1 = 45f, handL1 = 30f, antX = -4f },
                new KeyPose { time = 0.85f, bodyY =  0.000f, lean =  0f, twist =   0f, armR0 = -14f, armR1 = 14f, armL0 = -14f, armL1 = 14f, handR1 = 12f, handL1 = 12f, antX =  0f },
            });

            BuildClip(anim, smr, bones, "pickup_wood", WrapMode.Once, new[]
            {
                new KeyPose { time = 0.00f, bodyY =  0.000f, lean =  0f, legBend =  0f, armR0 = -16f, armR1 = 10f, armL0 = -16f, armL1 = 10f, handR1 = 10f, handL1 = 10f, antX =  0f },
                new KeyPose { time = 0.16f, bodyY = -0.022f, lean = 14f, legBend = 12f, armR0 = -24f, armR1 = -8f, armL0 = -24f, armL1 = -8f, handR1 =  5f, handL1 =  5f, antX =  8f },
                new KeyPose { time = 0.44f, bodyY = -0.062f, lean = 36f, legBend = 30f, armR0 = -18f, armR1 = 56f, armL0 = -18f, armL1 = 56f, handR1 = 38f, handL1 = 38f, antX = 20f },
                new KeyPose { time = 0.62f, bodyY = -0.048f, lean = 24f, legBend = 22f, armR0 =   4f, armR1 = 60f, armL0 =   4f, armL1 = 60f, handR1 = 44f, handL1 = 44f, antX = 10f },
                new KeyPose { time = 0.86f, bodyY =  0.008f, lean = -6f, legBend =  0f, armR0 =  22f, armR1 = 58f, armL0 =  22f, armL1 = 58f, handR1 = 48f, handL1 = 48f, antX = -12f },
                new KeyPose { time = 1.15f, bodyY =  0.000f, lean =  2f, legBend =  0f, armR0 =  14f, armR1 = 52f, armL0 =  14f, armL1 = 52f, handR1 = 42f, handL1 = 42f, antX =  2f },
            });

            BuildClip(anim, smr, bones, "load_wood", WrapMode.Once, new[]
            {
                new KeyPose { time = 0.00f, bodyY =  0.000f, lean =  2f, legBend =  0f, armR0 =  14f, armR1 = 52f, armL0 =  14f, armL1 = 52f, handR1 = 42f, handL1 = 42f, antX =  2f },
                new KeyPose { time = 0.22f, bodyY = -0.028f, lean = 18f, legBend = 14f, armR0 =   8f, armR1 = 44f, armL0 =   8f, armL1 = 44f, handR1 = 46f, handL1 = 46f, antX = 10f },
                new KeyPose { time = 0.50f, bodyY = -0.046f, lean = 30f, legBend = 24f, armR0 =  24f, armR1 = 68f, armL0 =  24f, armL1 = 68f, handR1 = 15f, handL1 = 15f, antX = 18f },
                new KeyPose { time = 0.78f, bodyY = -0.010f, lean =  6f, legBend =  4f, armR0 =  26f, armR1 = 48f, armL0 =  26f, armL1 = 48f, handR1 = 50f, handL1 = 50f, antX = -6f },
                new KeyPose { time = 1.10f, bodyY =  0.000f, lean =  0f, legBend =  0f, armR0 = -16f, armR1 = 12f, armL0 = -16f, armL1 = 12f, handR1 = 10f, handL1 = 10f, antX =  0f },
            });

            BuildClip(anim, smr, bones, "hold_light_stove", WrapMode.Loop, new[]
            {
                new KeyPose { time = 0.00f, bodyY = -0.036f, lean = 24f, legBend = 18f, armR0 = 10f, armR1 = 54f, armL0 = 14f, armL1 = 50f, handR1 = 46f, handL1 = 42f, antX = 14f },
                new KeyPose { time = 0.18f, bodyY = -0.040f, lean = 26f, legBend = 20f, twist = -6f, armR0 = 18f, armR1 = 62f, armL0 = 14f, armL1 = 52f, handR1 = 56f, handL1 = 44f, antX = 18f },
                new KeyPose { time = 0.35f, bodyY = -0.034f, lean = 23f, legBend = 17f, twist =  6f, armR0 =  8f, armR1 = 48f, armL0 = 16f, armL1 = 54f, handR1 = 38f, handL1 = 46f, antX = 12f },
                new KeyPose { time = 0.52f, bodyY = -0.040f, lean = 26f, legBend = 20f, twist = -4f, armR0 = 16f, armR1 = 60f, armL0 = 14f, armL1 = 50f, handR1 = 54f, handL1 = 42f, antX = 17f },
                new KeyPose { time = 0.70f, bodyY = -0.036f, lean = 24f, legBend = 18f, armR0 = 10f, armR1 = 54f, armL0 = 14f, armL1 = 50f, handR1 = 46f, handL1 = 42f, antX = 14f },
            });

            BuildClip(anim, smr, bones, "light_stove_done", WrapMode.Once, new[]
            {
                new KeyPose { time = 0.00f, bodyY = -0.032f, lean = 22f, legBend = 16f, armR0 = 12f, armR1 = 52f, armL0 = 12f, armL1 = 52f, handR1 = 42f, handL1 = 42f, antX = 12f },
                new KeyPose { time = 0.25f, bodyY =  0.010f, lean = -10f, legBend = 0f, armR0 = 72f, armR1 = 28f, armL0 = 72f, armL1 = 28f, handR1 = 24f, handL1 = 24f, antX = -18f },
                new KeyPose { time = 0.52f, bodyY =  0.032f, lean = -4f, legBend =  6f, armR0 = 92f, armR1 = 20f, armL0 = 92f, armL1 = 20f, handR1 = 30f, handL1 = 30f, antX = -10f },
                new KeyPose { time = 0.76f, bodyY = -0.012f, lean =  4f, legBend =  8f, armR0 = 46f, armR1 = 48f, armL0 = 46f, armL1 = 48f, handR1 = 48f, handL1 = 48f, antX =  8f },
                new KeyPose { time = 1.00f, bodyY =  0.000f, lean =  0f, legBend =  0f, armR0 = -14f, armR1 = 14f, armL0 = -14f, armL1 = 14f, handR1 = 12f, handL1 = 12f, antX =  0f },
            });

            BuildClip(anim, smr, bones, "inspect_memory", WrapMode.Once, new[]
            {
                new KeyPose { time = 0.00f, bodyY =  0.000f, lean =  0f, tilt = 0f, armR0 = -16f, armR1 = 10f, armL0 = -16f, armL1 = 10f },
                new KeyPose { time = 0.22f, bodyY = -0.014f, lean = 14f, tilt = 6f, twist = -6f, armR0 = 26f, armR1 = 44f, armL0 = -22f, armL1 = -16f, handR1 = 32f, antX = 10f },
                new KeyPose { time = 0.45f, bodyY = -0.022f, lean = 22f, tilt = 10f, twist = -8f, armR0 = 42f, armR1 = 56f, armL0 = -24f, armL1 = -20f, handR1 = 48f, antX = 16f },
                new KeyPose { time = 0.78f, bodyY = -0.016f, lean = 16f, tilt = -6f, twist =  4f, armR0 = 34f, armR1 = 48f, armL0 =  48f, armL1 = 42f, handR1 = 38f, handL1 = 50f, antX = 12f },
                new KeyPose { time = 1.05f, bodyY =  0.000f, lean =  4f, tilt =  4f, twist =  0f, armR0 = -12f, armR1 = 16f, armL0 =  42f, armL1 = 38f, handR1 = 14f, handL1 = 46f, antX =  4f },
            });

            // 4.3 学校章节
            BuildClip(anim, smr, bones, "inspect_slipper", WrapMode.Once, new[]
            {
                new KeyPose { time = 0.00f, bodyY =  0.000f, lean =  0f, legBend =  0f, armR0 = -16f, armR1 = 10f, armL0 = -16f, armL1 = 10f },
                new KeyPose { time = 0.24f, bodyY = -0.048f, lean = 30f, legBend = 24f, armR0 = -10f, armR1 = 52f, armL0 = -10f, armL1 = 44f, handR1 = 42f, handL1 = 36f, antX = 18f },
                new KeyPose { time = 0.48f, bodyY = -0.010f, lean =  8f, tilt =  9f, legBend = 6f, armR0 = 38f, armR1 = 54f, armL0 = 34f, armL1 = 50f, handR1 = 48f, handL1 = 46f, antX = 10f },
                new KeyPose { time = 0.84f, bodyY = -0.006f, lean =  6f, tilt = -9f, legBend = 4f, armR0 = 42f, armR1 = 52f, armL0 = 38f, armL1 = 48f, handR1 = 48f, handL1 = 46f, antX =  8f },
                new KeyPose { time = 1.20f, bodyY =  0.000f, lean =  4f, tilt =  4f, legBend = 0f, armR0 = 24f, armR1 = 42f, armL0 = 24f, armL1 = 42f, handR1 = 36f, handL1 = 36f, antX =  4f },
            });

            BuildClip(anim, smr, bones, "pickup_ball", WrapMode.Once, new[]
            {
                new KeyPose { time = 0.00f, bodyY =  0.000f, lean =  0f, legBend =  0f, armR0 = -16f, armR1 = 10f, armL0 = -16f, armL1 = 10f, handR1 = 10f, handL1 = 10f },
                new KeyPose { time = 0.16f, bodyY = -0.026f, lean = 16f, legBend = 14f, armR0 = -18f, armR1 = 18f, armL0 = -18f, armL1 = 18f, handR1 = 16f, handL1 = 16f, antX = 10f },
                new KeyPose { time = 0.40f, bodyY = -0.056f, lean = 32f, legBend = 28f, armR0 = -12f, armR1 = 52f, armL0 = -12f, armL1 = 52f, handR1 = 48f, handL1 = 48f, antX = 18f },
                new KeyPose { time = 0.68f, bodyY =  0.010f, lean = -4f, legBend =  0f, armR0 =  16f, armR1 = 50f, armL0 =  16f, armL1 = 50f, handR1 = 52f, handL1 = 52f, antX = -8f },
                new KeyPose { time = 0.95f, bodyY =  0.000f, lean =  2f, legBend =  0f, armR0 =   8f, armR1 = 46f, armL0 =   8f, armL1 = 46f, handR1 = 48f, handL1 = 48f, antX =  2f },
            });

            BuildClip(anim, smr, bones, "put_ball_bowl", WrapMode.Once, new[]
            {
                new KeyPose { time = 0.00f, bodyY =  0.000f, lean =  2f, legBend =  0f, armR0 =   8f, armR1 = 46f, armL0 =   8f, armL1 = 46f, handR1 = 48f, handL1 = 48f },
                new KeyPose { time = 0.22f, bodyY = -0.032f, lean = 20f, legBend = 18f, armR0 =  -4f, armR1 = 52f, armL0 =  -4f, armL1 = 52f, handR1 = 44f, handL1 = 44f, antX = 12f },
                new KeyPose { time = 0.48f, bodyY = -0.058f, lean = 34f, legBend = 28f, armR0 = -14f, armR1 = 60f, armL0 = -14f, armL1 = 60f, handR1 = 12f, handL1 = 12f, antX = 20f },
                new KeyPose { time = 0.78f, bodyY =  0.004f, lean = -6f, twist = 14f, legBend = 0f, armR0 = 32f, armR1 = 36f, armL0 = 28f, armL1 = 32f, handR1 = 28f, handL1 = 28f, antX = -10f },
                new KeyPose { time = 1.05f, bodyY =  0.000f, lean =  0f, twist =  0f, legBend = 0f, armR0 = -14f, armR1 = 14f, armL0 = -14f, armL1 = 14f, handR1 = 12f, handL1 = 12f, antX =  0f },
            });

            BuildClip(anim, smr, bones, "pet_dog", WrapMode.Once, new[]
            {
                new KeyPose { time = 0.00f, bodyY =  0.000f, lean =  0f, legBend =  0f, armR0 = -16f, armR1 = 10f, armL0 = -16f, armL1 = 10f },
                new KeyPose { time = 0.25f, bodyY = -0.048f, lean = 26f, legBend = 24f, armR0 =  18f, armR1 = 56f, armL0 =   4f, armL1 = 32f, handR1 = 36f, handL1 = 28f, antX = 14f },
                new KeyPose { time = 0.48f, bodyY = -0.052f, lean = 28f, legBend = 26f, armR0 =   6f, armR1 = 60f, armL0 =   6f, armL1 = 34f, handR1 = 48f, handL1 = 30f, antX = 18f },
                new KeyPose { time = 0.70f, bodyY = -0.046f, lean = 25f, legBend = 24f, armR0 =  20f, armR1 = 56f, armL0 =   6f, armL1 = 34f, handR1 = 34f, handL1 = 30f, antX = 12f },
                new KeyPose { time = 0.92f, bodyY = -0.052f, lean = 28f, legBend = 26f, armR0 =   8f, armR1 = 60f, armL0 =   8f, armL1 = 36f, handR1 = 48f, handL1 = 32f, antX = 16f },
                new KeyPose { time = 1.20f, bodyY =  0.000f, lean =  0f, legBend =  0f, armR0 = -14f, armR1 = 14f, armL0 = -14f, armL1 = 14f, handR1 = 12f, handL1 = 12f, antX =  0f },
            });

            // 4.4 灯塔与结局章节
            BuildClip(anim, smr, bones, "pickup_oil", WrapMode.Once, new[]
            {
                new KeyPose { time = 0.00f, bodyY =  0.000f, lean =  0f, tilt =  0f, legBend =  0f, armR0 = -16f, armR1 = 10f, armL0 = -16f, armL1 = 10f },
                new KeyPose { time = 0.18f, bodyY = -0.024f, lean = 16f, tilt = -6f, legBend = 14f, armR0 = -22f, armR1 = 24f, armL0 = -12f, armL1 =  6f, antX = 10f },
                new KeyPose { time = 0.45f, bodyY = -0.056f, lean = 32f, tilt = -12f, legBend = 28f, armR0 = -16f, armR1 = 52f, armL0 =   8f, armL1 = 18f, handR0 = 12f, handR1 = 44f, antX = 18f },
                new KeyPose { time = 0.76f, bodyY =  0.006f, lean = -4f, tilt =  6f, legBend =  2f, armR0 =  -2f, armR1 = 38f, armL0 =  14f, armL1 = 14f, handR0 = 10f, handR1 = 28f, antX = -8f },
                new KeyPose { time = 1.05f, bodyY =  0.000f, lean =  2f, tilt =  2f, legBend =  0f, armR0 =  -6f, armR1 = 34f, armL0 = -14f, armL1 = 10f, handR0 = 10f, handR1 = 24f, antX =  2f },
            });

            BuildClip(anim, smr, bones, "open_lighthouse_door", WrapMode.Once, new[]
            {
                new KeyPose { time = 0.00f, bodyY =  0.000f, lean =  0f, twist =  0f, armR0 = -16f, armR1 = 10f, armL0 = -16f, armL1 = 10f },
                new KeyPose { time = 0.18f, bodyY = -0.010f, lean =  8f, twist =  8f, armR0 =  -6f, armR1 = 34f, armL0 =  22f, armL1 = 36f, handL1 = 26f, antX =  6f },
                new KeyPose { time = 0.40f, bodyY = -0.018f, lean = 18f, twist = 14f, armR0 =  -6f, armR1 = 34f, armL0 =  36f, armL1 = 68f, handL1 = 12f, antX = 14f },
                new KeyPose { time = 0.68f, bodyY = -0.006f, lean = 10f, twist =  6f, armR0 =  -6f, armR1 = 34f, armL0 =  24f, armL1 = 48f, handL1 = 20f, antX =  8f },
                new KeyPose { time = 0.90f, bodyY =  0.000f, lean =  0f, twist =  0f, armR0 =  -6f, armR1 = 34f, armL0 = -14f, armL1 = 12f, handL1 = 12f, antX =  0f },
            });

            BuildClip(anim, smr, bones, "hold_pour_oil", WrapMode.Loop, new[]
            {
                new KeyPose { time = 0.00f, bodyY = -0.010f, lean = 12f, tilt = -4f, armR0 = 44f, armR1 = 56f, armL0 = 38f, armL1 = 52f, handR0 = 18f, handR1 = 46f, handL1 = 42f, antX = 10f },
                new KeyPose { time = 0.26f, bodyY = -0.014f, lean = 16f, tilt = -8f, armR0 = 54f, armR1 = 60f, armL0 = 46f, armL1 = 56f, handR0 = 28f, handR1 = 52f, handL1 = 48f, antX = 14f },
                new KeyPose { time = 0.54f, bodyY = -0.008f, lean = 10f, tilt = -2f, armR0 = 40f, armR1 = 54f, armL0 = 36f, armL1 = 50f, handR0 = 14f, handR1 = 42f, handL1 = 40f, antX =  8f },
                new KeyPose { time = 0.80f, bodyY = -0.010f, lean = 12f, tilt = -4f, armR0 = 44f, armR1 = 56f, armL0 = 38f, armL1 = 52f, handR0 = 18f, handR1 = 46f, handL1 = 42f, antX = 10f },
            });

            BuildClip(anim, smr, bones, "signal_wave", WrapMode.Once, new[]
            {
                new KeyPose { time = 0.00f, bodyY =  0.000f, lean =  0f, tilt =  0f, armR0 = -16f, armR1 = 10f, armL0 = -16f, armL1 = 10f },
                new KeyPose { time = 0.22f, bodyY =  0.014f, lean = -6f, tilt = -8f, armR0 = 102f, armR1 = 22f, armL0 =  16f, armL1 = 24f, handR0 = -16f, handR1 = 18f, antX = -10f },
                new KeyPose { time = 0.48f, bodyY =  0.018f, lean = -4f, tilt =  8f, armR0 =  76f, armR1 = 26f, armL0 =  22f, armL1 = 28f, handR0 =  24f, handR1 = 22f, antX =  -6f },
                new KeyPose { time = 0.74f, bodyY =  0.014f, lean = -6f, tilt = -8f, armR0 = 104f, armR1 = 22f, armL0 =  16f, armL1 = 24f, handR0 = -18f, handR1 = 18f, antX = -10f },
                new KeyPose { time = 1.00f, bodyY =  0.016f, lean = -4f, tilt =  6f, armR0 =  78f, armR1 = 26f, armL0 =  20f, armL1 = 26f, handR0 =  20f, handR1 = 22f, antX =  -6f },
                new KeyPose { time = 1.30f, bodyY =  0.000f, lean =  0f, tilt =  0f, armR0 = -14f, armR1 = 12f, armL0 = -14f, armL1 = 12f, handR0 =   0f, handR1 = 12f, antX =   0f },
            });

            BuildClip(anim, smr, bones, "ending_desk", WrapMode.Once, new[]
            {
                new KeyPose { time = 0.00f, bodyY =  0.000f, lean =  0f, armR0 = -16f, armR1 = 10f, armL0 = -16f, armL1 = 10f },
                new KeyPose { time = 0.22f, bodyY = -0.014f, lean = 16f, armR0 =  20f, armR1 = 52f, armL0 =  20f, armL1 = 52f, handR1 = 22f, handL1 = 22f, antX = 12f },
                new KeyPose { time = 0.45f, bodyY = -0.020f, lean = 20f, armR0 =  26f, armR1 = 58f, armL0 =  26f, armL1 = 58f, handR1 = 46f, handL1 = 46f, antX = 16f },
                new KeyPose { time = 0.80f, bodyY = -0.004f, lean =  8f, armR0 =  18f, armR1 = 48f, armL0 =  18f, armL1 = 48f, handR1 = 44f, handL1 = 44f, antX =  8f },
                new KeyPose { time = 1.15f, bodyY = -0.004f, lean = 10f, armR0 =  18f, armR1 = 48f, armL0 =  18f, armL1 = 48f, handR1 = 44f, handL1 = 44f, antX = 12f },
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("全部 18 套互动动作资产已生成完毕: " + OutDir);
        }
        finally
        {
            Object.DestroyImmediate(inst);
        }
    }

    static void BuildClip(Animation anim, SkinnedMeshRenderer smr, EggboyBones bones, string clipName, WrapMode wrapMode, KeyPose[] keys)
    {
        var rotCurves = new Dictionary<Transform, AnimationCurve[]>();
        var posCurves = new Dictionary<Transform, AnimationCurve[]>();
        var tracked = new List<Transform>();

        foreach (var b in smr.bones)
        {
            if (b == null || rotCurves.ContainsKey(b)) continue;
            tracked.Add(b);
            rotCurves[b] = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            posCurves[b] = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
        }

        foreach (var k in keys)
        {
            bones.RestoreBindPose();
            ApplyPose(bones, k);

            foreach (var b in tracked)
            {
                var r = b.localRotation;
                var p = b.localPosition;
                rotCurves[b][0].AddKey(k.time, r.x);
                rotCurves[b][1].AddKey(k.time, r.y);
                rotCurves[b][2].AddKey(k.time, r.z);
                rotCurves[b][3].AddKey(k.time, r.w);
                posCurves[b][0].AddKey(k.time, p.x);
                posCurves[b][1].AddKey(k.time, p.y);
                posCurves[b][2].AddKey(k.time, p.z);
            }
        }

        var clip = new AnimationClip
        {
            name = clipName,
            legacy = true,
            wrapMode = wrapMode,
            frameRate = 30f
        };

        Transform rootTr = anim.transform;
        foreach (var b in tracked)
        {
            string path = AnimationUtility.CalculateTransformPath(b, rootTr);
            var rc = rotCurves[b];
            var pc = posCurves[b];
            clip.SetCurve(path, typeof(Transform), "localRotation.x", rc[0]);
            clip.SetCurve(path, typeof(Transform), "localRotation.y", rc[1]);
            clip.SetCurve(path, typeof(Transform), "localRotation.z", rc[2]);
            clip.SetCurve(path, typeof(Transform), "localRotation.w", rc[3]);
            clip.SetCurve(path, typeof(Transform), "localPosition.x", pc[0]);
            clip.SetCurve(path, typeof(Transform), "localPosition.y", pc[1]);
            clip.SetCurve(path, typeof(Transform), "localPosition.z", pc[2]);
        }

        clip.EnsureQuaternionContinuity();

        string assetPath = $"{OutDir}/{clipName}.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        if (existing != null) AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(clip, assetPath);
        Debug.Log($"已生成动作资产: {assetPath} (length={clip.length:F2}s, wrap={wrapMode})");
    }

    static void ApplyPose(EggboyBones bones, KeyPose k)
    {
        float d = Mathf.Deg2Rad;
        if (bones.Get("body") != null)
        {
            bones.Move("body", 0f, k.bodyY, 0f);
            bones.Rotate("body", Vector3.right, k.lean * d);
            bones.Rotate("body", Vector3.forward, k.tilt * d);
            bones.Rotate("body", Vector3.up, k.twist * d);
        }
        float bend = k.legBend * d;
        bones.Rotate("legL", Vector3.right, -bend);
        bones.Rotate("legR", Vector3.right, -bend);
        bones.Rotate("footL", Vector3.right, bend);
        bones.Rotate("footR", Vector3.right, bend);

        bones.Rotate("armR", Vector3.up, k.armR1 * d);
        bones.Rotate("armR", Vector3.forward, -k.armR0 * d);
        bones.Rotate("armL", Vector3.up, -k.armL1 * d);
        bones.Rotate("armL", Vector3.forward, k.armL0 * d);

        bones.Rotate("handR", Vector3.up, k.handR1 * d);
        bones.Rotate("handR", Vector3.forward, -k.handR0 * d);
        bones.Rotate("handL", Vector3.up, -k.handL1 * d);
        bones.Rotate("handL", Vector3.forward, k.handL0 * d);

        bones.Rotate("antenna1", Vector3.right, k.antX * 0.5f * d);
        bones.Rotate("antenna2", Vector3.right, k.antX * d);
    }
}
