using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Windows 实机审计：使用发布版的角色 Move、场景碰撞和水位。每条线只在开头设置起点。
public sealed class TraversalAudit : MonoBehaviour
{
    [Serializable] sealed class Check
    {
        public string name;
        public bool passed;
        public Vector3 start, target, end;
        public float progress, maxY;
        public int firstFrame, lastFrame;
    }

    [Serializable] sealed class Report
    {
        public string status;
        public Check[] checks;
    }

    const float Dt = 1f / 60f;
    readonly List<Check> checks = new List<Check>();
    GameBootstrap boot;
    SafePoints rescue;
    string output;
    Camera cameraToRecord;
    RenderTexture frame;
    Texture2D pixels;
    int frameNumber;

    public void Begin(GameBootstrap game)
    {
        boot = game;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return null;
        var prototype = FindObjectOfType<PrototypeGame>();
        if (prototype != null) prototype.enabled = false;
        TidalWindChallenge.Suppress = true;
        rescue = FindObjectOfType<SafePoints>();
        if (rescue != null) rescue.Enabled = false;
        boot.water.SetInstant(0f);
        output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "traversal"));
        Directory.CreateDirectory(output);
        if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "-traversalvideo")) BeginRecording();

        yield return BothWays("market_east_gap", 1.1f, 0.50f, 3.65f, 0.50f, true);
        yield return BothWays("market_west_grass", -2.77f, 0.80f, -4.27f, 0.80f);
        yield return BothWays("bakery_back_lawn", -11.71f, -4.36f, -11.71f, -2.86f);
        yield return BothWays("school_lawn", 8.54f, -6.13f, 7.04f, -6.13f, true);
        yield return BothWays("housing_west", -23.66f, 16.75f, -24.72f, 17.81f, true);
        yield return BothWays("housing_north", 1.39f, 24.22f, 0.33f, 23.16f);
        yield return BothWays("housing_east", 19.60f, 20.08f, 21.10f, 20.08f);
        yield return BothWays("dock_seafront", 22.75f, -17.85f, 24.90f, -17.65f, true);
        yield return Motion("market_wall", 3.8f, 0.15f, 3.8f, 1.75f, true, block: true, record: true);
        yield return Motion("market_fence", 1.3f, -1.75f, 3.1f, -1.75f, true, block: true, record: true);
        yield return Motion("market_corner_slide", 2.22f, .46f, 5.10f, .98f, true, record: true);
        yield return Motion("housing_cliff", -3.95f, 10.41f, -3.95f, 11.66f, true, block: true, record: true);
        yield return TreeTrunk();
        yield return LowStep();
        yield return Beach();

        string status = checks.All(check => check.passed) ? "passed" : "failed";
        File.WriteAllText(Path.Combine(output, "traversal-audit.json"), JsonUtility.ToJson(new Report { status = status, checks = checks.ToArray() }, true));
        Debug.Log($"TRAVERSAL {status} passed={checks.Count(check => check.passed)}/{checks.Count}");
        if (frame != null) frame.Release();
        Application.Quit(status == "passed" ? 0 : 2);
    }

    IEnumerator BothWays(string name, float x0, float z0, float x1, float z1, bool record = true)
    {
        yield return Motion(name + "_walk", x0, z0, x1, z1, false, record: record);
        yield return Motion(name + "_run_back", x1, z1, x0, z0, true, record: record);
    }

    IEnumerator Motion(string name, float x0, float z0, float x1, float z1, bool run, bool block = false, bool jump = false, bool record = false)
    {
        int firstFrame = record ? frameNumber : -1;
        var start = Ground(x0, z0);
        var target = Ground(x1, z1);
        var delta = new Vector3(x1 - x0, 0f, z1 - z0);
        float distance = delta.magnitude;
        var direction = delta / distance;
        Position(start, direction);
        bool validStart = boot.world.Walkable(x0, z0);
        float yMax = start.y, progress = 0f, side = 0f;
        int limit = Mathf.CeilToInt((distance / (run ? 1.8f : 0.6f) + 0.9f) / Dt);
        for (int i = 0; i < limit; i++)
        {
            boot.player.Move(Dt, direction.x, direction.z, run, jump && i == 8);
            var moved = boot.player.transform.position - start;
            yMax = Mathf.Max(yMax, start.y + moved.y);
            moved.y = 0f;
            progress = Vector3.Dot(moved, direction);
            side = Mathf.Max(side, (moved - progress * direction).magnitude);
            if (record && i % 8 == 0) Record(direction);
            if (!block && progress >= distance - 0.09f) break;
            if (i % 5 == 0) yield return null;
        }
        if (record) Record(direction);
        bool passed = validStart && (block ? progress > 0.12f && progress < distance - 0.24f :
            progress >= distance - 0.09f && side < 0.38f && (!jump || yMax - start.y >= 0.27f));
        Add(name, passed, start, target, progress, yMax, firstFrame);
    }

    IEnumerator TreeTrunk()
    {
        foreach (var tree in FindObjectsOfType<MeshRenderer>())
        {
            if (!tree.sharedMaterials.Any(material => material != null && material.name == "NatureTrunk")) continue;
            Vector3 p = tree.transform.position;
            if (p.y < 2.9f || p.y > 5.6f || !boot.world.InLawnArea(p.x, p.z)) continue;
            Vector3 start = Ground(p.x + 1.2f, p.z);
            if (Mathf.Abs(start.y - p.y) > 0.2f || !boot.world.Walkable(start.x, start.z)) continue;
            if (boot.world.Colliders.Any(c => c.r.HasValue && c.top.HasValue && c.top.Value > start.y + .22f &&
                Vector2.Distance(new Vector2(c.x, c.z), new Vector2(start.x, start.z)) < c.r.Value + 0.37f)) continue;
            var center = new Vector3(p.x, start.y, p.z);
            Position(start, Vector3.left);
            int firstFrame = frameNumber;
            float nearest = 1.2f;
            for (int i = 0; i < 150; i++)
            {
                var delta = center - boot.player.transform.position;
                delta.y = 0f;
                var heading = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.left;
                boot.player.Move(Dt, heading.x, heading.z, true, false);
                var position = boot.player.transform.position;
                nearest = Mathf.Min(nearest, Vector2.Distance(new Vector2(position.x, position.z), new Vector2(center.x, center.z)));
                if (i % 8 == 0) Record(heading);
                if (i % 5 == 0) yield return null;
            }
            // 圆形障碍允许从侧面滑开。判定整个过程中身体有没有进入树干位置。
            Add("tree_trunk", nearest >= .45f && nearest <= .90f, start, center, 1.2f - nearest, start.y, firstFrame);
            Debug.Log($"TRAVERSAL tree_trunk_nearest={nearest:F3}");
            yield break;
        }
        Add("tree_trunk", false, Vector3.zero, Vector3.zero, 0f, 0f);
    }

    IEnumerator LowStep()
    {
        // 专用 30cm 台阶夹具。真实地面射线与侧面碰撞走正常入口，退出时随测试进程消失。
        var p = Ground(2.34f, 0.5f);
        var go = new GameObject("Traversal audit 30cm step");
        go.layer = LayerMask.NameToLayer("Ground");
        go.transform.position = p + Vector3.up * .15f;
        go.AddComponent<BoxCollider>().size = new Vector3(.35f, .30f, .8f);
        var collider = new EggboyCollider { x = p.x, z = p.z, hw = .175f, hd = .4f, top = p.y + .30f };
        boot.world.AddCollider(collider);
        Physics.SyncTransforms();
        yield return Motion("low_step_requires_jump", 1.25f, .5f, 3.55f, .5f, true, block: true);
        yield return Motion("low_step_jump", 1.64f, .5f, 3.55f, .5f, true, jump: true);
        boot.world.RemoveCollider(collider);
        Destroy(go);
        yield return null;
    }

    IEnumerator Beach()
    {
        Vector3 direction = Vector3.back;
        Vector3 start = Ground(5.28f, -11.62f);
        Position(start, direction);
        float farthest = start.z;
        for (int i = 0; i < 90; i++)
        {
            boot.player.Move(Dt, 0f, -1f, true, false);
            farthest = Mathf.Min(farthest, boot.player.transform.position.z);
        }
        Add("beach_walk_edge_holds", boot.player.transform.position.y > 2.8f, start, Ground(5.28f, -15f), start.z - farthest, start.y);

        Position(start, direction);
        int jumpFrames = frameNumber;
        bool landed = false;
        float landingZ = 0f;
        for (int i = 0; i < 240; i++)
        {
            // Aim for the open sand on the right of the boulder below the launch point.
            float steer = i >= 12 && !landed && boot.player.transform.position.z <= -12.55f ? 0.42f : 0f;
            boot.player.Move(Dt, steer, -1f, true, i == 12);
            var p = boot.player.transform.position;
            if (!landed && boot.player.grounded && boot.world.IsBeachSurface(p.x, p.z, p.y)) { landed = true; landingZ = p.z; }
            if (i % 8 == 0) Record(direction);
            if (landed && p.z < landingZ - 1.0f) break;
            if (i % 5 == 0) yield return null;
        }
        var sand = boot.player.transform.position;
        Add("beach_jump_land_and_walk", landed && sand.z < landingZ - 0.9f, start, Ground(6.18f, -15f), start.z - sand.z, sand.y, jumpFrames);
        if (landed && rescue != null)
        {
            int rescueFrames = frameNumber;
            int count = rescue.RescueCount;
            rescue.Enabled = true;
            float deadline = Time.realtimeSinceStartup + 10f;
            float nextFrame = Time.realtimeSinceStartup;
            while (rescue.RescueCount == count && Time.realtimeSinceStartup < deadline)
            {
                boot.player.Move(Mathf.Min(Time.deltaTime, .05f), 0f, 1f, true, false);
                if (Time.realtimeSinceStartup >= nextFrame)
                {
                    Record(Vector3.forward);
                    nextFrame += 8f * Dt;
                }
                yield return null;
            }
            Record(Vector3.forward);
            Add("beach_return_rescue", rescue.RescueCount == count + 1 && rescue.LastReason == "beach" && boot.player.transform.position.y > .9f,
                sand, rescue.LastTarget, 0f, boot.player.transform.position.y, rescueFrames);
            rescue.Enabled = false;
        }
        else Add("beach_return_rescue", false, sand, start, 0f, sand.y);
        boot.water.SetInstant(.45f);
        Position(start, direction);
        for (int i = 0; i < 120; i++) boot.player.Move(Dt, 0f, -1f, true, i == 12);
        Add("flooded_beach_closed", !boot.world.Walkable(5.28f, -15f) && boot.player.transform.position.y > 2.8f,
            start, Ground(5.28f, -15f), 0f, boot.player.transform.position.y);
    }

    Vector3 Ground(float x, float z) => new Vector3(x, boot.world.GroundAt(x, z), z);

    void Position(Vector3 p, Vector3 direction)
    {
        boot.player.transform.SetPositionAndRotation(p, Quaternion.LookRotation(direction));
        boot.player.yaw = Mathf.Atan2(direction.x, direction.z);
        boot.player.vel = Vector3.zero;
        boot.player.locked = false;
        boot.player.grounded = true;
        boot.player.jump = new JumpState();
    }

    void Add(string name, bool passed, Vector3 start, Vector3 target, float progress, float height, int firstFrame = -1)
    {
        var end = boot.player.transform.position;
        checks.Add(new Check { name = name, passed = passed, start = start, target = target, end = end, progress = progress, maxY = height, firstFrame = firstFrame, lastFrame = firstFrame >= 0 ? frameNumber - 1 : -1 });
        Debug.Log($"TRAVERSAL {name}={passed} progress={progress:F2} start={start} end={end}");
    }

    void BeginRecording()
    {
        cameraToRecord = Camera.main;
        if (cameraToRecord == null) return;
        cameraToRecord.enabled = false;
        frame = new RenderTexture(854, 480, 24, RenderTextureFormat.ARGB32);
        pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false);
    }

    void Record(Vector3 heading)
    {
        if (cameraToRecord == null || frame == null) return;
        var p = boot.player.transform.position;
        var side = Vector3.Cross(Vector3.up, heading);
        cameraToRecord.transform.position = p - heading * 2.4f + side * .65f + Vector3.up * 2.4f;
        cameraToRecord.transform.LookAt(p + heading * .35f + Vector3.up * .5f);
        cameraToRecord.targetTexture = frame;
        cameraToRecord.Render();
        var active = RenderTexture.active;
        RenderTexture.active = frame;
        pixels.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0);
        pixels.Apply();
        RenderTexture.active = active;
        File.WriteAllBytes(Path.Combine(output, $"frame_{frameNumber++:D4}.jpg"), pixels.EncodeToJPG(88));
    }
}
