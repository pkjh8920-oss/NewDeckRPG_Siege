// ============================================================
// UnitArt.cs
// 캐릭터와 몬스터의 "그림"을 만드는 곳입니다.
//
// 1) Assets/Resources/Art/ 폴더에 그림 파일이 있으면 그 그림을 씀
//    예) Assets/Resources/Art/BlackScythe.png → 검은 낫
// 2) 그림이 없으면 도형으로 임시 그림을 만듦 (슬라임 = 초록 덩어리 등)
//
// 나중에 진짜 그림이 생기면 Resources/Art 폴더에 같은 이름으로 넣기만 하면 됩니다.
//
// 그림 종류는 크기로 자동 구분
//   작은 그림(높이 160 이하) = 픽셀아트 → 정수 배로 키움 (픽셀이 안 뭉개지게)
//   큰 그림                  = 손그림   → 화면 높이 PaintedHeight 에 맞게 부드럽게 줄임
//
// 망토 그림: "이름_Cape" 파일이 있으면 몸 뒤에 망토를 따로 붙임 (목을 중심으로 흔들림)
// 공격 자세: "이름_Attack" 파일이 있으면 공격하는 순간에만 그 그림으로 바꿔 보여 줌
//   (몸·망토·무기를 잠깐 숨기고, 양손으로 낫을 휘두르는 그림 한 장으로)
// 무기 그림: "이름_Weapon" 파일이 있으면 몸 뒤에 무기를 따로 붙임 (예: BlackScythe_Weapon.png)
//   몸 그림의 손 위치, 무기 그림의 쥐는 위치를 아래 Weapons 표에 적어 두면
//   무기가 손을 기준으로 따로 움직일 수 있음 (UnitMotion 이 흔들고 휘두름)
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class UnitArt
{
    public const float PaintedHeight = 340f;   // 손그림 캐릭터가 화면에 보이는 키

    // 몬스터 그림은 크기가 서로 달라야 함 (슬라임은 작게, 수문장은 크게) → 이름별로 화면 키를 따로 정함
    // 여기에 없는 그림은 PaintedHeight 를 씀
    static readonly Dictionary<string, float> DisplayHeights = new Dictionary<string, float>
    {
        { "Slime", 150f },
        { "Bat", 210f },
        { "Soldier", 290f },
        { "Golem", 320f },        // 2026-11-15 용암 골렘 그림으로 바꿈 (덩치가 커서 조금 키움)
        { "Watcher", 280f },
        { "Gatekeeper", 410f },
        { "Werewolf", 400f },      // 광폭한 수문장 (웨어울프족 보스)
        { "Orc", 430f },           // 철퇴 수문장 (오크족 보스)
        { "ZombieKing", 420f },    // 부패한 수문장 (좀비족 보스)
        { "Architect", 450f },
        { "FortressLord", 430f },
        // 3-122: 일반 몬스터 그림 (기존 4종 + 새 1인 몬스터 4종)
        { "Rat", 160f }, { "Imp", 220f }, { "Spider", 190f }, { "Cultist", 290f },
        { "HornBeetle", 190f }, { "Sporeling", 200f }, { "ClaySplitter", 270f }, { "ClaySmall", 170f }, { "LanternKeeper", 280f },
        // 3-131: 정예·엘리트 그림 5종 (일반 7종은 같은 이름 그림을 고해상도로 교체 → 크기 그대로)
        { "OrcSlinger", 290f }, { "ThornKnight", 310f },                       // 정예
        { "Assassin", 300f }, { "Shaman", 300f }, { "EliteWatcher", 300f },    // 엘리트
        { "SquadWatcher", 220f },
        // 3-140: 일반 무리 (여러 마리가 같이 나와서 작게)
        { "Apprentice", 230f }, { "ApprenticeShield", 230f }, { "ApprenticeHex", 250f },   // 수련생 3명
        { "Larva", 200f },          // 점액 유충 ×4
        { "EmberSpirit", 210f },    // 불씨 정령 ×3   // 3-138: 감시자 무리 (EliteWatcher.png 를 작게)
        // 3-161: 2스테이지 보스 5 · 엘리트 4 그림 (가로로 긴 보스는 키를 낮게 → 폭이 화면 오른쪽 절반을 넘지 않게)
        { "AbyssShell", 280f },     // 심연의 거대 껍질 (가로 약 670)
        { "ForgottenKing", 330f },  // 잊혀진 왕의 망령 (해골 근위병 2명과 함께라 조금 작게)
        { "RiftWatcher", 280f },    // 차원 균열의 감시자 (가로 약 660)
        { "SunStatue", 470f }, { "MoonStatue", 470f },          // 심판의 쌍둥이 석상 (3-219: 380 → 470, 캐릭터(360)보다 확실히 크게)
        { "HoundHandler", 330f }, { "Beast", 300f },            // 사냥개 조련사 & 굶주린 맹수
        { "StoneSentinel", 400f },  // 고대 석상 파수꾼
        { "SoulWitch", 370f },      // 영혼 결속 마녀
        // 3-183: 정예 그림 7종 (무리로 나오는 것은 조금 작게 → 화면 오른쪽 절반 안에 들어가게)
        { "BoneCollector", 300f },                                  // 뼈조각 수집가 (혼자)
        { "SkeletonShield", 285f }, { "SkeletonArcher", 285f },     // 해골 방패병 & 사수 (2마리)
        { "SkeletonGuard", 250f },                                  // 해골 근위병 (잊혀진 왕의 망령 곁에 2마리, 가로가 넓은 그림)
        { "GoblinChief", 265f }, { "GoblinSpear", 250f }, { "GoblinNet", 250f },   // 고블린 3마리
        // 3-151: 플레이어 키 (예전엔 모두 PaintedHeight 340). 남캐는 조금 크게, 여캐는 조금 작게 (예시 B 안)
        //   공격·방어 같은 자세 그림도 몸 그림과 같은 배율이라 자동으로 같이 바뀜
        { "BlackScythe", 360f }, { "FrostMage", 360f },        // 남캐 +6%
        { "CosmicPhoenix", 325f }, { "Sniper", 325f },         // 여캐 -4%
    };

    // 3-151: 가로 배율 (1 = 그대로). 여캐는 0.95 → 조금 얇게 (0.92 부터는 방패가 타원처럼 찌그러져 보여서 0.95)
    //   몸·자세·망토·무기 그림 모두 가로만 이 배율을 곱함. 여기에 없는 그림은 1
    static readonly Dictionary<string, float> WidthScales = new Dictionary<string, float>
    {
        { "CosmicPhoenix", 0.95f }, { "Sniper", 0.95f },
    };
    static float WidthScale(string artName)
    {
        float w;
        return WidthScales.TryGetValue(artName, out w) ? w : 1f;
    }
    // 발 아래 빈 공간 (그림 높이에 대한 비율). 그림 아래쪽에 빛·무기·망토가 있어 발이 그림 맨 아래가 아닌 경우
    //   2026-11-15: 그림마다 발바닥 위치를 재서 정함 → 이만큼 그림을 내려서 발이 바닥 줄에 닿게
    static readonly Dictionary<string, float> FootRatios = new Dictionary<string, float>
    {
        { "BlackScythe", 0.05f }, { "BlackScythe_Charge", 0.13f }, { "BlackScythe_Attack", 0.03f },
        { "BlackScythe_Ambush", 0.07f }, { "BlackScythe_Night", 0.02f },
        { "CosmicPhoenix", 0.005f }, { "CosmicPhoenix_Attack", 0.03f }, { "CosmicPhoenix_Ambush", 0.06f }, { "CosmicPhoenix_Defend", 0.005f },
        { "FrostMage", 0.01f }, { "Sniper", 0.005f }, { "Sniper_Throw", 0.005f },
        { "FrostMage_AbsoluteZero", 0.0757f }, { "FrostMage_LightningFall", 0.0757f }, { "FrostMage_ManaCharge", 0.0084f },
        { "Sniper_Attack", 0.0092f }, { "Sniper_Bayonet", 0.017f },   // 3-90: 스나이퍼 조준 사격 / 총검술   // 3-75: 몸 그림과 같은 구도라 몸 자리에 정확히 겹치게 계산한 값 { "FrostMage_Lightning", 0.033f }, { "FrostMage_Ice", 0.05f },
        { "FortressLord", 0.02f }, { "Architect", 0.035f }, { "Architect_Attack", 0.035f },   // 3-187: 설계자 새 그림 (발 아래 빈 공간 3.5%)
        { "Werewolf", 0.01f }, { "Orc", 0.01f }, { "ZombieKing", 0.01f }, { "Golem", 0.01f },
    };
    // 그림의 아래쪽 위치 (발이 바닥 줄에 닿게). 표에 없으면 예전처럼 -6
    static float FootY(string name, float height)
    {
        float r;
        return FootRatios.TryGetValue(name, out r) ? -r * height : -6f;
    }

    public const float AttackOffsetX = 30f;    // 공격 자세 그림을 앞으로 얼마나 옮길지 (달려드는 느낌)
    // 수비 자세를 앞뒤로 얼마나 옮길지 (그림마다 인물 위치가 달라서 캐릭터별로)
    //   CosmicPhoenix: 방어 그림은 왼쪽에 불꽃 철퇴가 길게 뻗어 있어서 인물이 그림 가운데보다 오른쪽 → 왼쪽으로 당김
    // 수비 자세를 아예 안 쓰는 캐릭터 (방어도가 있어도 평소 자세)
    //   CosmicPhoenix: 2026-11-09 전투 그림을 새 디자인(검붉은 옷)으로 바꿈 → 예전 수비 그림(갑옷 기사)과 달라서 끔
    //                  새 디자인의 수비 그림이 생기면 여기서 빼고 CosmicPhoenix_Defend.png 를 바꾸면 됨
    static readonly HashSet<string> NoDefendPose = new HashSet<string> { "BlackScythe" };   // 2026-11-15: 피닉스는 새 방어 그림으로 다시 켬

    static readonly Dictionary<string, float> DefendOffsets = new Dictionary<string, float>
    {
        { "CosmicPhoenix", 0f },
    };
    public const float HarvestOffsetX = 20f;   // 영혼 수확 자세: 인물이 그림 가운데보다 조금 왼쪽
    public const float ChargeOffsetX = 0f;     // 차징 자세 (2026-11-15 새 그림: 인물이 가운데)
    public const float CurseOffsetX = 70f;     // 저주 자세: 인물은 왼쪽, 오른쪽은 원혼 덩어리
    public const float NightOffsetX = 0f;      // 밤 자세 (2026-11-15 새 그림: 인물이 가운데, 위에 원혼)    // 수비 자세: 앞쪽에 영혼 방패가 있어서 그림 가운데가 인물보다 앞에 있음

    // 무기 붙이는 위치 (0~1 비율, 왼쪽 아래가 0,0)
    //   Hand = 몸 그림에서 손 위치 / Grip = 무기 그림에서 손이 쥐는 곳 / Angle = 기본 기울기
    private class WeaponInfo
    {
        public Vector2 Hand;
        public Vector2 Grip;
        public float Angle;
        public WeaponInfo(Vector2 hand, Vector2 grip, float angle) { Hand = hand; Grip = grip; Angle = angle; }
    }

    // 망토 붙이는 위치 (0~1 비율): Attach = 몸 그림에서 목 위치 / Pivot = 망토 그림에서 목(회전 중심)
    private class CapeInfo
    {
        public Vector2 Attach;
        public Vector2 Pivot;
        public CapeInfo(Vector2 attach, Vector2 pivot) { Attach = attach; Pivot = pivot; }
    }

    // 망토·무기를 몸과 따로 붙일 캐릭터 목록 (지금은 없음)
    // 2026-10-30: 검은 낫을 새 그림(낫·사슬이 몸 그림에 같이 그려짐)으로 바꿔서 따로 붙이던 망토·낫은 뺌.
    //   예전 BlackScythe_Cape.png / BlackScythe_Weapon.png 파일이 남아 있어도 안 쓰임 (지워도 됨)
    //   예) { "BlackScythe", new CapeInfo(new Vector2(0.514f, 0.884f), new Vector2(0.575f, 0.962f)) }
    private static readonly Dictionary<string, CapeInfo> Capes = new Dictionary<string, CapeInfo>
    {
    };

    private static readonly Dictionary<string, WeaponInfo> Weapons = new Dictionary<string, WeaponInfo>
    {
    };
    // 플레이어 클래스 → 그림 파일 이름
    public static string PlayerArtName(PlayerClass cls)
    {
        switch (cls)
        {
            case PlayerClass.BlackScythe: return "BlackScythe";
            case PlayerClass.CosmicPhoenix: return "CosmicPhoenix";
            case PlayerClass.FrostMage: return "FrostMage";
            case PlayerClass.Sniper: return "Sniper";
            default: return "Common";
        }
    }

    // 몬스터 이름 → 그림 파일 이름
    public static string EnemyArtName(string enemyName)
    {
        // 2026-11-15 새 몬스터 (그림 파일이 없으면 아래 BuildEnemyPlaceholder 의 도형으로 보임)
        //   그림을 넣으려면 Resources/Art/ 에 같은 이름 PNG (예: Rat.png) 를 넣고 DisplayHeights 에 키를 적으면 됨
        // 3-102 새 몬스터 (그림이 오면 Resources/Art/ 에 이 이름으로). 이름이 겹치는 기존 몬스터보다 먼저 검사
        if (enemyName.Contains("딱정벌레")) return "HornBeetle";
        if (enemyName.Contains("포자충")) return "Sporeling";
        if (enemyName.Contains("점토 분열체")) return "ClaySplitter";
        if (enemyName.Contains("작은 점토체")) return "ClaySmall";
        if (enemyName.Contains("등불지기")) return "LanternKeeper";
        if (enemyName.Contains("불씨 정령")) return "EmberSpirit";
        if (enemyName.Contains("유충")) return "Larva";
        if (enemyName.Contains("방패 수련생")) return "ApprenticeShield";   // 3-140: 수련생 3명이 각자 그림
        if (enemyName.Contains("주술 수련생")) return "ApprenticeHex";
        if (enemyName.Contains("수련생")) return "Apprentice";           // 단검 수련생
        if (enemyName.Contains("뼈조각")) return "BoneCollector";
        if (enemyName.Contains("해골 사수")) return "SkeletonArcher";
        if (enemyName.Contains("해골 방패병")) return "SkeletonShield";
        if (enemyName.Contains("해골 근위병")) return "SkeletonGuard";
        if (enemyName.Contains("고블린 대장")) return "GoblinChief";
        if (enemyName.Contains("투창병")) return "GoblinSpear";
        if (enemyName.Contains("그물병")) return "GoblinNet";
        if (enemyName.Contains("석상 파수꾼")) return "StoneSentinel";
        if (enemyName.Contains("결속 마녀")) return "SoulWitch";
        if (enemyName.Contains("조련사")) return "HoundHandler";
        if (enemyName.Contains("맹수")) return "Beast";
        if (enemyName.Contains("거대 껍질")) return "AbyssShell";
        if (enemyName.Contains("왕의 망령")) return "ForgottenKing";
        if (enemyName.Contains("차원 균열")) return "RiftWatcher";
        if (enemyName.Contains("태양의 석상")) return "SunStatue";
        if (enemyName.Contains("달의 석상")) return "MoonStatue";
        if (enemyName.Contains("쥐")) return "Rat";
        if (enemyName.Contains("임프")) return "Imp";
        if (enemyName.Contains("거미")) return "Spider";
        if (enemyName.Contains("광신도")) return "Cultist";
        if (enemyName.Contains("투석병")) return "OrcSlinger";
        if (enemyName.Contains("가시 갑주병")) return "ThornKnight";
        if (enemyName.Contains("주술사")) return "Shaman";
        if (enemyName.Contains("암살자")) return "Assassin";
        if (enemyName.Contains("슬라임")) return "Slime";
        if (enemyName.Contains("박쥐")) return "Bat";
        if (enemyName.Contains("병정")) return "Soldier";
        if (enemyName.Contains("골렘")) return "Golem";
        if (enemyName.Contains("엘리트 감시자")) return "EliteWatcher";   // 3-131: 엘리트 감시자 전용 그림 (공격의 감시자 무리는 그대로 Watcher)
        if (enemyName.Contains("감시자")) return "SquadWatcher";   // 3-138: 감시자 무리(공격·수호·저주) = 엘리트 감시자와 같은 그림, 3마리라 조금 작게
        if (enemyName.StartsWith("요새")) return "FortressLord";   // 3스테이지 보스 (예전 설계자 그림)
        // 종족별 수문장 (1~2스테이지 보스). "요새의 …"(3스테이지)는 위에서 먼저 걸러짐
        if (enemyName.Contains("광폭한")) return "Werewolf";
        if (enemyName.Contains("철퇴")) return "Orc";
        if (enemyName.Contains("부패한")) return "ZombieKing";
        if (enemyName.Contains("수문장")) return "Gatekeeper";
        if (enemyName.Contains("설계자")) return "Architect";
        return "Enemy";
    }

    // 그림을 만들어서 parent 안에 발 위치(아래 가운데)에 맞춰 넣음. 반환값 = 그림의 높이
    public static float Build(Transform parent, string artName, bool isPlayer, Color tint)
    {
        RectTransform weapon, cape;
        return Build(parent, artName, isPlayer, tint, out weapon, out cape);
    }

    // weapon / cape = 따로 붙인 무기·망토 (없으면 null). UnitMotion 에 넘겨 주면 따로 움직임
    public static float Build(Transform parent, string artName, bool isPlayer, Color tint, out RectTransform weapon, out RectTransform cape)
    {
        weapon = null;
        cape = null;

        // Resources.Load: Assets/Resources 폴더 안의 파일을 이름으로 불러옴 (확장자는 빼고)
        Sprite sprite = Resources.Load<Sprite>("Art/" + ArtFile(artName));
        if (sprite != null && sprite.rect.height > 160f)
        {
            return BuildPainted(parent, artName, sprite, out weapon, out cape);
        }
        if (sprite != null)
        {
            // 픽셀아트는 정수 배로 키워야 깔끔함: 72픽셀 그림 → 4배 = 288
            float scale = Mathf.Max(1f, Mathf.Floor(288f / sprite.rect.height));
            Vector2 size = new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);
            Image img = UIFactory.MakeImage("Sprite", parent, Color.white, false);
            img.sprite = sprite;
            img.raycastTarget = false;
            UIFactory.Place(img.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, -6), size);
            return size.y;
        }
        return isPlayer ? BuildPlayerPlaceholder(parent, tint) : BuildEnemyPlaceholder(parent, artName);
    }

    // 3-138: 그림 이름 → 실제 파일 이름. 같은 그림을 다른 크기로 쓸 때 (자바의 Map 별칭)
    //   SquadWatcher(감시자 무리) → EliteWatcher.png 를 같이 씀 (크기는 DisplayHeights 의 SquadWatcher 값)
    static string ArtFile(string artName)
    {
        if (artName == "SquadWatcher") return "EliteWatcher";
        return artName;
    }

    // ---------------- 손그림 (큰 그림) ----------------
    static float BuildPainted(Transform parent, string artName, Sprite body, out RectTransform weapon, out RectTransform cape)
    {
        weapon = null;
        cape = null;
        float targetHeight;
        if (!DisplayHeights.TryGetValue(artName, out targetHeight)) targetHeight = PaintedHeight;
        float scale = targetHeight / body.rect.height;
        float sx = WidthScale(artName);   // 3-151: 가로 배율
        Vector2 size = new Vector2(body.rect.width * scale * sx, body.rect.height * scale);

        // 1) 뒤쪽 은은한 달빛 (어두운 캐릭터가 배경에 묻히지 않게)
        Image back = UIFactory.MakeImage("BackLight", parent, new Color(0.55f, 0.65f, 1f, 0.22f), false);
        back.sprite = UIFactory.SoftGlowSprite;
        back.raycastTarget = false;
        UIFactory.Place(back.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0, size.y * 0.55f), new Vector2(size.x * 1.9f, size.y * 1.25f));

        // 2) 망토 (가장 뒤. 목을 중심으로 흔들림)
        Sprite capeSprite = Resources.Load<Sprite>("Art/" + artName + "_Cape");
        CapeInfo capeInfo;
        if (capeSprite != null && Capes.TryGetValue(artName, out capeInfo))
        {
            Image c = UIFactory.MakeImage("Cape", parent, Color.white, false);
            c.sprite = capeSprite;
            c.raycastTarget = false;
            RectTransform crt = c.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = capeInfo.Pivot;
            crt.sizeDelta = new Vector2(capeSprite.rect.width * scale * sx, capeSprite.rect.height * scale);
            crt.anchoredPosition = new Vector2(-size.x / 2f + capeInfo.Attach.x * size.x, -6f + capeInfo.Attach.y * size.y);
            cape = crt;
        }

        // 3) 무기 (몸보다 먼저 만들어서 몸 뒤에 그려짐 → 손이 손잡이 앞에 보임)
        Sprite weaponSprite = Resources.Load<Sprite>("Art/" + artName + "_Weapon");
        WeaponInfo info;
        if (weaponSprite != null && Weapons.TryGetValue(artName, out info))
        {
            Image w = UIFactory.MakeImage("Weapon", parent, Color.white, false);
            w.sprite = weaponSprite;
            w.raycastTarget = false;
            RectTransform wrt = w.rectTransform;
            wrt.anchorMin = wrt.anchorMax = new Vector2(0.5f, 0f);
            wrt.pivot = info.Grip;                                     // 쥐는 곳을 중심으로 회전
            wrt.sizeDelta = new Vector2(weaponSprite.rect.width * scale * sx, weaponSprite.rect.height * scale);
            wrt.anchoredPosition = new Vector2(-size.x / 2f + info.Hand.x * size.x, -6f + info.Hand.y * size.y);
            wrt.localEulerAngles = new Vector3(0, 0, info.Angle);
            weapon = wrt;
        }

        // 4) 몸
        Image img = UIFactory.MakeImage("Sprite", parent, Color.white, false);
        img.sprite = body;
        img.raycastTarget = false;
        UIFactory.Place(img.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, FootY(artName, size.y)), size);

        // 5) 자세 그림들 (평소엔 꺼 둠. 몸 그림과 같은 비율로 키우고 발 위치를 맞춤)
        //    파일 이름 = 이름 + 뒤붙임. 없으면 그 자세는 그냥 평소 그림
        //    _Attack  : 공격 카드 돌진할 때
        //    _Special : 필살기 내려찍는 순간
        //    _Charge  : 차징 중 (검은 낫: 월광섬이 다음 턴에 터지기를 기다리는 동안 계속)
        //    _Defend  : 수비 (방어도가 남아 있는 동안 계속 + 방어도를 얻거나 막는 순간)
        //    offsetX = 앞으로 얼마나 옮길지. 그림 속 인물이 한쪽에 치우쳐 있으면 여기로 맞춤
        // 3-90: 스나이퍼 공격 = 제자리 조준 사격이라 앞으로 옮기지 않음 (가운데 정렬로 맞춘 그림)
        AddPose(parent, artName, "_Attack", "AttackPose", scale, artName == "Sniper" ? 0f : AttackOffsetX);
        AddPose(parent, artName, "_Special", "SpecialPose", scale, AttackOffsetX);
        AddPose(parent, artName, "_Charge", "ChargePose", scale, ChargeOffsetX);
        float defendOffset;
        if (!DefendOffsets.TryGetValue(artName, out defendOffset)) defendOffset = 0f;
        if (!NoDefendPose.Contains(artName))   // 검은 낫은 수비 자세를 쓰지 않음 (예전 그림 파일이 남아 있어도 무시)
            AddPose(parent, artName, "_Defend", "DefendPose", scale, defendOffset);
        AddPose(parent, artName, "_Harvest", "HarvestPose", scale, HarvestOffsetX);   // 영혼 수확 카드
        AddPose(parent, artName, "_Ambush", "AmbushPose", scale, AttackOffsetX);      // 기습 카드 + 월광섬이 터질 때
        AddPose(parent, artName, "_Curse", "CursePose", scale, CurseOffsetX);         // 적에게 거는 스킬 (죽음의 인장 등)
        AddPose(parent, artName, "_Night", "NightPose", scale, NightOffsetX);         // 죽음의 밤
        AddPose(parent, artName, "_Lightning", "LightningPose", scale, 0f);           // 마도사 [벼락] 공격 (2026-11-15)
        AddPose(parent, artName, "_Ice", "IcePose", scale, 0f);                       // 마도사 얼음 공격 (2026-11-15)
        AddPose(parent, artName, "_Throw", "ThrowPose", scale, 0f);                   // 스나이퍼 투척 (연막탄·사슬 함정, 3-74)
        AddPose(parent, artName, "_AbsoluteZero", "AbsoluteZeroPose", scale, 0f);     // 마도사 [절대영도] 전용 얼음 자세 (3-75)
        AddPose(parent, artName, "_LightningFall", "LightningFallPose", scale, 0f);   // 마도사 [낙뢰] 전용 전기 자세 (3-75)
        AddPose(parent, artName, "_ManaCharge", "ManaChargePose", scale, 0f);         // 마도사 마력 집중 (마력 증폭·재구성·해방, 3-77)
        AddPose(parent, artName, "_Bayonet", "BayonetPose", scale, 0f);               // 스나이퍼 [총검술] 휘두르기 (3-90)
        return size.y + 10f;
    }

    // 자세 그림 하나 붙이기 (파일이 없으면 아무것도 안 함)
    static void AddPose(Transform parent, string artName, string suffix, string objName, float scale, float offsetX)
    {
        Sprite sprite = Resources.Load<Sprite>("Art/" + artName + suffix);
        if (sprite == null) return;
        Image img = UIFactory.MakeImage(objName, parent, Color.white, false);
        img.sprite = sprite;
        img.raycastTarget = false;
        Vector2 size = new Vector2(sprite.rect.width * scale * WidthScale(artName), sprite.rect.height * scale);   // 3-151: 몸과 같은 가로 배율
        UIFactory.Place(img.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(offsetX * WidthScale(artName), FootY(artName + suffix, size.y)), size);
        img.enabled = false;
    }

    // ---------------- 임시 그림 (도형) ----------------

    static Image Part(Transform parent, string name, Color c, Vector2 pos, Vector2 size, bool circle, float angle = 0f)
    {
        Image img = circle ? UIFactory.MakeCircle(name, parent, c) : UIFactory.MakeImage(name, parent, c);
        img.raycastTarget = false;
        UIFactory.Place(img.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), pos, size);
        img.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
        return img;
    }

    static void Eyes(Transform parent, Vector2 center, float gap, float size, Color c)
    {
        Part(parent, "EyeL", c, center + new Vector2(-gap, 0), new Vector2(size, size), true);
        Part(parent, "EyeR", c, center + new Vector2(gap, 0), new Vector2(size, size), true);
    }

    static float BuildPlayerPlaceholder(Transform parent, Color tint)
    {
        Part(parent, "Body", tint, new Vector2(0, 100), new Vector2(110, 200), false);
        Part(parent, "Head", tint * 1.2f, new Vector2(0, 225), new Vector2(80, 80), true);
        Eyes(parent, new Vector2(14, 228), 12, 10, Color.white);
        return 270f;
    }

    static float BuildEnemyPlaceholder(Transform parent, string artName)
    {
        switch (artName)
        {
            // ---- 2026-11-15 새 몬스터 임시 도형 (그림이 오면 PNG 로 바뀜) ----
            case "Rat":
                Part(parent, "Tail", UIFactory.Hex("#8A6A6A"), new Vector2(75, 30), new Vector2(80, 8), false, -20f);
                Part(parent, "Body", UIFactory.Hex("#6A5A50"), new Vector2(0, 45), new Vector2(150, 80), true);
                Part(parent, "Head", UIFactory.Hex("#7A6A60"), new Vector2(-70, 60), new Vector2(70, 56), true);
                Part(parent, "Ear", UIFactory.Hex("#C08A8A"), new Vector2(-60, 95), new Vector2(28, 28), true);
                Part(parent, "Spots", UIFactory.Hex("#7CC050"), new Vector2(10, 60), new Vector2(30, 20), true);
                Eyes(parent, new Vector2(-82, 66), 10, 9, UIFactory.Hex("#FF4040"));
                return 120f;

            case "Imp":
                Part(parent, "Flame", new Color(1f, 0.5f, 0.15f, 0.45f), new Vector2(0, 120), new Vector2(150, 170), true);
                Part(parent, "WingL", UIFactory.Hex("#8A2A1A"), new Vector2(-55, 140), new Vector2(70, 34), false, 25f);
                Part(parent, "WingR", UIFactory.Hex("#8A2A1A"), new Vector2(55, 140), new Vector2(70, 34), false, -25f);
                Part(parent, "Body", UIFactory.Hex("#D0502A"), new Vector2(0, 110), new Vector2(70, 90), true);
                Part(parent, "HornL", UIFactory.Hex("#3A1A10"), new Vector2(-18, 165), new Vector2(10, 26), false, 20f);
                Part(parent, "HornR", UIFactory.Hex("#3A1A10"), new Vector2(18, 165), new Vector2(10, 26), false, -20f);
                Eyes(parent, new Vector2(0, 122), 14, 11, UIFactory.Hex("#FFE060"));
                return 200f;

            case "Spider":
                for (int i = 0; i < 4; i++)
                {
                    Part(parent, "LegL" + i, UIFactory.Hex("#2A2430"), new Vector2(-55 - i * 6, 40 + i * 6), new Vector2(90, 7), false, 30f - i * 20f);
                    Part(parent, "LegR" + i, UIFactory.Hex("#2A2430"), new Vector2(55 + i * 6, 40 + i * 6), new Vector2(90, 7), false, -30f + i * 20f);
                }
                Part(parent, "Abdomen", UIFactory.Hex("#3A3044"), new Vector2(20, 70), new Vector2(110, 90), true);
                Part(parent, "Mark", UIFactory.Hex("#C03040"), new Vector2(25, 80), new Vector2(26, 30), true);
                Part(parent, "Head", UIFactory.Hex("#4A3E55"), new Vector2(-50, 60), new Vector2(60, 52), true);
                Eyes(parent, new Vector2(-58, 66), 9, 8, UIFactory.Hex("#E040FF"));
                return 130f;

            case "Cultist":
                Part(parent, "Robe", UIFactory.Hex("#3A1E40"), new Vector2(0, 110), new Vector2(120, 220), false);
                Part(parent, "Sash", UIFactory.Hex("#A02030"), new Vector2(0, 120), new Vector2(124, 16), false, -15f);
                Part(parent, "Hood", UIFactory.Hex("#2A1430"), new Vector2(0, 245), new Vector2(90, 90), true);
                Part(parent, "Dagger", UIFactory.Hex("#C8C8D0"), new Vector2(-70, 120), new Vector2(8, 60), false, 30f);
                Eyes(parent, new Vector2(0, 245), 14, 10, UIFactory.Hex("#FF3030"));
                return 290f;

            case "OrcSlinger":
                Part(parent, "Body", UIFactory.Hex("#5A7A3A"), new Vector2(0, 110), new Vector2(140, 200), false);
                Part(parent, "Belt", UIFactory.Hex("#5A3A20"), new Vector2(0, 80), new Vector2(144, 20), false);
                Part(parent, "Head", UIFactory.Hex("#6A8A44"), new Vector2(0, 235), new Vector2(90, 80), true);
                Part(parent, "Rock", UIFactory.Hex("#8A8478"), new Vector2(-85, 190), new Vector2(46, 40), true);
                Part(parent, "Tusk", UIFactory.Hex("#F0E8D0"), new Vector2(-14, 218), new Vector2(8, 16), false);
                Eyes(parent, new Vector2(0, 245), 16, 10, UIFactory.Hex("#FFD040"));
                return 285f;

            case "ThornKnight":
                Part(parent, "Armor", UIFactory.Hex("#3E4A3A"), new Vector2(0, 120), new Vector2(150, 230), false);
                for (int i = 0; i < 5; i++)
                    Part(parent, "Thorn" + i, UIFactory.Hex("#7AA050"), new Vector2(-60 + i * 30, 238), new Vector2(10, 34), false, (i - 2) * 12f);
                Part(parent, "Helm", UIFactory.Hex("#2E3A2C"), new Vector2(0, 262), new Vector2(96, 86), true);
                Part(parent, "Visor", UIFactory.Hex("#0A0E08"), new Vector2(0, 264), new Vector2(66, 14), false);
                Eyes(parent, new Vector2(0, 264), 16, 9, UIFactory.Hex("#B0FF60"));
                return 310f;

            case "Shaman":
                Part(parent, "Aura", new Color(0.4f, 0.9f, 0.3f, 0.22f), new Vector2(0, 160), new Vector2(260, 300), true);
                Part(parent, "Robe", UIFactory.Hex("#3A4A2A"), new Vector2(0, 120), new Vector2(140, 240), false);
                Part(parent, "Staff", UIFactory.Hex("#6A4A2A"), new Vector2(85, 150), new Vector2(10, 280), false);
                Part(parent, "Skull", UIFactory.Hex("#E0DCC8"), new Vector2(85, 295), new Vector2(40, 40), true);
                Part(parent, "Mask", UIFactory.Hex("#C8C0A0"), new Vector2(0, 268), new Vector2(84, 96), true);
                Eyes(parent, new Vector2(0, 272), 16, 12, UIFactory.Hex("#60FF40"));
                return 330f;

            case "Assassin":
                Part(parent, "Cloak", UIFactory.Hex("#1A1824"), new Vector2(0, 120), new Vector2(130, 240), false);
                Part(parent, "BladeL", UIFactory.Hex("#B8C0D0"), new Vector2(-75, 110), new Vector2(8, 70), false, 40f);
                Part(parent, "BladeR", UIFactory.Hex("#B8C0D0"), new Vector2(75, 110), new Vector2(8, 70), false, -40f);
                Part(parent, "Hood", UIFactory.Hex("#12101A"), new Vector2(0, 262), new Vector2(92, 92), true);
                Part(parent, "Scarf", UIFactory.Hex("#6A1A2A"), new Vector2(0, 222), new Vector2(100, 20), false);
                Eyes(parent, new Vector2(0, 268), 14, 8, UIFactory.Hex("#FFFFFF"));
                return 320f;

            case "Slime":
                Part(parent, "Body", UIFactory.Hex("#3FB86A"), new Vector2(0, 55), new Vector2(170, 110), false);
                Part(parent, "Top", UIFactory.Hex("#3FB86A"), new Vector2(0, 110), new Vector2(110, 70), true);
                Part(parent, "Shine", new Color(1, 1, 1, 0.35f), new Vector2(-40, 85), new Vector2(26, 18), true);
                Eyes(parent, new Vector2(-10, 70), 24, 20, UIFactory.Hex("#10301C"));
                return 150f;

            case "Bat":
                Part(parent, "WingL", UIFactory.Hex("#5A3A7A"), new Vector2(-62, 150), new Vector2(110, 46), false, 20f);
                Part(parent, "WingR", UIFactory.Hex("#5A3A7A"), new Vector2(62, 150), new Vector2(110, 46), false, -20f);
                Part(parent, "Body", UIFactory.Hex("#3A2850"), new Vector2(0, 140), new Vector2(84, 84), true);
                Eyes(parent, new Vector2(0, 148), 15, 12, UIFactory.Hex("#FF5050"));
                return 190f;

            case "Soldier":
                Part(parent, "Legs", UIFactory.Hex("#3A3E44"), new Vector2(0, 45), new Vector2(70, 90), false);
                Part(parent, "Body", UIFactory.Hex("#6A6E60"), new Vector2(0, 140), new Vector2(110, 120), false);
                Part(parent, "Shield", UIFactory.Hex("#5A4A34"), new Vector2(-60, 130), new Vector2(60, 100), false);
                Part(parent, "Head", UIFactory.Hex("#7A7E70"), new Vector2(0, 225), new Vector2(70, 70), true);
                Eyes(parent, new Vector2(10, 228), 12, 9, UIFactory.Hex("#B8FF60"));
                return 265f;

            case "Golem":
                Part(parent, "Body", UIFactory.Hex("#6B6460"), new Vector2(0, 110), new Vector2(200, 200), false);
                Part(parent, "ArmL", UIFactory.Hex("#57504C"), new Vector2(-120, 100), new Vector2(60, 150), false);
                Part(parent, "ArmR", UIFactory.Hex("#57504C"), new Vector2(120, 100), new Vector2(60, 150), false);
                Part(parent, "Head", UIFactory.Hex("#7B746E"), new Vector2(0, 235), new Vector2(100, 70), false);
                Eyes(parent, new Vector2(0, 238), 22, 14, UIFactory.Hex("#FFB040"));
                return 275f;

            case "Watcher":
                Part(parent, "Aura", new Color(0.55f, 0.3f, 0.9f, 0.25f), new Vector2(0, 160), new Vector2(240, 240), true);
                Part(parent, "Eye", UIFactory.Hex("#E8E0F0"), new Vector2(0, 160), new Vector2(170, 170), true);
                Part(parent, "Iris", UIFactory.Hex("#7A2AB0"), new Vector2(10, 160), new Vector2(90, 90), true);
                Part(parent, "Pupil", UIFactory.Hex("#FF3030"), new Vector2(14, 160), new Vector2(36, 36), true);
                return 280f;

            case "Gatekeeper":
                Part(parent, "Cape", UIFactory.Hex("#4A1A1A"), new Vector2(0, 150), new Vector2(230, 280), false);
                Part(parent, "Armor", UIFactory.Hex("#3A3C46"), new Vector2(0, 160), new Vector2(170, 240), false);
                Part(parent, "Helm", UIFactory.Hex("#2A2C34"), new Vector2(0, 320), new Vector2(110, 110), false);
                Part(parent, "Visor", UIFactory.Hex("#0A0A0E"), new Vector2(0, 322), new Vector2(80, 18), false);
                Eyes(parent, new Vector2(0, 322), 20, 12, UIFactory.Hex("#FF3030"));
                return 380f;

            // ---- 3-102 새 몬스터 임시 도형 (크기만 대충 구분. 그림이 오면 PNG 로 바뀜) ----
            case "Larva": case "EmberSpirit": case "ClaySmall": case "Sporeling":
            {
                Color c = artName == "EmberSpirit" ? UIFactory.Hex("#E0602A") : artName == "Larva" ? UIFactory.Hex("#7AB04A")
                        : artName == "Sporeling" ? UIFactory.Hex("#8A6AB0") : UIFactory.Hex("#9A7A5A");
                Part(parent, "Body", c, new Vector2(0, 50), new Vector2(110, 100), true);
                Eyes(parent, new Vector2(0, 62), 16, 12, Color.white);
                return 110f;
            }
            case "AbyssShell": case "ForgottenKing": case "RiftWatcher": case "SunStatue": case "MoonStatue": case "StoneSentinel":
            {
                Color c = artName == "AbyssShell" ? UIFactory.Hex("#2A4A5A") : artName == "ForgottenKing" ? UIFactory.Hex("#4A5A7A")
                        : artName == "RiftWatcher" ? UIFactory.Hex("#4A2A7A") : artName == "SunStatue" ? UIFactory.Hex("#B08A3A")
                        : artName == "MoonStatue" ? UIFactory.Hex("#7A8AA8") : UIFactory.Hex("#6A6A60");
                Part(parent, "Body", c, new Vector2(0, 150), new Vector2(220, 300), false);
                Eyes(parent, new Vector2(0, 240), 30, 18, UIFactory.Hex("#FFD060"));
                return 320f;
            }

            default:
                Part(parent, "Body", UIFactory.Hex("#5A4A6A"), new Vector2(0, 90), new Vector2(150, 180), false);
                Eyes(parent, new Vector2(0, 140), 20, 14, Color.white);
                return 200f;
        }
    }
}
