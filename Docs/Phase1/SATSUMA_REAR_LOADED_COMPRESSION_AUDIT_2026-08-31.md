# Satsuma 4A — rear loaded compression audit

Status: original V1d.38 diagnosis retained as historical evidence. The V1d.39
NWH/kinematic correction and complete scoped automated handoff results are
recorded in the linked correction report. The user accepted the bounded live
4A result on 2026-09-01 and
accepted V1d.38 extension without shock separation, then reported excessive
rear ride height and little compression when lowered onto an arm/wheel.
Issue 4A is now accepted ahead of the separate rear wheel-placement correction
(issue 5). No donor file is changed.

Subsequent authorized implementation is tracked separately in
[the 4A correction record](SATSUMA_REAR_LOADED_COMPRESSION_FIX_2026-08-31.md).
The analysis below describes the pre-fix V1d.38 baseline.

## Verdict

There is a confirmed force-curve mismatch, not merely a visual spring issue.
The original stock rear wheel spring has zero force at full droop. The remake
still applies approximately 2.65 kN along the spring at its accepted stock
droop stop, equivalent to approximately 1.32 kN of support at each rear wheel
through the arm leverage. A lightly loaded rear corner can therefore remain
near the droop stop instead of settling into its travel.

V1d.38 corrected the excessive travel but deliberately retained the previous
free lengths and forces. The shorter allowed extension increased the existing
preload at the new stop. The force mismatch is demonstrated by code and geometry;
the exact loads, contacts and settled pose of the user's screenshot were not
measured in a new runtime capture. Do not infer exact ride height from the image.

## Original-game authority and force

Read-only frozen export: `msc-world-baseline-04a1.1-c3f2f337`,
`GAME.unity` SHA256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Source root is the external staged export recorded in the V1d.38 report.

[Wheel.cs:1074](E:/GAYmDev_Studio/MySummerCar_Remake_Game_DonorStaging/raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/Scripts/Assembly-CSharp/Wheel.cs:1074)
calculates `springForce = suspensionRate * compression`.
Compression is in metres, clamped to `[0, suspensionTravel]` at line 846.
Airborne compression and suspension force are explicitly zero at lines 988–998.
Grounded force acts on the chassis at the wheel position at line 913. Neither
body mass nor wheel mass multiplies the spring-force formula.

Suspension FSM 108170 overwrites serialized/startup wheel settings:

| Installed spring | Wheel spring rate (N/m) | Travel (m) | Carrier local Y (m) | Force at 0 / 50 mm compression |
|---|---:|---:|---:|---:|
| None | 2 | 0.14 | −0.150 | 0 / 0.1 N |
| Stock | 21200 | 0.14 | −0.165 | 0 / 1060 N |
| Extra-long | 29000 | 0.17 | −0.180 | 0 / 1450 N |
| Rally, reference only | 25000 | 0.15 | −0.165 | 0 / 1250 N |

Frozen `GAME.unity` rate/travel references: none RL 4467185/4467295,
RR 4467480/4467590; stock RL 4468329/4468439, RR 4468624/4468734;
long RL 4468919/4469029, RR 4469214/4469324; rally RL 4473479/4473589,
RR 4473774/4473884. The FSM checks `Data.Installed`, not `Bolted`.
Carrier Y plus `compression - travel` moves the model/IK target
(`Wheel.cs:759`); carrier offset is not a preload term.

For illustration, a static 100 kg equivalent load on one stock rear corner,
away from bump stops and other contacts, gives `981 / 21200 = 0.0463 m`
compression. This is suspension travel, not tyre deflection or a prediction of
the current screenshot's axle load.

Stock shock bump/rebound coefficients are 1000/1000 N·s/m; absent shock 2/2.
They are selected independently from spring type. Fast damping starts at
0.3 m/s with factors 0.3; rear anti-roll rate is zero. See `Wheel.cs:1076–1084`
and `GAME:4467765/4467875`, `4466621/4466731`, `6994310`, `6994418–6994420`.
Damping is zero at rest and cannot correct static ride height.

## Current remake and the V1d.38 interaction

[SatsumaRearSuspensionController.cs:319](E:/GAYmDev_Studio/MySummerCar_Remake/Assets/Game/Vehicle/Assembly/Runtime/SatsumaRearSuspensionController.cs:319)
uses stock rate 32000 N/m and free length 0.24 m; long rate 36000 N/m and free
length 0.27 m. At zero velocity, after installation expansion completes:

```text
F_coil = clamp(K_coil * max(0, freeLength - seatDistance), 0, 6000 N)
```

The generated prefab contains these same values; this is not an unused default.
Forces are equal/opposite at the actual upper/lower spring seats. Their sign is
correct and rear NWH force ownership is disabled. No reversed force or obvious
duplicate rear spring-force owner was found.

Read-only calculations use the builder's arm pivot, neutral rotation, seats
and wheel centres at lines 258, 317 and 419. Representative RL results:

| Arm pose | Seat distance | Coil force | Equivalent vertical wheel support |
|---|---:|---:|---:|
| Previous arbitrary −32° stop | 197.48 mm | 1361 N | approximately 715 N |
| Accepted stock −16.138° stop | 157.29 mm | 2647 N | approximately 1323 N |
| −10° | 141.01 mm | 3168 N | approximately 1576 N |
| 0° | 113.92 mm | 4035 N | approximately 2017 N |
| Stock compression stop +9.216° | 88.70 mm | 4842 N | approximately 2447 N |

The wheel-equivalent value is obtained by balancing the coil torque about the
arm's chassis-X axis against vertical force at the wheel centre. Using the
nearby drum centre instead changes the stock-droop estimate to approximately
1315 N, not the conclusion. This is a geometric spring contribution, not a
live contact-force measurement; unsprung gravity, chassis pitch and contact
locations still matter.

Across both stock corners the initial wheel-equivalent spring support is about
2.64 kN, or 270 kg equivalent rear load, before additional compression. The old
stop had about 1.85 times less initial support. Long springs have approximately
1.71 kN per wheel at their own accepted droop, about 349 kg across the axle.

Do not compare `32000` directly with donor `21200` and call the remake 1.5 times
stiffer: donor K is wheel-referenced; remake K acts at the spring seat. The
motion ratio is approximately 0.5 at stock droop, and the present effective
wheel stiffness there is only about 7.7 kN/m. The primary mismatch is the
offset/shape of the force curve, not simply a large stiffness scalar. Merely
reducing K or replacing it with 21200 does not reproduce the original curve.

## Mass and centre of mass: separate mismatch, not the primary explanation

Frozen root Rigidbody mass 389 kg (`GAME:1371825`) is a serialized value, not
the final runtime baseline. FSM 106931 `SetMass` initializes `CarMass` to 400
and applies that variable periodically; installation/removal changes it
(`GAME:3564569–3564740`). Exact mass for the user's incomplete assembly requires
matching its installed parts and state; neither 389 nor 400 alone proves it.

Donor CarDynamics 112090 references CoG transform 68158 at `(0, 0.2, 0)`
(`GAME:6994311`, `1001236–1001246`). `CarDynamics.cs:720` assigns the transform's
local position to body centre of mass every FixedUpdate. The remake builder's
comment at line 6118 saying the donor does not override CoM is therefore wrong
for runtime: it calls ResetCenterOfMass at line 6122, producing bare CoM
`(0.0003170822, 0.067791566, -0.3310254)` in the generated prefab.

The remake mass controller excludes dynamically installed parts from chassis
mass aggregation; their separate bodies retain their mass. No direct double
counting of the dynamic rear assembly was found. A backwards CoM shift normally
adds rear load and therefore favours more compression, not less. It cannot by
itself explain the reported lack of sag. Do not tune CoM or chassis mass to
conceal the spring preload; preserve accepted behaviour while this separate
parity discrepancy is assessed explicitly.

## Why passing tests did not detect this

- `RearSpringShowsLoadedDeflectionAndShockDampedRebound` freezes the chassis and
  applies 3500 N upward directly at the lower spring seat. It only checks more
  than 15 mm transient shortening and rebound, not sag under vehicle weight.
- `RearSpringsLiftTheChassisThroughGroundedDrums` freezes chassis rotation and
  requires a height increase greater than 20 mm, with no target sag or upper
  ride-height limit. It does not exercise the user's wheel/front-contact setup.
- Existing V1d.38 telemetry in `Logs/codex-rear-droop-v1d38-physics.log:1515`
  records a transient minimum seat distance of 2.73 mm in the artificial-force
  test. That is not a calibrated settled length. At line 1541 the grounded-drum
  fixture settles near −3°; its different constraint/load scheme does not
  disprove the user report.

The previous 38/38 result remains factual but does not certify donor ride height.
This audit runs no new Unity tests or original-game capture. Checks executed:
scoped source/generated-prefab/log inspection, independent PowerShell geometry
and static-force arithmetic, and cross-review of donor force/mass and remake
force ownership. Parallel work is not inspected for current compile status.

## Короткое ТЗ по 4А

1. Сохранить принятые V1d.38 пределы хода и отсутствие разрыва амортизатора.
   Переднюю подвеску, установку колёс и сейвы не менять.
2. Привязать нулевое сжатие/нулевую силу пружины к полному вывесу её профиля.
   Воспроизвести донорскую силовую кривую у колеса с учётом плеча рычага;
   не переносить коэффициент Wheel напрямую на физическое седло пружины.
3. Отдельно проверить перевод демпфирования между этими координатами. Не
   исправлять статическую высоту демпфером, массой, CoM или растяжением мешей.
4. Добавить калибровочные тесты: нулевая сила при вывесе, сила/сжатие при
   известных нагрузках, например 100 кг на stock → около 46 мм донорного
   хода. Различать ход подвески, дугу физической ступицы и деформацию шины.
5. Проверить свободный кузов без заморозки наклона при опоре на барабаны/колёса,
   затем подъём и повторное опускание домкрата. Записывать реальную нагрузку,
   профиль, угол, высоту и расстояние между седлами, включая обе стороны и
   лёгкую/нагруженную сборку. Сохранить проверки перекрытия половинок аморта.
6. После автотестов получить отдельную приёмку 4А пользователем. До неё к №5
   не переходить. Несовпадение массы/CoM учитывать в диагностике, не исправлять
   попутно без отдельного согласованного объёма и регрессий.

Implementation has not started. This specification is the next scoped task,
not a claim that loaded compression or the remaining V1d.38 checks are fixed.
