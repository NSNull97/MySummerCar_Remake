# Stock first-start: starter, fuel and ignition gates

Classification: `BehavioralReference`. Bounded, read-only action decoding;
no donor/staging writes, Unity execution or live-donor gameplay verification.
Companion to `SATSUMA_MINIMUM_START_GATES_AUDIT_2026-09-05.md`, which covers
Combustion GO3028 / Cylinders104983 and Powertrain104984.

Source: the same locked external `GAME.unity` used by that audit, SHA-256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Offsets below are bytes; lines refer to this frozen YAML. Action indexes are
zero-based. The recorded conditions were decoded from action names, enable
flags, parameter indexes/bytes, object/string reference arrays and transitions,
not inferred from variable names alone. Raw donor FSM/runtime code is not copied.

## Proven distinction: cranking is not combustion

Starter GO9489 has MotorParts FSM106806 (byte92996816, L3492255) and Starter
FSM106807 (byte93007437, L3492646).

Normal key path:

`Wait for start -> Electricity OK -> ACC -> Motor installed -> Wiring ->
Starter damage -> Check clutch -> Turn key -> Fuel Mixture -> Start or not ->
Start engine -> Crank up -> Running`.

- `Turn key` L3493012 A5 sets the shared RPM variable to300 every frame;
  audio/battery consumption already run, with a0.316s delay to Fuel Mixture.
- `Fuel Mixture` L3493204 A8 also sets RPM300 every frame and runs starter audio.
  A11 reads FuelGO18546/FuelLine.FuelOK; A12 true sends START ENGINE to
  `Start or not`; false has no transition and therefore continues cranking.
  Battery Charge<80 or mouse release sends KEYUP to Wait.
- `Start or not` L3494014 A1/A2 read CombustionGO3028.Cylinders.CombustionOK and
  Powertrain.PowertrainOK every frame. A8 still sets RPM300 every frame. A10
  BoolAllTrue of those two sends RUN to Start engine; false does not stop the
  starter. Mouse release returns to Wait.

Therefore fuel/combustion/powertrain readiness is **not a CanCrank gate**.
Provided the separate block/electrical/starter gates pass, missing plugs or
pistons (and even a missing crankshaft in these actions) can leave the starter
loop running without combustion. A no-pistons starter loop is not a claim of
physically rotating a nonexistent part; it describes the donor's control/audio
and RPM state. The previously established cylinder-pair condition still gates
successful combustion, rather than requiring all four plugs and pistons.

### ShutOff is a stall request, not a starter-lock flag

- `Start engine` L3494554 A10 clears ShutOff before its0.3s transition.
- `Crank up` L3494339 sets drivetrain RPM600; A2 MotorInstalled=false or A3
  ShutOff=true sends OFF to Stall engine. This is the post-catch0.2s phase,
  not the earlier RPM300 starter loop.
- `Running` L3496062 A7 MotorInstalled=false, A8 ShutOff=true or A9 RPM<100
  sends OFF to Stall engine.
- `Stall engine` L3495082 sets drivetrain maxTorque=0, maxPower=0,
  engineFrictionFactor=3 and canStall=true; returns to Wait near RPM100.
- `Wait` L3493462 A13 resets ShutOff=false and A14 Starting=false; RPM is0.

The Combustion/Fuel/Electrics writers that set Starter.ShutOff=true consequently
request loss of running combustion. Reading that flag as a blanket prohibition
on turning the starter would change the observed control contract.

## Block and starter attachment requirements

MotorParts106806 `Not installed` L3492271 A2 reads **BlockGO35519.Data.Bolted**,
everyFrame=true. True enters Installed. `Installed` L3492494 A0 sets
Starter.MotorInstalled=true; A1 continues reading Block.Data.Bolted and false
returns to Not installed, where A1 sets MotorInstalled=false.

Starter106807 `Motor installed` L3495019 tests that bool: true enters Wiring,
false returns to Wait. The engine must thus be attached/bolted to the car, not
merely resting in the bay or held close to the three supports. The independent
docking audit identifies this Block database's three engine mounting bolts and
the accepted ON2/OFF0 engine-assembly group contract. In the remake, require the
actual engine-block occupant of `mount.satsuma.engine-assembly` plus that group's
IsBolted; do not replace it with transform proximity or internal-part presence.

Starter106807 `Wiring` L3497196 A0..A3 and A4 BoolAllTrue require:

| Donor database object | Exact field | Current meaning |
| --- | --- | --- |
| WiringStarter15250 | Data.Bolted | Starter cable fastening |
| WiringBatteryMinus3446 | Data.Bolted | Negative battery terminal fastening |
| WiringGround1 7904 | Data.Installed | Ground connection |
| Starter1999 | Data.Installed | `vehicle.satsuma.part.starter` present |

Starter **part** Bolted is not read by this gate. Its installed state and its
separate cable fastening must not be conflated. Starter damage is a later
wear branch: WearStarter>25 proceeds directly; lower/equal values enter a
random broken/proceed choice. Full wear behavior is outside the overnight scope.
`Check clutch` L3496457 reads AutoClutch and enables StartHelp; it contains no
BoolTest that makes clutch pedal depression mandatory for cranking.

## Electrical supply, and what CoilHarness actually proves

Electrics GO22680 / FSM110521, byte163709337, L5983674:

- `Wiring` L5983753 A0..A4 and A5 BoolAllTrue require Battery26601.Installed,
  WiringBatteryPlus21525.Bolted, WiringBatteryMinus3446.Bolted,
  WiringIgnition23629.Installed and WiringBatteryHarness30568.Installed.
- `Battery` L5985257 reads Charge. Greater than97 proceeds to OK; lower goes
  Not Ok. Equality has no explicit FloatCompare event, then FINISHED goes
  Not Ok. OK L5983957 sets ElectricsOK=true. The donor later divides Charge
  by10 for displayed Volts; these native Charge units must not be copied
  blindly into the remake's existing voltage model.
- Starter106807 `Electricity OK` L3495841 reads that ElectricsOK before ACC.
- `Run on battery` L5984806 drains charge and only Charge<92 enters Engine off;
  equal/greater returns to Reset. The alternator/belt are not mandatory for a
  first start on a charged battery in this flow.

**CoilHarness is not established as a separate stock-battery start gate.**
Exact WiringCoil database is GO24055. A bounded exact-reference search of the
locked GAME found it in Starter's db_Required6 variable, wiring assembly/info
records, and Electrics State2. Starter106807 has the db_Required6 variable but
no decoded action owner references it. Presence of that variable is not proof.

The real consumer is Electrics110521 `State 2` L5985569 A0..A4:
Alternator17553.Installed, Fanbelt30703.Installed,
WiringAlternator34806.Installed, WiringCoil24055.Installed,
WiringRegulator25716.Installed must all be true. This branch is reached from
`Battery 2` L5985194 when Battery=false and permits the alternator-only running
path; otherwise Engine off sets ShutOff=true. It does not add CoilHarness to
the ordinary charged-battery Wiring/Start or not checks. Combustion separately
requires the installed physical Electrics/plug-wires part GO31811, as documented
in the companion audit. These are distinct database identities.

## Fuel: hard conditions versus leaks and wear

Fuel GO18546 / FuelLine FSM109337, byte142457403, L5231383:

| State / source line | Exact read / polarity | Stock project part |
| --- | --- | --- |
| Reset L5231399 | FuelTank12246.FuelLevel<0.2 -> Not Ok2; equality passes | Fuel quantity |
| Fueltank L5231492 | FuelTank12246.Installed false -> Not Ok2 | `vehicle.satsuma.part.fuel-tank` |
| Fuel strainer L5231655 | FuelStrainer481.Installed false -> Not Ok | `vehicle.satsuma.part.fuel-strainer` |
| Fuel Pump L5231745 | Fuelpump27941.Installed false -> Not Ok4 | `vehicle.satsuma.part.fuel-pump` |
| Carburator L5231991 | Carburator23454.Bolted; neither stock nor bolted alternatives7858/8183 -> Not Ok4 | `vehicle.satsuma.part.carburetor` |
| Timing chain L5232506 | Timingchain23.Installed false -> Not Ok4 | `vehicle.satsuma.part.timing-chain` |

Fuel pump and tank **Bolted** are not required by these particular start checks.
Fuel Pump damage L5232769 and Carburator damage L5232859 reject Damaged=true;
the stock first-start approximation must not be described as full wear parity.

- `Fuel line` L5233242 reads FuelLine24327.Bolted. True proceeds directly;
  false enters `Leak` L5233347, subtracts0.025 from tank FuelLevel, and **still
  proceeds to Fuel strainer**. Thus its fastening is a leakage condition, not
  an unconditional hard startup prohibition in this FSM. No separate
  FuelLine.Installed action occurs here.
- `Airfilter` L5233439 absence enters Dirt accumulation, then continues to
  Carburator damage. It is not an unconditional startup prohibition.
- No FuelTank electrical sender wire appears in FuelLine's hard supply checks.
  Do not make the gauge wire a fuel-pump power requirement; this is a mechanical
  fuel pump path.
- `Not Ok`, `Not Ok2`, `Not Ok4` set FuelOK=false and Starter.ShutOff=true;
  the latter two also reset CarbReserve=0.
- `Starting` L5232949 keeps an already true FuelOK. Otherwise `Carb fuel?`
  L5233012 waits until CarbReserve>=0.095 and Mixture>RequiredMixture before
  proceeding to Fuel OK. Reserve<0.095 or insufficient mixture returns to
  Fuel Usage. RequiredMixture's serialized default is0.5. Fuel Usage
  L5232162 accumulates CarbReserve from pump efficiency and caps it at0.1.

Mixture, reserve priming, pump wear, fuel leak rate/timing, upside-down behavior,
charging, damage and full running simulation are not implemented or verified
by this audit. For the bounded overnight integration, keep a documented
structural fuel-availability condition separate from a liquid-quantity bypass;
a debug refill/bypass must not install an absent tank, strainer, pump or carb.

## Inspection outcome

Additional bounded charging audit: Electrics110521 `Check alternator`
L5984413 A2..A5 reads **Installed**, not Bolted, for Fanbelt30703,
Alternator17553, WiringAlternator34806 and WiringRegulator25716;
A6 requires all four plus Battery (the Battery26601.Installed value read by
Wiring). Both OK and Not Ok reach this check, so a successful ElectricsOK
branch is not itself an additional charging gate. `Alternator damage`
L5984632 rejects Alternator17553.Data.Damaged=true; false reaches
`Alternator eff` L5985104, which calculates `(101-WearAlternator)*350`
from WearSetup34974.Data.WearAlternator. `Charge battery` L5984129 reads actual
drivetrain112082.rpm, sets battery MaxVoltage=145 and adds
`clamp(rpm/AlternatorEfficiency+(AmbientTemperature-30)/1000,0,0.25)`
to Battery.Data.Charge (A8, everyFrame=false/perSecond=false). It does not
simply charge because an engine-running bool is true. CoilHarness is absent
from this charging branch; it belongs to the separate battery-absent path
documented above. Belt Wear has no direct FloatCompare in this electrical
consumer: the actual belt Jumping FSM110290 (byte160468195), Reset A0/A2,
reads AlternatorBelt30703.Data.Wear and only **Wear>99** sends WEAR;
equal99 does not break. Belt destroyed sends DAMAGED to that database's
Data112909 (byte207888171), whose global DAMAGED transition enters No belt:
A1 sets Installed=false, so the next charging check fails. Belt wear here
is increasing damage, not remaining condition; do not copy its numeric
polarity onto the item-condition bridge without its documented conversion.
No Assets or donor files were changed and no Unity test was run for this
additional inspection.

No runtime, authoring, generated payload or save file was changed by this audit.
No Unity tests were run here. The source supports separate CanCrank and CanRun
gates and the exact stock structural reads above. It does **not** support
all-parts-fully-tightened, all-four-cylinder, mandatory alternator/belt, mandatory
fuel-sender wire, mandatory air-filter or mandatory CoilHarness hard gates for
the normal charged-battery first-start path. Other complete-game dependencies
remain bounded by the explicitly listed FSMs, not claimed globally absent.
