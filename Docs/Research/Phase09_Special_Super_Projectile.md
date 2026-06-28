# Research — Phase 9: Special & Super Moves (motion input, projectile)

> Module: `FA.Specials` (chỉ reference `FA.Core`). Tái dùng tối đa pipeline P4 (Input/Parser),
> P5 (Hitbox/MoveData/Relay/AttackState pattern), P8 (ManaBar). KHÔNG code lại thứ Unity / phase trước.
> Nguồn: CLAUDE.md §5 (Phase 9), §3.2/§3.4/§3.5, §1.1; GDD §2.2/§2.3, §4.2 "Special & Super Move Framework".

---

## 1. Cơ chế cốt lõi & sub-mechanics

Phase 9 thêm tầng "đòn đặc biệt" lên nền combat P5–P7. 6 sub-mechanic:

1. **Special move (motion input)** — `↓↘→` / `↓↙←` / DP + nút Normal (L/M/H) → ra đòn đặc biệt.
2. **Super move (nút riêng)** — `Super1` (50% mana) / `Super2` (75% mana). Không cần motion, dùng được trên không.
3. **Projectile system** — prefab Rigidbody2D + BoxCollider2D + Hitbox, layer `ProjectileP1/ProjectileP2`,
   spawn bằng Animation Event `SpawnProjectile`.
4. **Charge mana → special mạnh hơn** — giữ nút/motion lâu → biến thể mạnh (đọc thời gian giữ qua parser).
5. **`SpecialState` / `SuperState` tham số hóa bởi `MoveData`** — thêm move = thêm asset, không sửa state.
6. **Tra bảng special từ motion** — `CharacterData` (hoặc một MoveSet asset) map (motion + nút) → `MoveData`.

GDD §4.2 chốt: Special cost "by character"; Super = 1 nút riêng, 50%/75% mana, "no combo input, usable in the air";
"some special can charge extra mana to be stronger"; 1 normal dạng bắn chưởng tốn mana (projectile-type).
Role special (Projectile/Reversal/AntiAir/MixUp/BlockBreak) đã có enum `MoveRole` ở Core từ P4 — P9 mới tiêu thụ.

---

## 2. Tham khảo (SF | Smash | Lựa chọn cho Foreigner Arena)

| Sub-mechanic | Street Fighter | Smash Bros | Chọn (lý do) |
|---|---|---|---|
| Kích special | Motion `↓↘→`+đấm (QCF), DP `→↓↘`; buffer ~8–10f; "lenient" bỏ hướng thừa | Special = 1 nút + hướng (B / ↑B / ↓B) | **Theo SF**: motion input qua `CommandParser` đã có (QCF/QCB/DP, lenient, tương-đối-facing). Đúng định hướng "lai SF" của GDD. Không làm kiểu Smash 1-nút cho special thường. |
| Kích super | Motion dài (2×QCF) hoặc nút macro; tốn meter (EX/Super bar) | Final Smash = 1 nút khi có Smash Ball | **Lai**: dùng **nút riêng** (Smash-style, GDD §4.2 "1 dedicated button") **+ tốn meter** (SF-style). Super1=50%, Super2=75% mana. |
| Mana / meter | Super/EX gauge đầy theo damage dealt/taken; tiêu theo "stock" (1/2/3 thanh) | Không có (Smash dùng %) | **SF-style stock**: `ManaBar` (P8) đã là model GG/SFV "chia ô" + `TrySpend(int ô)`. Super tiêu theo ô. Xem §3 mapping 50%/75%. |
| Projectile | Hadoken: spawn ở frame cụ thể của clip, di chuyển ngang tốc cố định, 1 hit rồi tắt | PK Fire/Falco Laser: prefab vật lý, despawn theo range/time | **Lai**: spawn qua **Animation Event** (SF frame-precise) + di chuyển bằng **Rigidbody2D.velocity** (Unity-native), despawn theo lifetime/khoảng cách. |
| Charge | Charge moves (giữ ↓ X frame rồi ↑) HOẶC giữ nút (Guile Sonic); SF cũng có "hold to power up" (Gief lariat?) | Smash charge (giữ B: Samus charge shot) | **Theo "hold nút để power-up"** (GDD §4.2). Đọc số frame giữ nút qua parser → chọn `MoveData` biến thể (level 1/2). KHÔNG dùng charge-motion `←→` cho P9 (parser mới có khung, chưa hiện thực — để sau). |
| Air super | Một số super có air version | Up-B/aerial special phổ biến | **Cho phép super trên không** (GDD): bỏ gate grounded cho `SuperState`. Special motion thường vẫn ưu tiên grounded (air special "depends on character" — GDD §6, để ngỏ). |

---

## 3. Ánh xạ Unity-native (component nào, KHÔNG code tay phần nào)

### 3.1. Input → đã có, CHỈ tiêu thụ (KHÔNG code lại parser)
- Motion: `CommandParser.TryMatchMotion(MotionInput, withinFrames, facing)` / `DetectMotion(...)` — **đã hiện thực** QCF/QCB/DP, lenient, tương-đối-facing. P9 chỉ **gọi**.
- Nút Super1/Super2/L/M/H: `WasPressedWithin(InputButton, edgeWindow)` + **bắt buộc** `ConsumePress(...)` (memory phase4: 1 nhấn = 1 kích).
- Charge: `GetDirectionHold(...)` đã có cho hướng; **giữ NÚT** thì đọc số frame giữ từ buffer — đề xuất thêm 1 helper `HeldFrames(InputButton)` ở `CommandParser` (Core) NẾU cần (mở rộng = thêm method, không sửa method cũ). Đây là phần Core duy nhất có thể cần đụng.
- **KHÔNG** đọc Input System trực tiếp (memory input-system-no-legacy-input).

### 3.2. State: SpecialState / SuperState — clone pattern của AttackState (KHÔNG sửa AttackState)
- `IState` ở Core; pattern y hệt `AttackState`: set `Animator.SetInteger(StateId, move.animatorStateId)`, khóa input tới Animation Event `RecoveryEnd`, subscribe relay per-instance, Exit unsubscribe.
- **1 state class phục vụ MỌI special** (tham số bởi `MoveData`), **1 state class cho MỌI super** — thêm đòn = thêm asset (extensible §1.2). KHÔNG 1 class/đòn.
- Truy cập nhân vật qua `ICombatContext` (Core) → `FA.Specials` **KHÔNG ref `FA.Movement`/`FA.Combat`** (§1.3). Cần thêm gì (vd zero-vx, IsGrounded) đều đã có trong `ICombatContext`.
- Tách Special vs Super state vì khác hành vi: Super dùng được trên không (bỏ gate grounded), không có motion; có thể có animation-speed/iframe khác.

### 3.3. Entry-point: SpecialController (clone pattern CombatController, KHÔNG sửa CombatController)
- MonoBehaviour riêng trong `FA.Specials`, song song `CombatController`. Mỗi `Update`:
  1. Nếu `Super1/Super2` edge → tra `MoveData` super tương ứng → kiểm mana (`ManaBar.TrySpend`) → vào `SuperState`. (Cho phép cả khi trên không.)
  2. Nếu một nút L/M/H edge **và** một motion vừa hoàn tất trong window → tra bảng special → (kiểm mana nếu có cost) → vào `SpecialState`.
- **Thứ tự ưu tiên parse vs CombatController**: SpecialController phải xét **trước** normal — khi `↓↘→+L` thì ra special chứ không phải nL. Hai cách (chọn 1, ghi rõ cho implementer):
  - (A) `SpecialController.Update` chạy trước (Script Execution Order) và khi nó ăn input thì `ConsumePress(L)` → CombatController thấy đã consume → không ra nL. **Khuyến nghị (A)** vì dùng đúng cơ chế consume đã có, không cần module nói chuyện nhau.
  - (B) Một "AttackRouter" hợp nhất — tốn công, dễ đụng file cũ. Bỏ.
- Gate: special thường chỉ khi GROUNDED + ground-neutral (giống P5); super cho cả air. Dùng `ICombatContext.IsGrounded` + so `CurrentState` với Idle/Walk như CombatController.

### 3.4. Projectile — Unity-native vật lý + Animation Event spawn
- **Prefab** (đặt trong `Prefabs/`): `Rigidbody2D` (gravityScale=0 cho chưởng bay thẳng, hoặc >0 nếu rơi theo cung), `BoxCollider2D` (body, có thể trigger), child `Hitbox` = `BoxCollider2D isTrigger` + `HitboxAuthoring` (TÁI DÙNG P5) layer `Hitbox`, gán `MoveData` của projectile.
- **Layer ProjectileP1/ProjectileP2** đã khai ở `GameLayers` + Project Settings. Hitbox của projectile vẫn ở layer `Hitbox` để Collision Matrix (Hitbox↔Hurtbox) tự lọc — KHÔNG `if(layer==)`. Layer ProjectileP1/P2 dùng cho **body** projectile để xử lý projectile-vs-projectile / projectile-vs-wall qua Matrix (không code).
- **Spawn = Animation Event `SpawnProjectile(variantId)`** → `AnimationEventRelay.SpawnProjectileEvent` (đã có) → một `ProjectileSpawner` (FA.Specials) subscribe per-instance → Instantiate prefab tại muzzle, set hướng theo `Facing`, set `Rigidbody2D.linearVelocity = facing * speed`, gán attackerRoot + hitChannel (`OnHitLandedChannel`) cho `HitboxAuthoring`.
- **Di chuyển**: `Rigidbody2D.linearVelocity` (KHÔNG tự dời transform). **Despawn**: lifetime (timer chấp nhận — đây KHÔNG phải frame-data đòn, giống dashDuration được phép), hoặc khi va wall/ngoài camera. Despawn về Idle bằng Destroy hoặc (tốt hơn) pool đơn giản.
- HitData của projectile vẫn raise qua `OnHitLandedChannel` → P6 hit-reaction, P7 knockback, P8 mana **tự nghe sẵn** (decoupling §3.3). Attacker của HitData = root người bắn (đặt `attackerRoot` khi spawn) → mana/combo cộng đúng người.
- **Chưởng-normal tốn mana** (GDD): chỉ là một `MoveData` `category=Normal`, `role=Projectile`, `manaCostUnits>0`, spawn projectile như trên — kích từ CombatController hay SpecialController tùy thiết kế; đơn giản nhất: để nó là một special không-motion hoặc một entry trong bảng. Khuyến nghị coi như special (motion None + button) để giữ CombatController nguyên vẹn.

### 3.5. Mana cost — TÁI DÙNG ManaBar.TrySpend (KHÔNG code lại thanh mana)
- `ManaBar` (P8) đã có `TrySpend(int segments)` + `Segments` + `CurrentMana`. SpecialController/SuperState gọi `TrySpend` TRƯỚC khi ChangeState; thiếu → không ra (và không consume animation).
- **Mapping 50%/75% → ô**: ManaBar hiện tiêu theo **ô nguyên**. Đề xuất:
  - Đặt số ô tối đa cố định (vd `MaxMana=400`, `ManaPerSegment=100` → 4 ô). Super1 = 2 ô (~50%), Super2 = 3 ô (~75%).
  - HOẶC thêm `TrySpendPercent(float)` vào ManaBar (Resources) nếu muốn đúng % tuyệt đối — **đây là sửa file P8**, cân nhắc: GDD nói % nhưng ManaBar đã chốt model ô. **Khuyến nghị**: dùng `MoveData.manaCostUnits` (đã có field) = số ô, set sao cho ~50%/75% theo MaxMana cấu hình; KHÔNG sửa ManaBar. Ghi "CẦN TEST" để chỉnh MaxMana/ManaPerSegment cho ra đúng cảm giác 50/75%.
- Special cost "by character": dùng `MoveData.manaCostUnits` (0 nếu free). Charge level mạnh hơn = tiêu thêm ô (có thể là 2 MoveData biến thể, hoặc 1 MoveData + multiplier — xem §3.6).

### 3.6. Charge → biến thể mạnh hơn
- Đọc số frame giữ nút khi vào state (helper `HeldFrames` nếu thêm). Ngưỡng `MoveData.chargeFrames` (đã có field) → nếu giữ ≥ ngưỡng & đủ mana thêm → dùng **MoveData biến thể "charged"** (asset thứ 2) hoặc nhân hệ số.
- Khuyến nghị extensible: bảng special map (motion+button) → có thể trỏ tới 2 MoveData (level 1 / level 2 charged). Hoặc đơn giản P9: 1 cờ `isCharged` trong state điều chỉnh `baseKnockback`/`baseDamagePercent` runtime. **Chốt giá trị + CẦN TEST.**

### 3.7. Bảng special (motion → MoveData) — đặt ở đâu
- `CharacterData` (FA.Movement) là nơi GDD nói "tra bảng special của CharacterData". NHƯNG `MoveData` ở Core còn `CharacterData` ở `FA.Movement`; `FA.Specials` chỉ ref Core → **KHÔNG đọc được `CharacterData` của Movement**.
- **Khuyến nghị**: tạo **`MoveSetData` (ScriptableObject ở FA.Core)** = list các entry `{motion, button, MoveData, manaCostUnits override?}`. SpecialController serialize ref tới `MoveSetData` (gán trên prefab). Vừa đúng §1.3 (chỉ ref Core), vừa extensible (thêm move = thêm entry asset), vừa tách dữ liệu khỏi `CharacterData` (giữ Movement nguyên). Lookup: với mỗi entry, `DetectMotion` khớp motion + nút edge → trả MoveData.
- Lý do KHÔNG nhồi list special vào `CharacterData`: sẽ buộc `FA.Specials` ref `FA.Movement` (vi phạm §1.3). `MoveSetData` ở Core là cầu nối sạch.

### KHÔNG code tay (bảng đối chiếu §1.1)
- Vận tốc/đường bay projectile: **Rigidbody2D**, không tự `x += v*dt`.
- Lọc va chạm projectile: **Physics2D Collision Matrix**, không if-layer.
- Thời điểm spawn / hitbox on-off: **Animation Event** (`SpawnProjectile`/`HitboxOn`/`HitboxOff`), không đếm frame.
- Transition sang clip special/super: **Animator transition theo StateId** (cửa sổ Animator), không `Animator.Play`.
- Mana: **ManaBar P8**; mọi listener hit (knockback/combo/mana) **đã subscribe** kênh — không nối lại.

---

## 4. Edge case & cạm bẫy

1. **Special "nuốt" normal**: `↓↘→+L` phải ra special, KHÔNG nL. Giải: SpecialController parse trước + `ConsumePress(L)` khi ăn → CombatController bỏ qua (cơ chế consume P4). Test kỹ.
2. **Motion theo facing**: parser đã tương-đối-facing; truyền `ICombatContext.Facing` đúng. Sau khi đổi hướng giữa motion → lenient parser có thể trượt; chấp nhận (giống SF).
3. **Mana check phải TRƯỚC ChangeState**: nếu vào state rồi mới phát hiện thiếu mana → kẹt animation/whiff. Kiểm `Segments`/`TrySpend` trước; thiếu → không consume edge/animation.
4. **Super trên không**: bỏ gate grounded cho super, NHƯNG cần xử lý landing/recovery (Animation Event RecoveryEnd vẫn về Idle/Air tùy grounded). Tái dùng cách AttackState chọn Idle/Walk; super air về Air-state (cần `ICombatContext` cấp Air? — hiện chỉ có Idle/Walk). Cạm bẫy: ICombatContext CHƯA expose air-state. Đề xuất: super air khi RecoveryEnd nếu !grounded → để StateMachine rơi tự nhiên (set StateId Fall) hoặc thêm getter. Ghi rõ cho implementer; tránh sửa Movement nhiều.
5. **Projectile attacker attribution**: phải set `attackerRoot` của HitboxAuthoring trên prefab spawn = người bắn, nếu không mana/combo cộng nhầm + guard self-hit sai (người bắn tự trúng chưởng mình). Quan trọng.
6. **Projectile self-collision**: chưởng vừa spawn ở sát thân người bắn → hurtbox người bắn. Guard self-root trong HitboxAuthoring ĐÃ có (so victimRoot==attackerRoot) → set attackerRoot đúng là đủ.
7. **Double-spawn**: nếu clip có 2 Animation Event `SpawnProjectile` hoặc relay subscribe 2 lần → 2 chưởng. Subscribe per-instance ở OnEnable/Exit, unsubscribe đối xứng.
8. **Despawn leak**: projectile không despawn → tích tụ. Lifetime timer + despawn khi ra ngoài camera/va wall. Cap số projectile/người nếu cần (giống item cap P13).
9. **Charge giữ nút nhưng bị gián đoạn**: nếu đang charge mà ăn đòn → hủy charge (vào HurtState do HitReaction, state special tự Exit). Đảm bảo Exit unsubscribe relay.
10. **Edge double-fire của Super button**: phải `ConsumePress(Super1/Super2)` sau khi ăn (memory phase4) — nếu không 1 nhấn ra nhiều super, trừ mana nhiều lần.
11. **Hitstop áp cho projectile**: HitstopController P5 freeze Animator+velocity của 2 root. Projectile là object riêng — khi chưởng trúng, hitstop áp cho người bắn + victim (theo HitData root) là OK; bản thân chưởng có thể bay tiếp hoặc tự tắt. Quyết định: chưởng 1-hit thì tắt ngay sau hit (despawn on hit) — đơn giản nhất, tránh multi-hit ngoài ý muốn.

---

## 5. Giá trị mặc định đề xuất (đưa vào ScriptableObject nào) — CẦN TEST

| Tham số | Giá trị khởi điểm | Đặt ở | Ghi chú |
|---|---|---|---|
| Super1 mana cost | 2 ô (≈50%) | `MoveData.manaCostUnits` | CẦN TEST. MaxMana 4 ô. |
| Super2 mana cost | 3 ô (≈75%) | `MoveData.manaCostUnits` | CẦN TEST. |
| MaxMana / ManaPerSegment | 400 / 100 → 4 ô | `ResourceConfigSO` (P8, đã có) | Điều chỉnh để 2/3 ô ra đúng cảm 50/75%. KHÔNG sửa code ManaBar. |
| Special cost (chưởng-normal) | 1 ô | `MoveData.manaCostUnits` | "projectile-type costs mana" (GDD). |
| Special motion window | dùng `InputConfigSO.motionWindow` đã có | Core SO P4 | Không thêm field mới. |
| Charge threshold | 30 frame (~0.5s) | `MoveData.chargeFrames` (đã có) | CẦN TEST. |
| Charged damage/kb multiplier | ×1.4 | hằng trong SpecialState hoặc MoveData biến thể | CẦN TEST. |
| Projectile speed | 10 units/s | `MoveSetData` entry hoặc field mới trên một `ProjectileData`/MoveData | Bay thẳng gravityScale=0. CẦN TEST. |
| Projectile lifetime | 2.5 s | prefab/`ProjectileSpawner` field | Hoặc despawn ngoài camera. CẦN TEST. |
| Projectile despawn-on-hit | true | prefab logic | 1-hit chưởng (tránh multi-hit). |
| Super air-usable | true | `SpecialController`/`SuperState` cờ | GDD §4.2. |

> Mọi số "CẦN TEST" để Inspector chỉnh, không hardcode (CLAUDE.md §6). `MoveData` đã có sẵn:
> `motion`, `button`, `chargeFrames`, `manaCostUnits`, `rageCost`, `role`, `requiresRageMode`,
> `category`, `animatorStateId`, `baseDamagePercent`, `launchAngle`, `baseKnockback`, `knockbackGrowth`
> — **đủ cho P9, không cần mở rộng schema MoveData** (trừ khi muốn tách projectileSpeed/lifetime —
> khuyến nghị để ở prefab/MoveSetData để giữ MoveData thuần "đòn").

---

## 6. Checklist bàn giao cho Implement (việc cụ thể, module FA.Specials)

- [ ] `SpecialState : IState` (FA.Specials) — clone pattern `AttackState`: set StateId, khóa input tới
      `RecoveryEnd`, subscribe relay per-instance, gate grounded; tham số bởi `MoveData`. KHÔNG đếm frame.
- [ ] `SuperState : IState` (FA.Specials) — như trên nhưng **bỏ gate grounded** (air-usable), không motion.
      Xử lý về Idle/Air khi RecoveryEnd theo grounded.
- [ ] `MoveSetData : ScriptableObject` (FA.Core) — list entry `{MotionInput motion, InputButton button,
      MoveData move, int? chargedManaUnits}`; lookup helper. (Đặt Core để FA.Specials chỉ ref Core; không
      đụng `CharacterData` của Movement.)
- [ ] `SpecialController : MonoBehaviour` (FA.Specials) — entry-point song song CombatController:
      đọc `ICombatContext.Parser` → `DetectMotion` + nút edge → tra `MoveSetData` → `ManaBar.TrySpend` →
      `ConsumePress` → ChangeState(SpecialState). Super1/Super2 edge → SuperState. Đảm bảo chạy TRƯỚC
      CombatController (Script Execution Order) hoặc consume nút để special "thắng" normal.
- [ ] `ProjectileSpawner : MonoBehaviour` (FA.Specials) — subscribe `AnimationEventRelay.SpawnProjectileEvent`
      per-instance; Instantiate prefab tại muzzle theo `Facing`, set `Rigidbody2D.linearVelocity`, gán
      attackerRoot + `OnHitLandedChannel` cho `HitboxAuthoring` của projectile.
- [ ] `Projectile : MonoBehaviour` (FA.Specials) — lifetime/despawn (timer + ngoài camera + on-hit/va wall);
      KHÔNG tự dời transform (dùng Rigidbody2D).
- [ ] (Tùy) `CommandParser.HeldFrames(InputButton)` (FA.Core) — chỉ THÊM method cho charge nếu cần; không sửa method cũ.
- [ ] Projectile prefab: Rigidbody2D (gravityScale theo loại) + BoxCollider2D body (layer ProjectileP1/P2)
      + child Hitbox (HitboxAuthoring tái dùng, layer Hitbox, MoveData projectile).
- [ ] Animator: state cho từng special/super (StateId mới), transition theo StateId; Animation Event
      `SpawnProjectile`/`HitboxOn`/`HitboxOff`/`RecoveryEnd` trên clip.
- [ ] MoveData asset: special (motion + button + role Projectile/Reversal/AntiAir...), super1 (Super1,
      2 ô), super2 (Super2, 3 ô), chưởng-normal (role Projectile, 1 ô).
- [ ] Scene `Test_Phase09` + dummy: test motion→special, mana-gate super, projectile bay/trúng/knockback,
      charge mạnh hơn. Verify normal P5 KHÔNG hồi quy (nL vẫn ra khi không có motion).
- [ ] Cập nhật `Docs/CharacterBuildGuide.md` §Phase 9 (khai motion+cost trong MoveData/MoveSetData,
      dựng projectile prefab, gán role).

### Quyết định cần người dùng xác nhận (GDD §6 để ngỏ)
- Số/loại special mỗi nhân vật, role nào (cần xem sprite) — dùng placeholder 1 projectile + 1 reversal.
- Air special có/không cho từng nhân vật (mặc định: super air-yes, special thường ground-only ở P9).
- Mana model: giữ "số ô" (khuyến nghị) hay đổi sang % tuyệt đối (sửa ManaBar — không khuyến nghị).
