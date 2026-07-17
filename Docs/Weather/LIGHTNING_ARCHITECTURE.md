# Архитектура молний

## Разделение authority

- ambient visual lightning — presentation-only, без gameplay effect;
- gameplay strike — project-owned выбор точки, затем отдельные presentation, thunder и gameplay-effect hooks.

Enviro используется только для visual bolt. Автономный vendor storm scheduler и vendor thunder audio отключены.

## Gameplay selection

`LightningStrikeDirector` использует отдельный deterministic RNG/state. Вес кандидата учитывает exposure, height/attractor, protection, cooldown и fairness. Player proxy может участвовать только в diagnostics; player не является прямой целью по умолчанию. Persistent state использует stable candidate IDs, а не scene instance IDs.

Ambient request не продвигает sequence/counters, по которым считается gameplay fairness. Поэтому серия атмосферных вспышек не повышает и не понижает шанс следующего gameplay-кандидата.

Spawn/load grace, global cooldown и per-candidate cooldown запрещают немедленный удар после restore. Fairness использует число завершённых gameplay strikes по candidate records. Thunder delay вычисляется как listener distance / speed of sound.

## Events и damage status

Gameplay path выдаёт:

- `LightningStrikeEvent`;
- `LightningPresentationRequest`;
- `ThunderAudioRequest`;
- typed gameplay-effect hook.

Health/death authority в проекте пока отсутствует. 07B работает в non-lethal DEV mode и не создаёт параллельную health-систему. Electrical/NPC/damage consumers, forest fire, power grid и destruction не входят в milestone.

## Presentation isolation и failure modes

Adapter использует один isolated runtime-клон `LightningStrike.prefab` и один runtime flash material; paid source prefab/material не мутируются. Visual intensity ограниченно аппроксимируется runtime color inputs.

Strike отклоняется без side effects при restore grace, cooldown, пустом/невалидном наборе кандидатов, non-finite position/weight или полном protection. Missing Enviro visual capability не отменяет authoritative gameplay event, но обязан дать явную presentation diagnostic.
