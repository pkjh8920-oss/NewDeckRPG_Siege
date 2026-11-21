// ============================================================
// Vfx.cs
// 그림 파일 없이 도형으로 만드는 간단한 이펙트 모음입니다.
//
//   Vfx.Slash   : 베기 궤적 (하얀 선 + 색 선이 쭉 그어졌다가 사라짐)
//   Vfx.Sparks  : 불똥 (작은 동그라미들이 사방으로 튀었다가 떨어짐)
//   Vfx.Ring    : 퍼지는 고리 (방어도 얻을 때)
//   Vfx.Rise    : 위로 올라가는 반짝이 (회복할 때)
//   Firefly     : 배경에 떠다니는 반딧불
//
// VfxPart 하나가 "이펙트 조각 하나"이고, 정해진 시간 동안 움직이다가 스스로 사라집니다.
// ============================================================

using UnityEngine;
using UnityEngine.UI;

public static class Vfx
{
    static VfxPart Make(string name, Transform layer, Sprite sprite, Color color, Vector2 pos, float angle, bool sliced)
    {
        RectTransform rt = UIFactory.MakeRect(name, layer);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        if (sliced) img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.localEulerAngles = new Vector3(0, 0, angle);
        rt.sizeDelta = Vector2.zero;
        return rt.gameObject.AddComponent<VfxPart>();
    }

    // 베기: 비스듬한 선이 순식간에 길어졌다가 가늘어지며 사라짐
    public static void Slash(Transform layer, Vector2 pos, Color color)
    {
        float angle = Random.Range(20f, 45f) * (Random.value < 0.5f ? 1f : -1f);
        VfxPart glow = Make("SlashGlow", layer, UIFactory.Rounded, new Color(color.r, color.g, color.b, 0.75f), pos, angle, true);
        glow.Life = 0.32f; glow.MidTime = 0.25f; glow.FadeFrom = 0.3f;
        glow.StartSize = new Vector2(30, 34); glow.MidSize = new Vector2(330, 34); glow.EndSize = new Vector2(360, 4);

        VfxPart core = Make("SlashCore", layer, UIFactory.Rounded, Color.white, pos, angle, true);
        core.Life = 0.28f; core.MidTime = 0.25f; core.FadeFrom = 0.3f;
        core.StartSize = new Vector2(30, 14); core.MidSize = new Vector2(300, 14); core.EndSize = new Vector2(330, 2);
    }

    // 불똥: count 개가 사방으로 튐
    public static void Sparks(Transform layer, Vector2 pos, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(250f, 650f);
            VfxPart p = Make("Spark", layer, UIFactory.Circle, color, pos, 0f, false);
            p.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed;
            p.Gravity = 1100f;
            p.Life = Random.Range(0.35f, 0.6f);
            float size = Random.Range(8f, 16f);
            p.StartSize = new Vector2(size, size);
            p.MidSize = p.StartSize;
            p.EndSize = new Vector2(2, 2);
            p.FadeFrom = 0.4f;
        }
    }

    // ================= 필살기 연출 =================

    // 날카로운 조각: 몸 주위에서 바깥으로 뿜어져 나감 (붉은 기운, 검은 깃털, 자홍 결정 등)
    public static void Shards(Transform layer, Vector2 pos, Color color, int count, float speed, float spread)
    {
        for (int i = 0; i < count; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f + 0.25f).normalized;   // 조금 위쪽으로
            Vector2 start = pos + dir * Random.Range(0f, spread);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;   // 삼각형 끝이 날아가는 방향을 보게
            VfxPart p = Make("Shard", layer, UIFactory.TriangleSprite, color, start, angle, false);
            p.Velocity = dir * speed * Random.Range(0.5f, 1.2f);
            p.Gravity = -60f;
            p.Life = Random.Range(0.45f, 0.8f);
            float len = Random.Range(28f, 70f);
            p.StartSize = new Vector2(len * 0.25f, len * 0.4f);
            p.MidSize = new Vector2(len * 0.3f, len);
            p.EndSize = new Vector2(2f, len * 0.6f);
            p.MidTime = 0.2f;
            p.FadeFrom = 0.4f;
        }
    }

    // 큰 초승달 베기
    public static void Crescent(Transform layer, Vector2 pos, Color color, float size, float angle)
    {
        VfxPart glow = Make("CrescentGlow", layer, UIFactory.CrescentSprite, new Color(color.r, color.g, color.b, 0.8f), pos, angle, false);
        glow.Life = 0.45f; glow.MidTime = 0.25f; glow.FadeFrom = 0.25f;
        glow.StartSize = new Vector2(size * 0.4f, size * 0.4f);
        glow.MidSize = new Vector2(size, size);
        glow.EndSize = new Vector2(size * 1.15f, size * 1.15f);

        VfxPart core = Make("CrescentCore", layer, UIFactory.CrescentSprite, new Color(0.94f, 0.9f, 1f, 1f), pos, angle, false);   // 연보라빛 흰색
        core.Life = 0.3f; core.MidTime = 0.3f; core.FadeFrom = 0.2f;
        core.StartSize = new Vector2(size * 0.35f, size * 0.35f);
        core.MidSize = new Vector2(size * 0.85f, size * 0.85f);
        core.EndSize = new Vector2(size * 0.9f, size * 0.9f);
    }

    // 세로 빛기둥: 대상 위치에 하늘에서 꽂히는 붉은 빛
    public static void Pillar(Transform layer, Vector2 pos, Color color)
    {
        VfxPart outer = Make("Pillar", layer, UIFactory.BeamSprite, color, new Vector2(pos.x, pos.y + 300f), 0f, false);
        outer.Life = 0.55f; outer.MidTime = 0.15f; outer.FadeFrom = 0.3f;
        outer.StartSize = new Vector2(20f, 1600f); outer.MidSize = new Vector2(260f, 1600f); outer.EndSize = new Vector2(30f, 1600f);

        VfxPart core = Make("PillarCore", layer, UIFactory.BeamSprite, Color.white, new Vector2(pos.x, pos.y + 300f), 0f, false);
        core.Life = 0.4f; core.MidTime = 0.15f; core.FadeFrom = 0.25f;
        core.StartSize = new Vector2(8f, 1600f); core.MidSize = new Vector2(90f, 1600f); core.EndSize = new Vector2(6f, 1600f);
    }

    // 잔상: 그림을 붉게 물들인 복사본이 그 자리에 남았다가 사라짐
    public static void Ghost(Transform parent, Image source, Color tint, float life)
    {
        if (source == null) return;
        RectTransform rt = UIFactory.MakeRect("Ghost", parent);
        rt.SetAsFirstSibling();   // 캐릭터 뒤에
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = source.sprite;
        img.color = tint;
        img.raycastTarget = false;
        // 원본 그림의 화면 위치·크기를 그대로 복사
        RectTransform src = source.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        Vector3 center = src.TransformPoint(src.rect.center);
        Vector3 local = parent.InverseTransformPoint(center);
        rt.anchoredPosition = new Vector2(local.x, local.y);
        Vector2 size = new Vector2(src.rect.width * src.lossyScale.x / parent.lossyScale.x, src.rect.height * src.lossyScale.y / parent.lossyScale.y);
        VfxPart p = rt.gameObject.AddComponent<VfxPart>();
        p.Life = life; p.FadeFrom = 0f;
        p.StartSize = size; p.MidSize = size; p.EndSize = size * 1.04f;
    }

    // 퍼지는 고리
    public static void Ring(Transform layer, Vector2 pos, Color color)
    {
        VfxPart p = Make("Ring", layer, UIFactory.RingSprite, color, pos, 0f, false);
        p.Life = 0.45f; p.MidTime = 0.5f; p.FadeFrom = 0.2f;
        p.StartSize = new Vector2(40, 40); p.MidSize = new Vector2(200, 200); p.EndSize = new Vector2(260, 260);
    }

    // 위로 천천히 올라가는 반짝이
    public static void Rise(Transform layer, Vector2 pos, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 start = pos + new Vector2(Random.Range(-70f, 70f), Random.Range(-60f, 40f));
            VfxPart p = Make("Rise", layer, UIFactory.Circle, color, start, 0f, false);
            p.Velocity = new Vector2(0, Random.Range(90f, 180f));
            p.Life = Random.Range(0.6f, 0.9f);
            float size = Random.Range(8f, 14f);
            p.StartSize = new Vector2(2, 2);
            p.MidSize = new Vector2(size, size);
            p.EndSize = new Vector2(2, 2);
            p.MidTime = 0.3f;
            p.FadeFrom = 0.5f;
        }
    }

    // ==================== 3-184: 그림 이펙트 (Resources/Fx/) ====================
    // 그림 한 장을 코드로 움직임 (크기·투명도·회전). 그림이 없으면 아무것도 안 함
    //   그림 파일: Fx_MagicCircle, Fx_Lightning, Fx_IceBurst (제피르) / Fx_Crescent, Fx_XSlash (카인)
    //              Fx_Beam, Fx_Explosion (실비아) / Fx_FireRing, Fx_Phoenix (이그니스) / Fx_Shield (방어) / Fx_Smoke (적 처치)
    static readonly System.Collections.Generic.Dictionary<string, Sprite> fxCache = new System.Collections.Generic.Dictionary<string, Sprite>();
    public static Sprite FxSprite(string name)
    {
        Sprite s;
        if (!fxCache.TryGetValue(name, out s)) { s = Resources.Load<Sprite>("Fx/" + name); fxCache[name] = s; }
        return s;
    }

    // 그림 이펙트 하나. height = 화면 키 (가로는 그림 비율대로). 반환값 = 조각 (없으면 null)
    public static VfxPart Art(Transform layer, string name, Vector2 pos, float height, float angle, float life,
                              float startScale, float midScale, float endScale, float midTime = 0.25f, float fadeFrom = 0.55f)
    {
        // 3-186: "이름2" 그림이 있으면 반반 확률로 그쪽 (같은 이펙트도 매번 조금 다르게 보이게)
        Sprite v2 = FxSprite(name + "2");
        if (v2 != null && Random.value < 0.5f) name = name + "2";
        Sprite s = FxSprite(name);
        if (s == null) return null;
        float w = height * s.rect.width / s.rect.height;
        VfxPart p = Make(name, layer, s, Color.white, pos, angle, false);
        p.Life = life; p.MidTime = midTime; p.FadeFrom = fadeFrom; p.FadeIn = 0.08f;
        p.StartSize = new Vector2(w, height) * startScale;
        p.MidSize = new Vector2(w, height) * midScale;
        p.EndSize = new Vector2(w, height) * endScale;
        return p;
    }

    // 제피르 번개: 하늘에 마법진(돌면서 커짐) → 0.15초 뒤 번개가 대상 위로 내리꽂힘
    public static void ZephyrLightning(Transform layer, Vector2 target)
    {
        VfxPart circle = Art(layer, "Fx_MagicCircle", target + new Vector2(0, 330), 190f, 0f, 0.75f, 0.3f, 1f, 1.05f, 0.3f, 0.6f);
        if (circle != null) circle.Spin = -160f;   // 시계 방향으로 돌며 커짐
        VfxPart bolt = Art(layer, "Fx_Lightning", target + new Vector2(0, 150), 470f, 0f, 0.4f, 0.9f, 1f, 1f, 0.2f, 0.45f);
        if (bolt != null) bolt.Delay = 0.15f;
    }

    // 제피르 얼음: 대상 자리에서 얼음 결정이 솟아오름
    public static void ZephyrIce(Transform layer, Vector2 target)
    {
        Art(layer, "Fx_IceBurst", target + new Vector2(0, -20), 250f, 0f, 0.55f, 0.4f, 1.05f, 1.1f, 0.25f, 0.5f);
    }

    // 카인 베기: 초승달 (보통) / X자 (필살기·처형)
    public static void KainSlash(Transform layer, Vector2 target, bool big)
    {
        if (big) Art(layer, "Fx_XSlash", target, 300f, 0f, 0.45f, 0.5f, 1.1f, 1.2f, 0.2f, 0.45f);
        else Art(layer, "Fx_Crescent", target, 250f, Random.Range(-20f, 20f), 0.38f, 0.7f, 1.05f, 1.1f, 0.2f, 0.4f);
    }

    // 실비아 사격: 총구에서 대상까지 빛줄기 + 맞은 자리 폭발
    public static void SylviaShot(Transform layer, Vector2 muzzle, Vector2 target)
    {
        Sprite s = FxSprite("Fx_Beam");
        if (s != null)
        {
            Vector2 d = target - muzzle;
            float len = d.magnitude;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            VfxPart beam = Make("Fx_Beam", layer, s, Color.white, muzzle + d * 0.5f, ang, false);
            float h = 70f;
            beam.Life = 0.28f; beam.MidTime = 0.25f; beam.FadeFrom = 0.4f;
            beam.StartSize = new Vector2(len * 0.6f, h * 0.5f);
            beam.MidSize = new Vector2(len, h);
            beam.EndSize = new Vector2(len, h * 0.3f);
        }
        VfxPart boom = Art(layer, "Fx_Explosion", target, 230f, Random.Range(0f, 360f), 0.5f, 0.3f, 1f, 1.15f, 0.25f, 0.45f);
        if (boom != null) boom.Delay = 0.07f;
    }

    // 이그니스: 불새가 앞으로 날아가 대상에 부딪힘
    public static void IgnisPhoenix(Transform layer, Vector2 from, Vector2 target)
    {
        VfxPart p = Art(layer, "Fx_Phoenix", from, 220f, 0f, 0.45f, 0.6f, 1f, 1.1f, 0.3f, 0.6f);
        if (p != null) p.Velocity = (target - from) / 0.4f;
        VfxPart burst = Art(layer, "Fx_Explosion", target, 180f, Random.Range(0f, 360f), 0.4f, 0.3f, 0.9f, 1f, 0.25f, 0.4f);
        if (burst != null) burst.Delay = 0.32f;
    }

    // 방어도 얻음: 푸른 방패 구 (이그니스는 불꽃 고리)
    public static void Guard(Transform layer, Vector2 body, bool fire, float unitHeight)
    {
        if (fire) Art(layer, "Fx_FireRing", body, unitHeight * 0.95f, 0f, 0.6f, 0.6f, 1f, 1.05f, 0.25f, 0.5f);
        else Art(layer, "Fx_Shield", body, unitHeight * 0.85f, 0f, 0.6f, 0.7f, 1f, 1.03f, 0.25f, 0.5f);
    }

    // 적 처치: 보라·검은 연기가 피어오르며 흩어짐
    public static void DeathSmoke(Transform layer, Vector2 body, float unitHeight)
    {
        VfxPart p = Art(layer, "Fx_Smoke", body, unitHeight * 0.9f, 0f, 0.9f, 0.5f, 1f, 1.25f, 0.3f, 0.35f);
        if (p != null) p.Velocity = new Vector2(0, 50f);
    }

    // ==================== 3-186: 프레임 이펙트 · 상태 이펙트 · 몬스터 공격 ====================
    static Sprite[] burstFrames;
    // 빛 터짐 5장 (Fx_Burst_0~4) 을 차례로. size = 화면 크기
    public static void Burst(Transform layer, Vector2 pos, float size, Color tint)
    {
        if (burstFrames == null)
        {
            System.Collections.Generic.List<Sprite> list = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < 10; i++) { Sprite f = FxSprite("Fx_Burst_" + i); if (f == null) break; list.Add(f); }
            burstFrames = list.ToArray();
        }
        if (burstFrames.Length == 0) return;
        RectTransform rt = UIFactory.MakeRect("Fx_Burst", layer);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = tint;
        img.raycastTarget = false;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        rt.localEulerAngles = new Vector3(0, 0, Random.Range(0f, 360f));
        VfxFrames f2 = rt.gameObject.AddComponent<VfxFrames>();
        f2.Frames = burstFrames;
        f2.Fps = 20f;
    }

    // 상태 이펙트: 캐릭터 몸 위에 작게 피어올랐다 사라짐
    //   Fx_Heal 회복 / Fx_PowerUp 힘 / Fx_Debuff 약화·취약·손상 / Fx_Burn 발화 / Fx_Freeze 동결·동상
    public static void Status(Transform layer, string name, Vector2 body, float unitHeight)
    {
        VfxPart p = Art(layer, name, body, Mathf.Clamp(unitHeight * 0.55f, 120f, 210f), 0f, 0.8f, 0.6f, 1f, 1.05f, 0.25f, 0.55f);
        if (p != null) p.Velocity = new Vector2(0, 40f);
    }

    // 몬스터 공격 종류 (몬스터 그림 이름으로 정함)
    public enum EnemyStyle { Impact, Arrow, Claw, Dark, Poison, Fire, Cosmic, RedSlash, Necro, SunBeam, MoonBeam, Roar }
    public static EnemyStyle StyleOf(string art)
    {
        switch (art)
        {
            // 3-188 보스 전용
            case "Architect": return EnemyStyle.Cosmic;                 // 설계자: 우주 균열 / 사슬 (반반)
            case "FortressLord": return EnemyStyle.RedSlash;            // 요새의 군주: 붉은 검기
            case "ForgottenKing": return EnemyStyle.Necro;              // 잊혀진 왕의 망령: 강령술 마법진
            case "SunStatue": return EnemyStyle.SunBeam;                // 태양의 석상: 금빛 광선
            case "MoonStatue": return EnemyStyle.MoonBeam;              // 달의 석상: 은빛 광선
            case "Werewolf": case "Orc": case "ZombieKing": case "Gatekeeper":
                return EnemyStyle.Roar;                                 // 수문장들: 포효 충격파 + 둔기
            case "SkeletonArcher": case "OrcSlinger": case "GoblinSpear": case "GoblinNet":
                return EnemyStyle.Arrow;
            case "Rat": case "Beast": case "Spider": case "Bat": case "HornBeetle": case "Imp": case "HoundHandler": case "Assassin":
                return EnemyStyle.Claw;
            case "Shaman": case "SoulWitch": case "Cultist": case "ApprenticeHex": case "RiftWatcher":
            case "Watcher": case "EliteWatcher": case "SquadWatcher": case "LanternKeeper":
                return EnemyStyle.Dark;
            case "Sporeling": case "Slime": case "Larva": case "ClaySplitter": case "ClaySmall": case "AbyssShell":
                return EnemyStyle.Poison;
            case "EmberSpirit":
                return EnemyStyle.Fire;
            default:
                return EnemyStyle.Impact;   // 고블린 대장·골렘·오크·수문장·해골 방패병·근위병 등 = 둔기 충격
        }
    }

    // 몬스터 공격이 플레이어에게 맞은 자리 (body = 몸 가운데, feet = 발밑)
    public static void EnemyHit(Transform layer, EnemyStyle style, Vector2 body, Vector2 feet, float unitHeight)
    {
        switch (style)
        {
            case EnemyStyle.Arrow:
                VfxPart a = Art(layer, "Fx_Arrow", body + new Vector2(60f, 10f), 120f, 0f, 0.3f, 0.9f, 1f, 1f, 0.2f, 0.5f);
                if (a != null) a.Velocity = new Vector2(-260f, -60f);   // 오른쪽 위 → 왼쪽 아래로 꽂힘
                break;
            case EnemyStyle.Claw:
                Art(layer, "Fx_Claw", body, unitHeight * 0.55f, 0f, 0.35f, 0.7f, 1.05f, 1.1f, 0.2f, 0.45f);
                break;
            case EnemyStyle.Dark:
                Art(layer, "Fx_DarkOrb", body, unitHeight * 0.6f, Random.Range(0f, 360f), 0.5f, 0.3f, 1f, 1.15f, 0.25f, 0.45f);
                break;
            case EnemyStyle.Poison:
                Art(layer, "Fx_Poison", feet + new Vector2(0, unitHeight * 0.18f), unitHeight * 0.45f, 0f, 0.6f, 0.5f, 1f, 1.05f, 0.25f, 0.5f);
                break;
            case EnemyStyle.Fire:
                Art(layer, "Fx_Burn", body, unitHeight * 0.5f, 0f, 0.55f, 0.5f, 1f, 1.1f, 0.25f, 0.5f);
                break;
            case EnemyStyle.Cosmic:
                if (Random.value < 0.5f) Art(layer, "Fx_CosmicRift", body + new Vector2(120f, 0f), unitHeight * 0.55f, 0f, 0.5f, 0.6f, 1f, 1.05f, 0.25f, 0.5f);
                else Art(layer, "Fx_Chains", body + new Vector2(140f, 0f), unitHeight * 0.5f, 0f, 0.5f, 0.6f, 1f, 1.05f, 0.25f, 0.5f);
                break;
            case EnemyStyle.RedSlash:
                Art(layer, "Fx_RedSlash", body + new Vector2(80f, 0f), unitHeight * 0.6f, 0f, 0.4f, 0.7f, 1.05f, 1.1f, 0.2f, 0.45f);
                break;
            case EnemyStyle.Necro:
                Art(layer, "Fx_Necro", feet + new Vector2(0, unitHeight * 0.12f), unitHeight * 0.55f, 0f, 0.7f, 0.4f, 1f, 1.05f, 0.25f, 0.55f);
                break;
            case EnemyStyle.SunBeam:
                Art(layer, "Fx_SunBeam", body + new Vector2(70f, 80f), unitHeight * 0.9f, 0f, 0.5f, 0.8f, 1f, 1f, 0.2f, 0.5f);
                break;
            case EnemyStyle.MoonBeam:
                Art(layer, "Fx_MoonBeam", body + new Vector2(70f, 80f), unitHeight * 0.9f, 0f, 0.5f, 0.8f, 1f, 1f, 0.2f, 0.5f);
                break;
            case EnemyStyle.Roar:
                Art(layer, "Fx_Roar", body + new Vector2(110f, 10f), unitHeight * 0.5f, 0f, 0.4f, 0.6f, 1f, 1.1f, 0.2f, 0.45f);
                Art(layer, "Fx_Impact", feet + new Vector2(0, unitHeight * 0.2f), unitHeight * 0.55f, 0f, 0.45f, 0.5f, 1f, 1.1f, 0.2f, 0.45f);
                break;
            default:
                Art(layer, "Fx_Impact", feet + new Vector2(0, unitHeight * 0.2f), unitHeight * 0.6f, 0f, 0.45f, 0.5f, 1f, 1.1f, 0.2f, 0.45f);
                break;
        }
    }

    // ==================== 3-188: 필살기 카드 · 보스 등장 · 부활 · 턴 시작 · 카드 뽑기 ====================
    // 크게 한 번 나타났다 천천히 사라지는 큰 이펙트 (화면 가운데 연출용)
    public static VfxPart Big(Transform layer, string name, Vector2 pos, float height, float life = 1.0f, float spin = 0f)
    {
        VfxPart p = Art(layer, name, pos, height, 0f, life, 0.5f, 1f, 1.08f, 0.25f, 0.6f);
        if (p != null) p.Spin = spin;
        return p;
    }

    // 3-197: 날아가는 그림 (화염의 창·피닉스 피어스·플라즈마 볼트·얼음 송곳·관통탄·난사·그림자 습격)
    //   그림은 모두 "오른쪽을 보는" 모양이라, from → to 방향으로 돌려서(angle) flight 초 동안 날려 보냄
    //   자바로 치면: 방향 벡터 d 의 각도 = Math.toDegrees(Math.atan2(d.y, d.x)) 와 같은 계산
    public static VfxPart Fly(Transform layer, string name, Vector2 from, Vector2 to, float height, float flight = 0.3f)
    {
        Vector2 d = to - from;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        VfxPart p = Art(layer, name, from, height, ang, flight + 0.15f, 0.8f, 1f, 1.05f, 0.2f, 0.7f);
        if (p != null) p.Velocity = d / flight;   // 1초에 움직이는 거리 = 전체 거리 ÷ 걸리는 시간
        return p;
    }

    // 불타는 유성: 왼쪽 위에서 대상 쪽으로 날아와 폭발
    public static void Meteor(Transform layer, Vector2 target)
    {
        Vector2 from = target + new Vector2(-420f, 360f);
        VfxPart m = Art(layer, "Fx_Meteor", from, 200f, -25f, 0.42f, 0.9f, 1f, 1f, 0.3f, 0.85f);
        if (m != null) m.Velocity = (target - from) / 0.4f;
        VfxPart b = Art(layer, "Fx_Explosion", target, 300f, Random.Range(0f, 360f), 0.6f, 0.3f, 1f, 1.15f, 0.25f, 0.45f);
        if (b != null) b.Delay = 0.38f;
    }

    // 조준경: 크게 나타나 대상 위로 좁혀 들어옴
    public static void Crosshair(Transform layer, Vector2 target)
    {
        Art(layer, "Fx_Crosshair", target, 200f, 0f, 0.55f, 2.0f, 1f, 0.9f, 0.45f, 0.75f);
    }
}

