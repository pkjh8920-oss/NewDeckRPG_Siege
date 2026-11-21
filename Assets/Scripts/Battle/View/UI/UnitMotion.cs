// ============================================================
// UnitMotion.cs
// 캐릭터·몬스터 그림을 "살아 있는 것처럼" 움직이는 부품입니다.
//
//   숨쉬기 : 가만히 있어도 위아래로 살짝 늘었다 줄었다 (발은 고정)
//   돌진   : Lunge()  → 공격할 때 앞으로 튀어나갔다가 돌아옴
//   피격   : Hit()    → 맞으면 뒤로 밀리면서 흔들리고 번쩍임
//   쓰러짐 : Dead = true → 숨쉬기를 멈추고 납작해짐
//   망토   : Cape 가 있으면 목을 중심으로 천천히 펄럭이고, 돌진할 때 뒤로 날리고, 맞으면 파르르 떨림
//   공격 자세: Body 안에 "AttackPose" 그림이 있으면 휘두르는 순간에만 그 그림으로 바꿈
//   필살기 자세: "SpecialPose" 그림이 있으면 필살기 내려찍는 순간에는 그 그림 (없으면 공격 자세)
//   필살기 : PlaySpecial() → 낫을 크게 뒤로 치켜들었다가(0.35초) 앞으로 내려찍음
//   카드 자세: "HarvestPose"(영혼 수확), "AmbushPose"(기습·월광섬 발동), "CursePose"(적에게 거는 스킬),
//             "NightPose"(죽음의 밤) 그림이 있으면 그 카드를 쓸 때만 보여 줌
//   밤 기운: NightAura = true 면 (죽음의 밤 동안) 뒤쪽 빛이 붉게 일렁임 → 어떤 자세든 붉은 기운이 함께 보임
//   무기   : Weapon 이 있으면 대기 중엔 살짝 흔들리고, 돌진할 때 뒤로 젖혔다가 앞으로 휘두름
//
// 그림이 들어 있는 "Body" 상자에 붙입니다. Body 의 기준점(pivot)이 발밑이라서
// 크기를 바꿔도 발은 바닥에 붙어 있습니다.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UnitMotion : MonoBehaviour
{
    public float Facing = 1f;     // 바라보는 방향: 플레이어 +1(오른쪽), 몬스터 -1(왼쪽)
    public bool Dead;
    public RectTransform Weapon;  // 따로 붙인 무기 (없으면 null)
    public RectTransform Cape;    // 따로 붙인 망토 (없으면 null)
    private float weaponBaseAngle = float.NaN;

    private RectTransform rt;
    private float phase;          // 숨쉬기 시작 시점을 캐릭터마다 다르게 (다 같이 움직이면 어색함)

    private float specialTimer;   // 필살기 동작 남은 시간
    public const float SpecialTime = 0.9f;
    public const float SpecialImpact = 0.42f;   // 내려찍는 순간 (시작부터 몇 초)
    private float lungeTimer;     // 돌진 남은 시간
    private const float LungeTime = 0.32f;
    private float hitTimer;       // 피격 흔들림 남은 시간
    private const float HitTime = 0.35f;
    private float hitPower;

    // 번쩍임: 그림 조각(Image)들의 원래 색을 기억했다가 잠깐 다른 색으로
    private List<Image> parts;
    private List<Color> originalColors;
    private float flashTimer;
    private const float FlashTime = 0.22f;
    private Color flashColor = Color.white;

    private float deadAmount;     // 0 = 살아 있음, 1 = 완전히 쓰러짐

    // 공격 자세 바꿔 끼우기용 (Body 안에서 이름으로 찾음)
    private Image attackPose;
    private Image specialPose;
    private Image chargePose;
    private Image defendPose;
    private Image harvestPose;            // 영혼 수확 자세
    private Image ambushPose;             // 기습 자세
    private Image cursePose;              // 저주 자세 (죽음의 인장 등)
    private Image nightPose;              // 죽음의 밤 자세
    private Image lightningPose;          // 마도사 전기 공격 자세 (9)
    private Image icePose;                // 마도사 얼음 공격 자세 (10)
    private Image throwPose;              // 스나이퍼 투척 자세 (11) - 3-74
    private Image absZeroPose;            // 마도사 절대영도 자세 (12) - 3-75
    private Image lightningFallPose;      // 마도사 낙뢰 자세 (13) - 3-75
    private Image manaChargePose;         // 마도사 마력 집중 자세 (14) - 3-77
    private Image bayonetPose;            // 스나이퍼 총검술 자세 (15) - 3-90
    private int lungePose;                // 이번 돌진에 쓸 자세 번호 (0 = 보통: 기습이면 6, 아니면 1)
    private float cardPoseTimer;          // 카드 자세 남은 시간
    private int cardPoseKind;             // 5 = 영혼 수확, 7 = 저주, 8 = 죽음의 밤
    public const float CardPoseTime = 0.8f;
    private int specialPoseKind = 2;      // 필살기 때 쓸 자세: 2 = 내려찍기, 3 = 차징(적월), 6 = 기습(월광섬)
    public bool NightAura;                // 죽음의 밤 동안 true (화면이 매번 맞춰 줌)
    private Image backLight;
    private Color backLightBase;
    private bool ambushLunge;             // 이번 돌진이 기습이면 true
    private float defendTimer;            // 수비 자세 남은 시간
    private const float DefendTime = 0.6f;
    public bool Charging;                 // true 면 차징 자세 (화면이 매번 맞춰 줌)
    public bool Guarding;                 // true 면 수비 자세를 계속 (방어도가 있는 동안, 화면이 매번 맞춰 줌)
    private Image bodySprite;
    private Image capeImage;
    private Image weaponImage;
    private bool posesFound;

    void Awake()
    {
        rt = (RectTransform)transform;
        phase = Random.Range(0f, 10f);
    }

    void FindPoses()
    {
        posesFound = true;
        Transform t;
        t = transform.Find("AttackPose"); if (t != null) attackPose = t.GetComponent<Image>();
        t = transform.Find("SpecialPose"); if (t != null) specialPose = t.GetComponent<Image>();
        t = transform.Find("ChargePose"); if (t != null) chargePose = t.GetComponent<Image>();
        t = transform.Find("DefendPose"); if (t != null) defendPose = t.GetComponent<Image>();
        t = transform.Find("HarvestPose"); if (t != null) harvestPose = t.GetComponent<Image>();
        t = transform.Find("AmbushPose"); if (t != null) ambushPose = t.GetComponent<Image>();
        t = transform.Find("CursePose"); if (t != null) cursePose = t.GetComponent<Image>();
        t = transform.Find("NightPose"); if (t != null) nightPose = t.GetComponent<Image>();
        t = transform.Find("LightningPose"); if (t != null) lightningPose = t.GetComponent<Image>();
        t = transform.Find("IcePose"); if (t != null) icePose = t.GetComponent<Image>();
        t = transform.Find("ThrowPose"); if (t != null) throwPose = t.GetComponent<Image>();
        t = transform.Find("AbsoluteZeroPose"); if (t != null) absZeroPose = t.GetComponent<Image>();
        t = transform.Find("LightningFallPose"); if (t != null) lightningFallPose = t.GetComponent<Image>();
        t = transform.Find("ManaChargePose"); if (t != null) manaChargePose = t.GetComponent<Image>();
        t = transform.Find("BayonetPose"); if (t != null) bayonetPose = t.GetComponent<Image>();
        t = transform.Find("BackLight"); if (t != null) { backLight = t.GetComponent<Image>(); backLightBase = backLight.color; }
        t = transform.Find("Sprite"); if (t != null) bodySprite = t.GetComponent<Image>();
        t = transform.Find("Cape"); if (t != null) capeImage = t.GetComponent<Image>();
        if (Weapon != null) weaponImage = Weapon.GetComponent<Image>();
    }

    // 공격 자세를 켜고 끔 (켜면 평소 몸·망토·무기를 숨김)
    // which: 0 = 평소 몸, 1 = 공격, 2 = 필살기 (없으면 공격), 3 = 차징, 4 = 수비 (없으면 평소)
    //        5 = 영혼 수확, 7 = 저주, 8 = 죽음의 밤 (없으면 평소), 6 = 기습 (없으면 공격)
    void ShowPose(int which)
    {
        if (!posesFound) FindPoses();
        if (which == 6 && ambushPose == null) which = 1;
        if (which == 5 && harvestPose == null) which = 0;
        if (which == 7 && cursePose == null) which = 0;
        if (which == 8 && nightPose == null) which = 0;
        if (which == 9 && lightningPose == null) which = 1;   // 그림이 없으면 공격 자세 (그것도 없으면 평소)
        if (which == 10 && icePose == null) which = 1;
        if (which == 11 && throwPose == null) which = 0;   // 투척 그림이 없으면 평소 자세
        if (which == 12 && absZeroPose == null) which = 10;        // 절대영도 그림이 없으면 보통 얼음 자세 (그것도 없으면 아래에서 공격 → 평소)
        if (which == 13 && lightningFallPose == null) which = 9;   // 낙뢰 그림이 없으면 보통 전기 자세
        if (which == 14 && manaChargePose == null) which = 0;      // 마력 집중 그림이 없으면 평소 자세
        if (which == 15 && bayonetPose == null) which = 1;         // 총검술 그림이 없으면 보통 공격 자세
        if (which == 9 && lightningPose == null) which = 1;
        if (which == 10 && icePose == null) which = 1;
        if (which == 2 && specialPose == null) which = 1;
        if (which == 1 && attackPose == null) which = 0;
        if (which == 3 && chargePose == null) which = 0;
        if (which == 4 && defendPose == null) which = 0;
        SetOn(attackPose, which == 1);
        SetOn(specialPose, which == 2);
        SetOn(chargePose, which == 3);
        SetOn(defendPose, which == 4);
        SetOn(harvestPose, which == 5);
        SetOn(ambushPose, which == 6);
        SetOn(cursePose, which == 7);
        SetOn(nightPose, which == 8);
        SetOn(lightningPose, which == 9);
        SetOn(icePose, which == 10);
        SetOn(throwPose, which == 11);
        SetOn(absZeroPose, which == 12);
        SetOn(lightningFallPose, which == 13);
        SetOn(manaChargePose, which == 14);
        SetOn(bayonetPose, which == 15);
        bool bodyOn = which == 0;
        if (bodySprite != null && bodySprite.enabled != bodyOn) bodySprite.enabled = bodyOn;
        if (capeImage != null) capeImage.enabled = bodyOn;
        if (weaponImage != null) weaponImage.enabled = bodyOn;
    }

    static void SetOn(Image img, bool on)
    {
        if (img != null && img.enabled != on) img.enabled = on;
    }

    // 수비 자세를 잠깐 보여 줌
    public void Defend()
    {
        defendTimer = DefendTime;
    }

    // poseKind: 내려찍는 순간 보여 줄 자세 (2 = 내려찍기, 3 = 차징, 6 = 기습)
    public void PlaySpecial(int poseKind = 2)
    {
        specialPoseKind = poseKind;
        specialTimer = SpecialTime;
        lungeTimer = 0f;
    }

    // ambush = true 면 기습: 더 멀리, 낮게 파고들고 기습 자세를 씀
    // pose: 돌진하는 동안 보여 줄 자세 번호 (3-90). 0 이면 예전처럼 (기습 = 6, 보통 = 1). 총검술 = 15
    public void Lunge(bool ambush = false, int pose = 0)
    {
        lungeTimer = LungeTime;
        ambushLunge = ambush;
        lungePose = pose;
    }

    // 스나이퍼 사격 (3-90): 돌진 없이 제자리에서 조준 자세(공격 자세 그림)를 잠깐
    public void Shoot() { cardPoseKind = 1; cardPoseTimer = CardPoseTime; }

    // 카드 자세를 잠깐 보여 줌 (영혼 수확 / 저주 / 죽음의 밤)
    public void Harvest() { cardPoseKind = 5; cardPoseTimer = CardPoseTime; }
    public void Curse()   { cardPoseKind = 7; cardPoseTimer = CardPoseTime; }
    public void NightCast() { cardPoseKind = 8; cardPoseTimer = CardPoseTime + 0.3f; }
    // 마도사 (2026-11-15): 전기 공격 / 얼음 공격 자세를 잠깐 (돌진 없이 제자리에서 마법)
    public void CastLightning() { cardPoseKind = 9; cardPoseTimer = CardPoseTime + 0.2f; }
    public void CastIce() { cardPoseKind = 10; cardPoseTimer = CardPoseTime + 0.2f; }
    // 스나이퍼 (3-74): 연막탄·사슬 함정을 던지는 자세를 잠깐 (돌진 없이 제자리)
    public void Throw() { cardPoseKind = 11; cardPoseTimer = CardPoseTime + 0.2f; }
    // 마도사 전용 큰 마법 (3-75): 절대영도 / 낙뢰. 보통 마법보다 조금 더 오래 보여 줌
    public void CastAbsoluteZero() { cardPoseKind = 12; cardPoseTimer = CardPoseTime + 0.45f; }
    public void CastLightningFall() { cardPoseKind = 13; cardPoseTimer = CardPoseTime + 0.45f; }
    // 마도사 마력 집중 (3-77): 마력 증폭·마력 재구성·마력해방
    public void ManaCharge() { cardPoseKind = 14; cardPoseTimer = CardPoseTime + 0.3f; }

    // power: 흔들림 세기 (큰 피해일수록 크게)
    public void Hit(float power, Color flash)
    {
        hitTimer = HitTime;
        hitPower = power;
        flashColor = flash;
        flashTimer = FlashTime;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Vector2 offset = Vector2.zero;

        // ---- 돌진: 0.1초 동안 앞으로 70 → 0.22초 동안 원래 자리로 ----
        if (lungeTimer > 0f)
        {
            lungeTimer -= dt;
            float t = 1f - Mathf.Max(0f, lungeTimer) / LungeTime;   // 0 → 1
            float k = (t < 0.3f) ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
            k = Mathf.SmoothStep(0f, 1f, k);
            offset.x += Facing * (ambushLunge ? 120f : 70f) * k;   // 기습은 더 멀리 파고듦
        }

        // ---- 피격: 뒤로 밀리면서 좌우로 흔들림 (점점 약해짐) ----
        if (hitTimer > 0f)
        {
            hitTimer -= dt;
            float k = Mathf.Max(0f, hitTimer) / HitTime;            // 1 → 0
            offset.x += -Facing * 18f * hitPower * k;                // 뒤로 밀림
            offset.x += Mathf.Sin(hitTimer * 70f) * 10f * hitPower * k;
        }
        // ---- 필살기: 살짝 뒤로 물러나며 웅크림 → 앞으로 크게 도약 → 돌아옴 ----
        float specialSwing = 0f;
        if (specialTimer > 0f)
        {
            specialTimer -= dt;
            float st = SpecialTime - Mathf.Max(0f, specialTimer);   // 지난 시간 (초)
            float imp = SpecialImpact;
            if (st < imp)
            {
                float k = Mathf.SmoothStep(0f, 1f, st / imp);
                offset.x += -Facing * 25f * k;              // 뒤로 물러남
                offset.y += 6f * k;
                specialSwing = 60f * k;                      // 낫을 뒤로 크게 치켜듦
            }
            else if (st < imp + 0.1f)
            {
                float k = (st - imp) / 0.1f;
                offset.x += Facing * Mathf.Lerp(-25f, 110f, k); // 순식간에 앞으로
                specialSwing = Mathf.Lerp(60f, -95f, k);     // 내려찍기
            }
            else
            {
                float k = Mathf.SmoothStep(0f, 1f, (st - imp - 0.1f) / (SpecialTime - imp - 0.1f));
                offset.x += Facing * Mathf.Lerp(110f, 0f, k);
                specialSwing = Mathf.Lerp(-95f, 0f, k);
            }
        }

        rt.anchoredPosition = offset;

        // ---- 공격 자세: 돌진의 앞부분(0.08~0.75), 필살기의 내려찍기 순간(0.4초) ----
        int pose = 0;
        if (lungeTimer > 0f)
        {
            float lt = 1f - lungeTimer / LungeTime;
            if (lt > 0.08f && lt < 0.75f) pose = lungePose > 0 ? lungePose : (ambushLunge ? 6 : 1);
        }
        if (specialTimer > 0f)
        {
            float st = SpecialTime - specialTimer;
            pose = (st >= SpecialImpact - 0.02f && st < SpecialImpact + 0.4f) ? specialPoseKind : 0;
        }
        // 공격·필살기가 아닐 때: 수비(방어도가 있는 동안) > 차징(계속) > 평소
        if (defendTimer > 0f) defendTimer -= dt;
        if (cardPoseTimer > 0f) cardPoseTimer -= dt;
        if (pose == 0 && cardPoseTimer > 0f) pose = cardPoseKind;   // 카드 자세는 수비보다 먼저
        if (pose == 0 && (defendTimer > 0f || Guarding)) pose = 4;   // 수비: 방어도를 얻은 순간 + 방어도가 남아 있는 동안
        if (pose == 0 && Charging) pose = 3;
        if (pose == 0 && NightAura && nightPose != null) pose = 8;   // 2026-11-15: 밤이 켜져 있는 동안 밤 자세 (원혼을 부르는 모습)
        ShowPose(Dead ? 0 : pose);

        // ---- 밤 기운: 죽음의 밤 동안 뒤쪽 빛이 붉게 일렁임 (어떤 자세든 같이 보임) ----
        if (backLight != null)
        {
            Color target = backLightBase;
            if (NightAura && !Dead)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f + phase);
                target = new Color(0.85f, 0.08f, 0.2f, 0.32f + 0.18f * pulse);   // 붉은 기운
            }
            backLight.color = Color.Lerp(backLight.color, target, dt * 6f);
        }

        // ---- 망토: 펄럭임 + 돌진하면 뒤로 날림 + 맞으면 떨림 ----
        if (Cape != null)
        {
            float t = Time.time;
            // 서로 다른 박자의 사인 두 개를 섞으면 바람처럼 불규칙해 보임
            float flap = Mathf.Sin(t * 1.3f + phase) * 2.5f + Mathf.Sin(t * 2.9f + phase * 2f) * 1f;
            if (lungeTimer > 0f)
            {
                float lt = 1f - Mathf.Max(0f, lungeTimer) / LungeTime;   // 0 → 1
                float k = (lt < 0.3f) ? lt / 0.3f : 1f - (lt - 0.3f) / 0.7f;
                flap += -11f * Mathf.SmoothStep(0f, 1f, k);              // 몸이 앞으로 가면 망토는 뒤로
            }
            if (hitTimer > 0f)
            {
                float hk = Mathf.Max(0f, hitTimer) / HitTime;
                flap += Mathf.Sin(hitTimer * 45f) * 4f * hk;             // 맞으면 파르르
            }
            if (specialTimer > 0f) flap -= Mathf.Abs(specialSwing) * 0.12f;   // 필살기: 망토가 크게 날림
            Cape.localEulerAngles = new Vector3(0, 0, flap * Facing * (1f - deadAmount));
            float stretch = 1f + Mathf.Sin(t * 1.3f + phase + 1f) * 0.015f;   // 살짝 늘었다 줄었다
            Cape.localScale = new Vector3(1f, stretch, 1f);
        }

        // ---- 무기: 대기 흔들림 + 휘두르기 ----
        if (Weapon != null)
        {
            if (float.IsNaN(weaponBaseAngle)) weaponBaseAngle = Weapon.localEulerAngles.z;
            float swing = Mathf.Sin(Time.time * 1.6f + phase) * 2.5f;   // 대기: ±2.5도
            if (lungeTimer > 0f)
            {
                float t = 1f - Mathf.Max(0f, lungeTimer) / LungeTime;   // 0 → 1
                // 0~0.25: 뒤로 젖힘(+25도) → 0.25~0.45: 앞으로 크게(-65도) → 그 뒤 천천히 제자리
                if (t < 0.25f) swing += Mathf.Lerp(0f, 25f, t / 0.25f);
                else if (t < 0.45f) swing += Mathf.Lerp(25f, -65f, (t - 0.25f) / 0.2f);
                else swing += Mathf.Lerp(-65f, 0f, Mathf.SmoothStep(0f, 1f, (t - 0.45f) / 0.55f));
            }
            swing += specialSwing;
            Weapon.localEulerAngles = new Vector3(0, 0, weaponBaseAngle + swing * Facing);
        }

        // ---- 숨쉬기 / 쓰러짐 ----
        deadAmount = Mathf.MoveTowards(deadAmount, Dead ? 1f : 0f, dt * 3f);
        float s = Mathf.Sin(Time.time * 2.2f + phase) * (1f - deadAmount);
        float sx = 1f - 0.012f * s + 0.15f * deadAmount;
        float sy = 1f + 0.025f * s - 0.35f * deadAmount;
        rt.localScale = new Vector3(sx, sy, 1f);

        // ---- 번쩍임 ----
        if (flashTimer > 0f)
        {
            if (parts == null) CollectParts();
            flashTimer -= dt;
            float k = Mathf.Max(0f, flashTimer) / FlashTime;        // 1 → 0
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == null) continue;
                Color o = originalColors[i];
                Color f = new Color(flashColor.r, flashColor.g, flashColor.b, o.a);
                parts[i].color = Color.Lerp(o, f, k * 0.85f);
            }
        }
    }

    void CollectParts()
    {
        parts = new List<Image>(GetComponentsInChildren<Image>());
        parts.RemoveAll(img => img.name == "BackLight");   // 뒤쪽 빛은 번쩍이지 않게
        originalColors = new List<Color>();
        foreach (Image img in parts) originalColors.Add(img.color);
    }
}
