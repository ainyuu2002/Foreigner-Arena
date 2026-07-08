# Phase 11 — Map & Biome (3 map: Băng / Đầm Lầy / Lửa)

> Người phụ trách: **P3 — Nguyễn Xuân Đạt (Maps & Weather)**. Tài liệu này append vào
> `Docs/CharacterBuildGuide.md` khi tổng hợp. Mọi thứ dựng bằng **Editor tool** (menu `FA/Stage/...`)
> theo đúng pattern harness P12 — không dựng tay, chạy lại menu là tái tạo được map.

---

## 1. Cách dựng (trong Unity Editor)

1. Mở project → chờ compile xong → menu **FA → Stage** xuất hiện.
2. Bấm **`FA/Stage/5. Build ALL (art + 3 map)`** — tool sẽ:
   - Sinh toàn bộ art pixel 3 biome vào `Assets/_Project/Art/Maps/{Ice,Swamp,Fire}/` (PNG + Tile asset,
     Point filter, PPU chuẩn) — art sinh procedural, chạy lại là tái tạo, không cần asset ngoài.
   - Dựng 3 scene: **`Map_Ice`**, **`Map_Swamp`**, **`Map_Fire`** trong `Assets/_Project/Scenes/`.
   - Tự thêm 3 scene vào **Build Settings** + đăng ký vào **Harness_StageRegistry** (P12 chọn map là chạy).
   - Tạo **`StageConfig_Swamp.asset`** nếu chưa có (Ice/Fire dùng asset sẵn).
3. Test nhanh không cần P12: mở 1 map → **`FA/Stage/Test - Thả 2 fighter vào map đang mở`**
   (P1 = Keyboard, P2 = Gamepad; tự gán opponent chéo, add camera target group, add DropThroughController,
   bật debug self-respawn) → **Play**.
4. Trước khi ghép luồng P12 thật: **`FA/Stage/Test - Tắt debug self-respawn (chuẩn bị ghép P12)`**
   rồi save scene (LivesSystem của P12 sẽ xử lý mạng qua kênh `OnPlayerDiedChannel`).

## 2. Nội dung từng map

| | Map_Ice — Đỉnh Vĩnh Đông | Map_Swamp — Rừng Sương Độc | Map_Fire — Lò Dung Nham |
|---|---|---|---|
| Layout | Sàn băng chính + 2 platform bay đối xứng | 2 bờ đất cao + bệ chìm giữa nước + 2 platform lệch tầng | Đảo bazan + 3 platform bay trên biển dung nham |
| Cơ chế thường trực | Mặt băng trượt (`IceSurface` friction 0.02 — Unity vật lý lo) | **SwampBiome**: slowness khi đứng đất + giảm lực nhảy ~25% | **FireBiome**: đứng yên trên đảo → +%/giây |
| Thiên tai | **SnowstormHazard**: bão chu kỳ random, slow + gió đẩy ngang phá combo | **Không có** (đúng GDD Trang 9) | **RisingLava**: cảnh báo nhấp nháy → lava dâng, chạm = chết |
| Đặc biệt | 2 **BreakableWall** 2 bên (vỡ → mở death zone; có hồi `wallRecovers=true`) | Nấm phát sáng + đom đóm + rừng sương parallax | Lava vẽ đè lên nhân vật (nuốt chửng), 3 đèn cam hắt sáng |
| Visual | Trời đêm + cực quang + trăng + núi tuyết parallax 3 lớp, tuyết rơi, nhũ băng | Trăng độc + dây leo + cây cổ thụ + mặt nước phản chiếu | Núi lửa phun + khói + tàn lửa bay + trăng máu |

Mọi map đều có: Death Zone 4 phía (xa rìa), `StageSpawnPoints` (spawnP1/P2 + respawn giữa map),
platform 1 chiều **PlatformEffector2D** (nhảy xuyên từ dưới, giữ ↓ để rớt xuyên qua `DropThroughController`),
camera **Cinemachine 3** (TargetGroup + PositionComposer + **Confiner2D** theo bounds map),
**Light2D** global + đèn điểm không khí, particle môi trường.

## 3. Code mới (module `FA.Stage`, chỉ ref `FA.Core`)

- `SwampBiome.cs` — biome Đầm Lầy: nhân vx khi đứng đất trong đầm (motor ghi lại vx mỗi step nên tốc độ
  hiệu dụng ổn định; trên không KHÔNG áp để không phá knockback P7) + phát hiện impulse nhảy → nhân vy 1 lần.
- `ParallaxLayer.cs` — lớp nền bám camera theo hệ số (Unity không có sẵn; code điều phối tối thiểu).
- `StageConfigSO.cs` — thêm 3 field Swamp (xem bảng CẦN TEST).
- Editor (FA.EditorTools): `StagePxCanvas` + `StageArtIce/Swamp/Fire` (sinh art), `StageSceneKit`
  (helper dựng scene), `StageMapBuilderIce/Swamp/Fire`, `StagePhase11Menu`.
- `FA.EditorTools.Editor.asmdef` thêm ref `Unity.Cinemachine` + `Unity.RenderPipelines.Universal.Runtime`.

## 4. Definition of Done (checklist test tay)

- [ ] Rơi/bị đánh văng ra 4 phía → console log chết + teleport về respawn (debug mode).
- [ ] Platform 1 chiều: nhảy xuyên từ DƯỚI lên đứng được; đứng trên giữ **↓** ~0.2s → rớt xuyên.
- [ ] **Ice**: knockback trên sàn băng trượt xa hơn rõ rệt; bão tuyết bật theo chu kỳ (hạt tuyết dày lên,
      bị gió đẩy lệch); đập tường băng nhiều lần (knockback ≥12) → vỡ → lộ death zone; để yên ~5s tường hồi.
- [ ] **Swamp**: đi trên đất chậm hơn hẳn; nhảy thấp hơn rõ (so với map khác); trên không knockback bình thường.
- [ ] **Fire**: đứng yên trên đảo → % tăng dần (HUD); di chuyển thì không; icon ⚠ nhấp nháy ~2.5-3s rồi lava
      dâng; chạm lava = mất mạng; lava dừng ở `maxRiseY`.
- [ ] Camera zoom/bám 2 người, KHÔNG lia ra ngoài mép map (Confiner).
- [ ] Không hồi quy P1–P10 (di chuyển/combat/resource như cũ — P11 chỉ ADD môi trường).
- [ ] Chọn map qua luồng P12 (CharSelect → StageSelect): 3 map xuất hiện trong danh sách, load additive OK
      (sau khi đã tắt debug self-respawn).

## 5. Tham số CẦN TEST (chỉnh Inspector/asset, KHÔNG sửa code)

| Asset | Field | Mặc định | Ghi chú |
|---|---|---|---|
| StageConfig_Swamp | `swampSpeedFactor` | 0.8 | slow đứng đất; nhỏ hơn = lầy hơn |
| StageConfig_Swamp | `swampJumpScale` | 0.85 | ~giảm 25-28% chiều cao nhảy (GDD: 25%) |
| StageConfig_Swamp | `swampJumpImpulseThreshold` | 6 | < jumpSpeed nhân vật (~12) |
| StageConfig_Ice | `snowstorm*` | 12/5/3/0.4/3/0.85 | nhịp + cường độ bão |
| StageConfig_Ice/Fire | `breakThreshold`, `wallImpactMinSpeed` | 100 / 12 | đồng bộ thang lực P7 |
| StageConfig_Fire | `firePercentPerSec` | 2.5 | %/giây đứng yên |
| StageConfig_Fire | `lavaRiseSpeed` / `lavaWarningLead` | 0.4 / 2.5 | GDD Trang 7 nói cảnh báo ~3s → cân nhắc tăng lead lên 3 |
| Data/Stage/IceSurface | `friction` | 0.02 | độ trượt băng |
| Scene Map_Fire | `RisingLava.maxRiseY` | -2.5 | trần dâng; cao hơn = gắt hơn |
| Scene (mỗi map) | vị trí 4 DeathZone | ±24 / +20..22 / −14..−18 | kéo xa/gần chỉnh độ khó ring-out |

## 6. Ghi chú kiến trúc (để reviewer soi)

- Unity-native đúng §1.1: trượt băng = PhysicsMaterial2D, platform 1 chiều = PlatformEffector2D,
  lava dâng = Kinematic `MovePosition` (code có sẵn P11), va chạm lọc bằng Collision Matrix, camera = Cinemachine.
- Stage KHÔNG xử lý mạng — DeathZone chỉ raise `OnPlayerDiedChannel`; P12 LivesSystem subscribe.
- SwampBiome là code MỚI duy nhất có logic gameplay: chỉ chạm `Rigidbody2D` + layer (không ref module khác),
  mọi số trong `StageConfigSO`. File < 500 dòng, 1 file 1 chức năng.
- Scene sinh bằng tool = diff sạch, không ai đụng scene của nhau (đúng quy tắc sở hữu scene trong plan nhóm).
