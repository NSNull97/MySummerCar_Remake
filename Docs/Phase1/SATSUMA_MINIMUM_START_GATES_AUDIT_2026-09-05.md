# Minimum stock-engine structural start gates — bounded read-only audit

Classification: `BehavioralReference`. No donor/staging edits, runtime changes,
Unity execution, full engine port or gameplay verification were performed.
Scope: Combustion GO3028 / Transform39085, FSM104983 Cylinders and FSM104984
Powertrain only. Fuel, starter wiring, electrical supply and ignition are outside
this two-FSM audit. Wear/damage branches are recorded only to qualify the scope.

Source: `E:/GAYmDev_Studio/MySummerCar_Remake_Game_DonorStaging/raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity`.
Verified SHA256: `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Line numbers below locate frozen YAML state/actionData records; action indexes
are zero-based. Statements describe decoded actions, not a copied runtime FSM.

```text
L48783: GO3028 Combustion; Transform39085; FSM104983 + FSM104984.
L2328699: FSM104984 Powertrain.
L2328823 Crankshaft: A0 GO32629.Data.Installed.
                    A1 false -> Not Ok 2; true -> Crank damage.
L2328986 Camshaft: A0 GO34424.Data.Installed; false -> Not Ok.
L2329076 Timing chain: A0 GO23.Data.Installed; false -> Not Ok.
L2329166 Rocker shaft: A0 GO19072.Data.Installed; false -> Not Ok 4.
L2329402 Rocker damage: GO19072.Data.Damaged=true -> Not Ok 4.
L2329492 Crank damage: GO32629.Data.Damaged=true -> State 1.
L2328913/2329256/2329329: PowertrainOK=false; GO9489.Starter.ShutOff=true.
L2328764 Wait: A0 PowertrainOK=true; A1 Wait(1 second, realTime).
L2322856: FSM104983 Cylinders; startState State 1.
L2325259 State 1: CombustionOK=false -> Reset.
L2322872 Reset: Power=0; Cyl1Fires..Cyl4Fires=false.
L2325169 Cylinderhead: A0 GO10083.Data.Bolted; false -> Not Ok.
L2325718 Flywheel: A0 GO29466.Data.Installed; A1 GO18977.Data.Installed.
                   A2 stock=true -> stock; A3 racing=true -> racing.
                   Neither installed -> FINISHED -> Not Ok.
L2324498 Electrics: A0 GO31811.Data.Installed -> Installed1.
                    A1 GO18381 Distributor.Data.Bolted -> Installed2.
                    A2 BoolAllTrue[Installed1,Installed2] -> Plug wear.
                    Otherwise FINISHED -> Not Ok.
L2323262 Cylinder1: piston GO2170.Installed + plug GO844.Installed.
L2323571 Cylinder2: piston GO16624.Installed + plug GO34637.Installed.
L2323880 Cylinder3: piston GO20184.Installed + plug GO9428.Installed.
L2324189 Cylinder4: piston GO10907.Installed + plug GO10828.Installed.
Each CylinderN: either false -> skip cylinder; no immediate Not Ok.
Piston wear<10 or plug wear<1 -> skip; equality passes.
L2323403 Add to power1: Power+=1; Cyl1Fires=true; same pattern N2..4.
Plug Tightness<8 or Wear<10 -> random misfire; CylNFires remains true.
L2326749 Piston pairs ok?: none[1,4] OR none[2,3] -> Not Ok.
L2326801 byteData names Cyl1Fires,Cyl4Fires,Cyl2Fires,Cyl3Fires.
L2324627 Not Ok: CombustionOK=false; GO9489.Starter.ShutOff=true.
L2327312 Wait: A3 CombustionOK=true; A4 Wait(1 second, realTime).
```

For undamaged stock content, these structural checks require installed crank,
camshaft, timing chain, rocker shaft, flywheel and Electrics; bolted cylinder
head and distributor; and `(cylinder1 || cylinder4) && (cylinder2 || cylinder3)`.
A qualifying cylinder has its piston and plug installed. A blanket
`AreAllInstalledAndSecured` or an all-four-plugs minimum is stricter than this
evidence. `Electrics` is database part GO31811, not proof of a separate wire gate.
Head-gasket branches produce symptoms/power loss; a direct startup prohibition
from the gasket was not established here. Full start readiness must also apply
the separately evidenced starter, supply, ignition and fuel requirements.
